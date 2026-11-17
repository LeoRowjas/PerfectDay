using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Services;

public sealed class TaskService(
    ITaskRepository tasks,
    IScheduleRepository schedules,
    TaskSeriesGenerator generator,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskType? type, DateOnly today, CancellationToken ct)
    {
        if (await generator.EnsureAsync(userId, TaskSeriesGenerator.Horizon(today), ct))
            await unitOfWork.SaveChangesAsync(ct);

        var items = await tasks.ListAsync(userId, type, ct);
        return items.Select(t => t.ToDto()).ToList();
    }

    public async Task<TaskDto> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
        (await FindAsync(userId, id, ct)).ToDto();

    public async Task<TaskDto> CreateAsync(Guid userId, SaveTaskRequest request, DateOnly today, CancellationToken ct)
    {
        if (request.Repeat is { } repeat)
        {
            var series = NewSeries(userId, request, repeat);
            var first = series.DatesFrom(series.StartDate, series.StartDate.AddDays(TaskSeriesGenerator.HorizonDays)).FirstOrDefault();
            if (first == default) throw new AppValidationException("В выбранный период нет ни одного повторения.");

            tasks.AddSeries(series);
            var created = series.Generate(Max(TaskSeriesGenerator.Horizon(today), first));
            tasks.AddRange(created);
            await unitOfWork.SaveChangesAsync(ct);
            return created[0].ToDto();
        }

        var task = new TaskItem(userId, CleanTitle(request), request.Type!.Value, request.DueDate!.Value,
            request.DurationMinutes!.Value, request.Priority!.Value);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDto();
    }

    /// <summary>
    /// У повторения периодической задачи изменение относится к нему и всем следующим: будущие повторения
    /// создаются заново по новому правилу. Без правила серия останавливается.
    /// </summary>
    public async Task<TaskDto> UpdateAsync(Guid userId, Guid id, SaveTaskRequest request, DateOnly today, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);

        // Блок со старыми типом, сроком или длительностью больше не верен: задача вернётся в «не размещённые».
        var planningChanged = task.Type != request.Type || task.DueDate != request.DueDate ||
                              task.DurationMinutes != request.DurationMinutes;
        if (planningChanged) await RemoveFromSchedulesAsync(id, ct);

        task.Update(CleanTitle(request), request.Type!.Value, request.DueDate!.Value,
            request.DurationMinutes!.Value, request.Priority!.Value);

        if (task.Series is { } series && !IsSameSeries(series, task, request.Repeat))
        {
            await RemoveFutureAsync(series, task, ct);
            if (request.Repeat is { } repeat)
            {
                series.Update(task.Title, task.Type, task.DurationMinutes, task.Priority, repeat.Kind!.Value,
                    repeat.Days ?? [], Min(series.StartDate, task.DueDate), repeat.Until);
                series.RestartAfter(task.DueDate);
                tasks.AddRange(series.Generate(Max(TaskSeriesGenerator.Horizon(today), task.DueDate)));
            }
            else
            {
                task.DetachFromSeries();
                tasks.RemoveSeries(series);
            }
        }
        else if (request.Repeat is { } repeat)
        {
            var created = NewSeries(userId, request, repeat);
            tasks.AddSeries(created);
            task.AttachToSeries(created);
            created.RestartAfter(task.DueDate);
            tasks.AddRange(created.Generate(Max(TaskSeriesGenerator.Horizon(today), task.DueDate)));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> SetCompletedAsync(Guid userId, Guid id, bool isCompleted, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);
        task.SetCompleted(isCompleted);
        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDto();
    }

    /// <summary>Переносит задачу на другой день: её блоки уходят из старого расписания, в новом она встанет сама.</summary>
    public async Task<TaskDto> SetDayAsync(Guid userId, Guid id, DateOnly dueDate, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);
        if (task.DueDate != dueDate)
        {
            await RemoveFromSchedulesAsync(id, ct);
            task.MoveTo(dueDate);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return task.ToDto();
    }

    /// <param name="withFollowing">Удалить и все следующие повторения, а серию остановить.</param>
    public async Task DeleteAsync(Guid userId, Guid id, bool withFollowing, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);
        if (withFollowing && task.Series is { } series)
        {
            await RemoveFutureAsync(series, task, ct);
            tasks.RemoveSeries(series);
        }

        await RemoveFromSchedulesAsync(id, ct);
        tasks.Remove(task);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Будущие невыполненные повторения после этого удаляются; выполненные остаются в истории.
    private async Task RemoveFutureAsync(TaskSeries series, TaskItem current, CancellationToken ct)
    {
        foreach (var next in await tasks.ListOpenInSeriesAsync(series.Id, current.DueDate, ct))
        {
            if (next.Id == current.Id) continue;
            await RemoveFromSchedulesAsync(next.Id, ct);
            tasks.Remove(next);
        }
    }

    private static bool IsSameSeries(TaskSeries series, TaskItem task, RepeatDto? repeat) =>
        repeat is not null && repeat.Kind == series.Kind && repeat.Until == series.EndDate &&
        (series.Kind == RecurrenceKind.Daily || (repeat.Days ?? []).ToHashSet().SetEquals(series.Days)) &&
        series.Title == task.Title && series.Type == task.Type &&
        series.DurationMinutes == task.DurationMinutes && series.Priority == task.Priority;

    private TaskSeries NewSeries(Guid userId, SaveTaskRequest request, RepeatDto repeat) =>
        new(userId, CleanTitle(request), request.Type!.Value, request.DurationMinutes!.Value, request.Priority!.Value,
            repeat.Kind!.Value, repeat.Days ?? [], request.DueDate!.Value, repeat.Until);

    private async Task<TaskItem> FindAsync(Guid userId, Guid id, CancellationToken ct) =>
        await tasks.GetAsync(userId, id, ct) ?? throw new NotFoundException("Эта задача уже удалена. Список обновлён.");

    private async Task RemoveFromSchedulesAsync(Guid taskId, CancellationToken ct)
    {
        foreach (var schedule in await schedules.ListContainingTaskAsync(taskId, ct))
        foreach (var block in schedule.Blocks.Where(b => b.TaskId == taskId).ToList())
            schedule.RemoveBlock(block);
    }

    private static string CleanTitle(SaveTaskRequest request)
    {
        var title = request.Title.Trim();
        if (title.Length == 0) throw new AppValidationException("Укажите название.");
        return title;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
