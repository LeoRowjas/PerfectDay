using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Services;

public sealed class TaskService(ITaskRepository tasks, IScheduleRepository schedules, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskType? type, CancellationToken ct)
    {
        var items = await tasks.ListAsync(userId, type, ct);
        return items.Select(t => t.ToDto()).ToList();
    }

    public async Task<TaskDto> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
        (await FindAsync(userId, id, ct)).ToDto();

    public async Task<TaskDto> CreateAsync(Guid userId, SaveTaskRequest request, CancellationToken ct)
    {
        var task = new TaskItem(userId, CleanTitle(request), request.Type!.Value, request.DueDate!.Value,
            request.DurationMinutes!.Value, request.Priority!.Value);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> UpdateAsync(Guid userId, Guid id, SaveTaskRequest request, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);

        // Блок со старыми типом, сроком или длительностью больше не верен: задача вернётся в «не размещённые».
        var planningChanged = task.Type != request.Type || task.DueDate != request.DueDate ||
                              task.DurationMinutes != request.DurationMinutes;
        if (planningChanged) await RemoveFromSchedulesAsync(id, ct);

        task.Update(CleanTitle(request), request.Type!.Value, request.DueDate!.Value,
            request.DurationMinutes!.Value, request.Priority!.Value);

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

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var task = await FindAsync(userId, id, ct);
        await RemoveFromSchedulesAsync(id, ct);
        tasks.Remove(task);
        await unitOfWork.SaveChangesAsync(ct);
    }

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
}
