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
    ICalendarRepository calendars,
    TaskSeriesGenerator generator,
    IUnitOfWork unitOfWork)
{
    private const string LunchTitle = "Обед и отдых";
    private const int WorseEnergyThreshold = 10;

    private static readonly Dictionary<BreakKind, (string Title, string Explanation)> Breaks = new()
    {
        [BreakKind.Rest] = ("Перерыв",
            "Короткий перерыв после длинной серии сложных задач: встаньте, пройдитесь, отвлекитесь от экрана."),
        [BreakKind.Walk] = ("Прогулка",
            "Смена деятельности между похожими сложными задачами восстанавливает концентрацию.")
    };

    public async Task<ScheduleDto> GetAsync(Guid userId, DateOnly date, DateOnly today, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (await EnsureOccurrencesAsync(userId, date, today, ct)) await unitOfWork.SaveChangesAsync(ct);
        var schedule = await schedules.GetAsync(userId, date, ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Заново раскладывает задачи дня. Закреплённые и выполненные блоки остаются на месте.</summary>
    public async Task<ScheduleDto> BuildAsync(Guid userId, DateOnly date, DateOnly today, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        await EnsureOccurrencesAsync(userId, date, today, ct);
        var schedule = await GetOrCreateAsync(userId, date, ct);

        await RebuildAsync(user, schedule, date, today, ct);
        user.MarkOnboarded();
        await unitOfWork.SaveChangesAsync(ct);

        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Облегчённый день: не больше двух часов сложной работы и обязательные паузы между задачами.</summary>
    public async Task<ScheduleDto> SetLightenedAsync(Guid userId, DateOnly date, DateOnly today, bool isLightened, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var schedule = await GetOrCreateAsync(userId, date, ct);

        schedule.SetLightened(isLightened);
        await RebuildAsync(user, schedule, date, today, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Закрепляет блок на новом времени и перестраивает незакреплённую часть дня вокруг него.</summary>
    public async Task<ScheduleDto> MoveBlockAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, TimeOnly newStart, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var profile = CreateProfile(user, date);
        var schedule = await GetExistingAsync(userId, date, ct);
        var moved = FindMovableBlock(schedule, blockId);

        var oldRange = RangeOf(moved);
        var others = schedule.Blocks.Where(b => b.Id != blockId).ToList();
        var newRange = DayPlanner.ValidateMove(Workday(user), newStart, oldRange.Length, others.Where(IsFixed).Select(RangeOf));
        var rearrangement = Rearrange(user, profile, schedule, others, newRange, Math.Min(oldRange.Start, newRange.Start));

        Apply(schedule, profile, rearrangement);
        var explanation = moved.Task is { } movedTask
            ? PlacementExplainer.ForManualMove(profile, movedTask.Type, newRange)
            : moved.Explanation;
        moved.Reschedule(newRange.StartTime, newRange.EndTime, explanation);
        moved.Pin();

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>То же, что перенос, но ничего не сохраняет: показывает энергию на новом месте и сдвиги остальных блоков.</summary>
    public async Task<MovePreviewDto> PreviewMoveAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, TimeOnly newStart, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var profile = CreateProfile(user, date);
        var schedule = await GetExistingAsync(userId, date, ct);
        var moved = FindMovableBlock(schedule, blockId);

        var oldRange = RangeOf(moved);
        var others = schedule.Blocks.Where(b => b.Id != blockId).ToList();
        var newRange = DayPlanner.ValidateMove(Workday(user), newStart, oldRange.Length, others.Where(IsFixed).Select(RangeOf));
        var cutoff = Math.Min(oldRange.Start, newRange.Start);
        var rearrangement = Rearrange(user, profile, schedule, others, newRange, cutoff);

        var changes = new List<BlockChangeDto> { new(TitleOf(moved), oldRange.StartTime, newRange.StartTime) };
        changes.AddRange(rearrangement.Kept
            .Where(b => b.Kind == BlockKind.Task && RangeOf(b).Start >= cutoff)
            .Select(b => new BlockChangeDto(TitleOf(b), b.Start, b.Start)));
        changes.AddRange(rearrangement.Replanned
            .Where(b => b.Kind == BlockKind.Task)
            .GroupBy(b => b.TaskId!.Value)
            .Select(group => (Old: group.Min(b => b.Start), New: rearrangement.Plan.Tasks.Where(p => p.Task.TaskId == group.Key).ToList(), Title: TitleOf(group.First())))
            .Where(x => x.New.Count > 0)
            .Select(x => new BlockChangeDto(x.Title, x.Old, x.New.Min(p => p.Range.StartTime))));

        int? newEnergy = null, oldEnergy = null;
        string? warning = null;
        if (moved.Task is { } task && EnergyProfile.CycleFor(task.Type) is { } cycle)
        {
            newEnergy = BiorhythmCalculator.Percent(profile.AverageEnergy(cycle, newRange));
            oldEnergy = BiorhythmCalculator.Percent(profile.AverageEnergy(cycle, oldRange));
            if (newEnergy < oldEnergy - WorseEnergyThreshold)
            {
                warning = $"{EnergyName(cycle)} энергия ниже: {newEnergy}% в {newRange.StartTime:HH\\:mm} против {oldEnergy}% " +
                          $"в {oldRange.StartTime:HH\\:mm}. Перенести можно, но эффект от задачи будет меньше.";
            }
        }

        return new MovePreviewDto(newRange.StartTime, newRange.EndTime, newEnergy, oldEnergy, warning,
            changes.OrderBy(c => c.To).ToList());
    }

    public async Task<ScheduleDto> UnpinBlockAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var schedule = await GetExistingAsync(userId, date, ct);
        var block = FindMovableBlock(schedule, blockId);

        block.Unpin();
        if (block.Task is { } task)
            block.Reschedule(block.Start, block.End, PlacementExplainer.ForPlanned(CreateProfile(user, date), task.Type, RangeOf(block)));

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Вставляет закреплённый перерыв или прогулку; задачи после него сдвигаются.</summary>
    public async Task<ScheduleDto> AddBreakAsync(Guid userId, DateOnly date, DateOnly today, AddBreakRequest request, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var profile = CreateProfile(user, date);
        var schedule = await GetExistingAsync(userId, date, ct);

        var blocks = schedule.Blocks.ToList();
        var range = DayPlanner.ValidateMove(Workday(user), request.Start!.Value, request.DurationMinutes!.Value,
            blocks.Where(IsFixed).Select(RangeOf));

        // Обычно хватает сдвинуть следующие блоки; если день не вмещает сдвиг, остаток раскладывается заново.
        if (!TryPushLater(user, profile, blocks, range))
            Apply(schedule, profile, Rearrange(user, profile, schedule, blocks, range, range.Start));

        var (title, explanation) = Breaks[request.Kind!.Value];
        var pause = new ScheduleBlock(schedule.Id, null, BlockKind.Break, title, range.StartTime, range.EndTime, explanation);
        pause.Pin();
        schedule.AddBlocks([pause]);

        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Убирает перерыв. Освободившееся время займут задачи при следующей перестройке.</summary>
    public async Task<ScheduleDto> RemoveBreakAsync(Guid userId, DateOnly date, DateOnly today, Guid blockId, CancellationToken ct)
    {
        EnsureEditable(date, today);
        var user = await GetUserAsync(userId, ct);
        var schedule = await GetExistingAsync(userId, date, ct);
        var block = FindBlock(schedule, blockId);
        if (block.Kind != BlockKind.Break) throw new ConflictException("Удалить можно только перерыв. Задачу удаляют в списке задач.");

        schedule.RemoveBlock(block);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>Пользователь решил оставить как есть: предупреждение больше не показывается в этот день.</summary>
    public async Task<ScheduleDto> DismissWarningAsync(Guid userId, DateOnly date, DateOnly today, string warningId, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        var schedule = await GetExistingAsync(userId, date, ct);

        schedule.DismissWarning(warningId);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(user, date, today, schedule, ct);
    }

    /// <summary>
    /// Календари синхронизировались: в построенных расписаниях с сегодняшнего дня события заменяются новыми,
    /// а если они изменились, день раскладывается заново.
    /// </summary>
    public async Task RefreshCalendarEventsAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is null || user.Chronotype is null) return;

        foreach (var schedule in await schedules.ListRangeAsync(userId, today, today.AddDays(TaskSeriesGenerator.MaxAheadDays), ct))
        {
            var current = schedule.Blocks.Where(b => b.Kind == BlockKind.Event).Select(Signature).Order();
            var expected = (await EventBlocksAsync(user, schedule, ct)).Select(Signature).Order();
            if (!current.SequenceEqual(expected)) await RebuildAsync(user, schedule, schedule.Date, today, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string Signature(ScheduleBlock block) => $"{block.Title}|{block.Start}|{block.End}";

    /// <summary>События календарей, которые занимают время в этот день, в виде закреплённых блоков.</summary>
    private async Task<List<ScheduleBlock>> EventBlocksAsync(User user, DaySchedule schedule, CancellationToken ct)
    {
        var connections = (await calendars.ListConnectionsAsync(user.Id, ct)).ToDictionary(c => c.Id);
        if (connections.Count == 0) return [];

        var workday = Workday(user);
        var blocks = new List<ScheduleBlock>();
        foreach (var item in await calendars.ListEventsAsync(user.Id, schedule.Date, schedule.Date, ct))
        {
            if (!connections.TryGetValue(item.ConnectionId, out var connection) || !CalendarRules.IsBusy(item, connection)) continue;

            var range = TimeRange.From(item.Start, item.End);
            var clipped = new TimeRange(Math.Max(range.Start, workday.Start), Math.Min(range.End, workday.End));
            if (clipped.Length <= 0) continue;

            var block = new ScheduleBlock(schedule.Id, null, BlockKind.Event, item.Title, clipped.StartTime, clipped.EndTime,
                $"Событие из календаря {CalendarRules.ProviderName(connection.Provider)}. Время занято, задачи стоят вокруг него.");
            block.Pin();
            blocks.Add(block);
        }

        return blocks;
    }

    private async Task<bool> EnsureOccurrencesAsync(Guid userId, DateOnly date, DateOnly today, CancellationToken ct)
    {
        if (date < today || date > today.AddDays(TaskSeriesGenerator.MaxAheadDays)) return false;
        var horizon = TaskSeriesGenerator.Horizon(today);
        return await generator.EnsureAsync(userId, date > horizon ? date : horizon, ct);
    }

    /// <summary>Сдвигает незакреплённые блоки, которые мешают вставке, на время после неё, не меняя их порядок.</summary>
    private static bool TryPushLater(User user, EnergyProfile profile, List<ScheduleBlock> blocks, TimeRange inserted)
    {
        var workday = Workday(user);
        var occupied = blocks.Where(IsFixed).Select(RangeOf).Append(inserted).ToList();
        var moving = blocks
            .Where(b => !IsFixed(b) && RangeOf(b).End > inserted.Start)
            .OrderBy(b => b.Start)
            .ToList();

        var shifted = new List<(ScheduleBlock Block, TimeRange Range)>();
        var cursor = inserted.End;
        foreach (var block in moving)
        {
            var length = RangeOf(block).Length;
            var start = Math.Max(RangeOf(block).Start, cursor);
            while (occupied.FirstOrDefault(r => r.Overlaps(new TimeRange(start, start + length))) is { Length: > 0 } busy)
                start = busy.End;

            var range = new TimeRange(start, start + length);
            if (range.End > workday.End) return false;

            occupied.Add(range);
            shifted.Add((block, range));
            cursor = range.End;
        }

        foreach (var (block, range) in shifted.Where(x => x.Range != RangeOf(x.Block)))
        {
            var explanation = block.Task is { } task ? PlacementExplainer.ForPlanned(profile, task.Type, range) : block.Explanation;
            block.Reschedule(range.StartTime, range.EndTime, explanation);
        }

        return true;
    }

    private sealed record Rearrangement(List<ScheduleBlock> Kept, List<ScheduleBlock> Replanned, DayPlan Plan);

    // Всё, что стоит раньше затронутого интервала и не мешает новому блоку, остаётся на месте; остальное раскладывается заново.
    private static Rearrangement Rearrange(User user, EnergyProfile profile, DaySchedule schedule, List<ScheduleBlock> others,
        TimeRange newRange, int cutoff)
    {
        var kept = others.Where(b => IsFixed(b) || (RangeOf(b).Start < cutoff && !RangeOf(b).Overlaps(newRange))).ToList();
        var replanned = others.Except(kept).ToList();

        var lunchBlock = replanned.FirstOrDefault(b => b.Kind == BlockKind.Break && !b.IsPinned);
        int? lunchTarget = lunchBlock is null ? null : TimeRange.ToMinutes(lunchBlock.Start);
        var replannedTasks = replanned
            .Where(b => b.Kind == BlockKind.Task)
            .GroupBy(b => b.Task!)
            .Select(group => ToPlannable(group.Key) with { DurationMinutes = group.Sum(b => RangeOf(b).Length) });

        var fixedRanges = kept.Select(RangeOf).Append(newRange).ToList();
        var plan = DayPlanner.PlaceAround(profile, Workday(user), replannedTasks, fixedRanges, lunchTarget,
            Options(schedule, kept));
        return new Rearrangement(kept, replanned, plan);
    }

    private static void Apply(DaySchedule schedule, EnergyProfile profile, Rearrangement rearrangement)
    {
        foreach (var block in rearrangement.Replanned) schedule.RemoveBlock(block);
        schedule.AddBlocks(CreateBlocks(schedule, profile, rearrangement.Plan));
    }

    private async Task RebuildAsync(User user, DaySchedule schedule, DateOnly date, DateOnly today, CancellationToken ct)
    {
        var profile = CreateProfile(user, date);
        var workday = Workday(user);

        // События календаря каждый раз берутся заново: они могли измениться после синхронизации.
        var kept = schedule.Blocks
            .Where(b => IsFixed(b) && b.Kind != BlockKind.Event && RangeOf(b).Start >= workday.Start && RangeOf(b).End <= workday.End)
            .ToList();
        var keptMinutes = MinutesByTask(kept);
        var events = await EventBlocksAsync(user, schedule, ct);

        var plannable = (await tasks.ListPlannableAsync(user.Id, date, date == today, ct))
            .Select(task => ToPlannable(task) with { DurationMinutes = task.DurationMinutes - keptMinutes.GetValueOrDefault(task.Id) })
            .Where(task => task.DurationMinutes > 0);

        var lunchTarget = kept.Any(b => b.Kind == BlockKind.Break && b.Title == LunchTitle) ? null : DayPlanner.DefaultLunchStart(workday);
        var plan = DayPlanner.PlaceAround(profile, workday, plannable, kept.Concat(events).Select(RangeOf).ToList(), lunchTarget,
            Options(schedule, kept));

        schedule.ReplaceBlocks(kept.Concat(events).Concat(CreateBlocks(schedule, profile, plan)).ToList());
    }

    private static PlanOptions Options(DaySchedule schedule, IEnumerable<ScheduleBlock> fixedBlocks)
    {
        if (!schedule.IsLightened) return PlanOptions.Normal;

        var fixedHard = fixedBlocks.Where(b => b.Task is { Type: not TaskType.Routine }).Sum(b => RangeOf(b).Length);
        return new PlanOptions(Math.Max(0, DayPlanner.LightDayHardMinutes - fixedHard), RequireBreaks: true);
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

        var profile = user.BirthDate is { } birthDate && user.Chronotype is { } chronotype
            ? new EnergyProfile(birthDate, date, chronotype)
            : null;
        var items = blocks
            .Where(b => b.Task is { IsCompleted: false })
            .Select(b => new LoadItem(b.TaskId, b.Task!.Title, b.Task.Type, RangeOf(b)))
            .ToList();

        var warnings = profile is null || blocks.Count == 0 ? [] : LoadAnalyzer.Analyze(profile, items);
        var summary = LoadAnalyzer.Summarize(items, blocks.Where(b => b.Kind == BlockKind.Break).Select(RangeOf), Workday(user), warnings);
        var isLightened = schedule?.IsLightened ?? false;
        var visible = warnings
            .Where(w => schedule is null || !schedule.DismissedWarnings.Contains(w.Id))
            // Облегчение уже применено: кнопка «Облегчить» больше не нужна.
            .Select(w => isLightened && w.Action?.Kind == LoadActionKind.Lighten ? w with { Action = null } : w)
            .Select(w => w.ToDto())
            .ToList();

        return new ScheduleDto(date, schedule is not null, isLightened, user.WorkdayStart, user.WorkdayEnd,
            ToBlockDtos(blocks, profile), unplaced, visible,
            new LoadSummaryDto(summary.HardMinutes, summary.WorkdayMinutes, summary.LongestSeriesMinutes,
                summary.LongestSeriesCount, summary.RestMinutes, summary.IsOverloaded));
    }

    private static List<ScheduleBlockDto> ToBlockDtos(List<ScheduleBlock> blocks, EnergyProfile? profile)
    {
        var partsTotal = blocks.Where(b => b.TaskId is not null).GroupBy(b => b.TaskId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var partsSeen = new Dictionary<Guid, int>();

        return blocks.Select(block =>
        {
            var part = 1;
            if (block.TaskId is { } taskId) partsSeen[taskId] = part = partsSeen.GetValueOrDefault(taskId) + 1;

            int? energy = profile is not null && block.Task is { } task && EnergyProfile.CycleFor(task.Type) is { } cycle
                ? BiorhythmCalculator.Percent(profile.AverageEnergy(cycle, RangeOf(block)))
                : null;

            return new ScheduleBlockDto(block.Id, block.TaskId, block.Kind, TitleOf(block), block.Task?.Type, block.Task?.Priority,
                block.Start, block.End, block.Explanation, energy, block.IsPinned, block.Task?.IsCompleted ?? false,
                part, block.TaskId is { } id ? partsTotal[id] : 1);
        }).ToList();
    }

    private static Dictionary<Guid, int> MinutesByTask(IEnumerable<ScheduleBlock> blocks) => blocks
        .Where(b => b.TaskId is not null)
        .GroupBy(b => b.TaskId!.Value)
        .ToDictionary(g => g.Key, g => g.Sum(b => RangeOf(b).Length));

    private static bool IsFixed(ScheduleBlock block) =>
        block.IsPinned || block.Kind == BlockKind.Event || block.Task is { IsCompleted: true };

    private static void EnsureEditable(DateOnly date, DateOnly today)
    {
        if (date < today) throw new ConflictException("Расписание прошедшего дня нельзя изменить.");
    }

    private static string EnergyName(BiorhythmCycle cycle) => cycle switch
    {
        BiorhythmCycle.Physical => "Физическая",
        BiorhythmCycle.Emotional => "Эмоциональная",
        _ => "Умственная"
    };

    private static string TitleOf(ScheduleBlock block) => block.Task?.Title ?? block.Title;

    private static TimeRange RangeOf(ScheduleBlock block) => TimeRange.From(block.Start, block.End);

    private static TimeRange Workday(User user) => TimeRange.From(user.WorkdayStart, user.WorkdayEnd);

    private static PlannableTask ToPlannable(TaskItem task) =>
        new(task.Id, task.Title, task.Type, task.Priority, task.DurationMinutes);

    private static ScheduleBlock FindBlock(DaySchedule schedule, Guid blockId) =>
        schedule.Blocks.FirstOrDefault(b => b.Id == blockId)
        ?? throw new NotFoundException("Этот блок уже изменился. Расписание обновлено, выберите его снова.");

    private static ScheduleBlock FindMovableBlock(DaySchedule schedule, Guid blockId)
    {
        var block = FindBlock(schedule, blockId);
        if (block.Kind == BlockKind.Event)
            throw new ConflictException("Это событие из календаря: его время меняется в самом календаре.");
        return block;
    }

    private static EnergyProfile CreateProfile(User user, DateOnly date)
    {
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            throw new ConflictException("Укажите дату рождения и хронотип в профиле, чтобы построить расписание.");

        return new EnergyProfile(birthDate, date, chronotype);
    }

    private async Task<DaySchedule> GetOrCreateAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var schedule = await schedules.GetAsync(userId, date, ct);
        if (schedule is not null) return schedule;

        schedule = new DaySchedule(userId, date);
        schedules.Add(schedule);
        return schedule;
    }

    private async Task<DaySchedule> GetExistingAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        await schedules.GetAsync(userId, date, ct) ?? throw new NotFoundException("Расписание на эту дату ещё не построено.");

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
}
