using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

public sealed class ScheduleService(
    IUserRepository users,
    ITaskRepository tasks,
    IScheduleRepository schedules,
    IUnitOfWork unitOfWork)
{
    private const string LunchTitle = "Обед и отдых";

    public async Task<ScheduleDto> GetAsync(Guid userId, DateOnly date, DateOnly today, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        var schedule = await schedules.GetAsync(userId, date, ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Заново раскладывает задачи дня. Закреплённые и выполненные блоки остаются на месте.</summary>
    public async Task<ScheduleDto> BuildAsync(Guid userId, DateOnly date, DateOnly today, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var profile = CreateProfile(user, date);
        var workday = TimeRange.From(user.WorkdayStart, user.WorkdayEnd);

        var schedule = await schedules.GetAsync(userId, date, ct);
        if (schedule is null)
        {
            schedule = new DaySchedule(userId, date);
            schedules.Add(schedule);
        }

        var kept = schedule.Blocks
            .Where(b => IsFixed(b) && RangeOf(b).Start >= workday.Start && RangeOf(b).End <= workday.End)
            .ToList();
        var keptMinutes = MinutesByTask(kept);

        var plannable = (await tasks.ListPlannableAsync(userId, date, date == today, ct))
            .Select(task => ToPlannable(task) with { DurationMinutes = task.DurationMinutes - keptMinutes.GetValueOrDefault(task.Id) })
            .Where(task => task.DurationMinutes > 0);

        var lunchTarget = kept.Any(b => b.Kind == BlockKind.Break) ? null : DayPlanner.DefaultLunchStart(workday);
        var plan = DayPlanner.PlaceAround(profile, workday, plannable, kept.Select(RangeOf).ToList(), lunchTarget);

        schedule.ReplaceBlocks(kept.Concat(CreateBlocks(schedule, profile, plan)).ToList());
        await unitOfWork.SaveChangesAsync(ct);

        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Закрепляет блок на новом времени и перестраивает незакреплённую часть дня вокруг него.</summary>
    public async Task<ScheduleDto> MoveBlockAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, TimeOnly newStart, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var profile = CreateProfile(user, date);
        var workday = TimeRange.From(user.WorkdayStart, user.WorkdayEnd);

        var schedule = await schedules.GetAsync(userId, date, ct)
                       ?? throw new NotFoundException("Расписание на эту дату ещё не построено.");
        var moved = schedule.Blocks.FirstOrDefault(b => b.Id == blockId)
                    ?? throw new NotFoundException("Этот блок уже изменился. Расписание обновлено, выберите его снова.");

        var others = schedule.Blocks.Where(b => b.Id != blockId).ToList();
        var oldRange = RangeOf(moved);
        var newRange = DayPlanner.ValidateMove(workday, newStart, oldRange.Length, others.Where(IsFixed).Select(RangeOf));

        // Всё, что стоит раньше затронутого интервала и не мешает блоку, остаётся на месте.
        var cutoff = Math.Min(oldRange.Start, newRange.Start);
        var kept = others.Where(b => IsFixed(b) || (RangeOf(b).Start < cutoff && !RangeOf(b).Overlaps(newRange))).ToList();
        var replanned = others.Except(kept).ToList();

        var lunchBlock = replanned.FirstOrDefault(b => b.Kind == BlockKind.Break);
        int? lunchTarget = lunchBlock is null ? null : TimeRange.ToMinutes(lunchBlock.Start);
        var replannedTasks = replanned
            .Where(b => b.Kind == BlockKind.Task)
            .GroupBy(b => b.Task!)
            .Select(group => ToPlannable(group.Key) with { DurationMinutes = group.Sum(b => RangeOf(b).Length) });

        var plan = DayPlanner.PlaceAround(profile, workday, replannedTasks,
            kept.Select(RangeOf).Append(newRange).ToList(), lunchTarget);

        foreach (var block in replanned) schedule.RemoveBlock(block);
        schedule.AddBlocks(CreateBlocks(schedule, profile, plan));

        var explanation = moved.Task is { } movedTask
            ? PlacementExplainer.ForManualMove(profile, movedTask.Type, newRange)
            : moved.Explanation;
        moved.Reschedule(newRange.StartTime, newRange.EndTime, explanation);
        moved.Pin();

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    public async Task<ScheduleDto> UnpinBlockAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var schedule = await schedules.GetAsync(userId, date, ct)
                       ?? throw new NotFoundException("Расписание на эту дату ещё не построено.");
        var block = schedule.Blocks.FirstOrDefault(b => b.Id == blockId)
                    ?? throw new NotFoundException("Этот блок уже изменился. Расписание обновлено, выберите его снова.");

        block.Unpin();
        if (block.Task is { } task)
            block.Reschedule(block.Start, block.End, PlacementExplainer.ForPlanned(CreateProfile(user, date), task.Type, RangeOf(block)));

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    private static List<ScheduleBlock> CreateBlocks(DaySchedule schedule, EnergyProfile profile, DayPlan plan)
    {
        var blocks = plan.Tasks
            .Select(p => new ScheduleBlock(schedule.Id, p.Task.TaskId, BlockKind.Task, p.Task.Title,
                p.Range.StartTime, p.Range.EndTime,
                p.IsPart
                    ? PlacementExplainer.ForPart(profile, p.Task.Type, p.Range)
                    : PlacementExplainer.ForPlanned(profile, p.Task.Type, p.Range)))
            .ToList();

        if (plan.Lunch is { } lunch)
        {
            blocks.Add(new ScheduleBlock(schedule.Id, null, BlockKind.Break, LunchTitle,
                lunch.StartTime, lunch.EndTime, PlacementExplainer.LunchExplanation));
        }

        return blocks;
    }

    private async Task<ScheduleDto> ToDtoAsync(User user, DateOnly date, DateOnly today, DaySchedule? schedule, CancellationToken ct)
    {
        var blocks = schedule?.Blocks.OrderBy(b => b.Start).ToList() ?? [];
        var plannedMinutes = MinutesByTask(blocks);

        // В прошедший день уже ничего не планируется: просроченные задачи переходят на сегодня.
        var plannable = date < today ? [] : await tasks.ListPlannableAsync(user.Id, date, date == today, ct);
        var unplaced = plannable
            .Select(t => new UnplacedTaskDto(t.Id, t.Title, t.Type, t.DueDate, t.DurationMinutes - plannedMinutes.GetValueOrDefault(t.Id)))
            .Where(t => t.DurationMinutes > 0)
            .ToList();

        return new ScheduleDto(date, schedule is not null, user.WorkdayStart, user.WorkdayEnd,
            ToBlockDtos(blocks), unplaced, AnalyzeLoad(user, date, blocks));
    }

    private static IReadOnlyList<LoadWarningDto> AnalyzeLoad(User user, DateOnly date, List<ScheduleBlock> blocks)
    {
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype || blocks.Count == 0) return [];

        var profile = new EnergyProfile(birthDate, date, chronotype);
        var items = blocks
            .Where(b => b.Task is { IsCompleted: false })
            .Select(b => new LoadItem(b.Task!.Title, b.Task.Type, RangeOf(b)));
        return LoadAnalyzer.Analyze(profile, items).Select(w => w.ToDto()).ToList();
    }

    private static List<ScheduleBlockDto> ToBlockDtos(List<ScheduleBlock> blocks)
    {
        var partsTotal = blocks.Where(b => b.TaskId is not null).GroupBy(b => b.TaskId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var partsSeen = new Dictionary<Guid, int>();

        return blocks.Select(block =>
        {
            var part = 1;
            if (block.TaskId is { } taskId) partsSeen[taskId] = part = partsSeen.GetValueOrDefault(taskId) + 1;

            return new ScheduleBlockDto(block.Id, block.TaskId, block.Kind, block.Task?.Title ?? block.Title, block.Task?.Type,
                block.Start, block.End, block.Explanation, block.IsPinned, block.Task?.IsCompleted ?? false,
                part, block.TaskId is { } id ? partsTotal[id] : 1);
        }).ToList();
    }

    private static Dictionary<Guid, int> MinutesByTask(IEnumerable<ScheduleBlock> blocks) => blocks
        .Where(b => b.TaskId is not null)
        .GroupBy(b => b.TaskId!.Value)
        .ToDictionary(g => g.Key, g => g.Sum(b => RangeOf(b).Length));

    private static bool IsFixed(ScheduleBlock block) => block.IsPinned || block.Task is { IsCompleted: true };

    private static void EnsureEditable(DateOnly date, DateOnly today)
    {
        if (date < today) throw new ConflictException("Расписание прошедшего дня нельзя изменить.");
    }

    private static TimeRange RangeOf(ScheduleBlock block) => TimeRange.From(block.Start, block.End);

    private static PlannableTask ToPlannable(TaskItem task) =>
        new(task.Id, task.Title, task.Type, task.Priority, task.DurationMinutes);

    private static EnergyProfile CreateProfile(User user, DateOnly date)
    {
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            throw new ConflictException("Укажите дату рождения и хронотип в профиле, чтобы построить расписание.");

        return new EnergyProfile(birthDate, date, chronotype);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
}
