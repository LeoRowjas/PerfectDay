using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;

namespace PerfectDay_Domain.Planning;

public sealed record PlannableTask(Guid TaskId, string Title, TaskType Type, TaskPriority Priority, int DurationMinutes);

public sealed record Placement(PlannableTask Task, TimeRange Range, bool IsPart = false);

/// <summary>Ограничения облегчённого дня: лимит сложной работы и обязательные паузы между задачами.</summary>
public sealed record PlanOptions(int? HardMinutesBudget = null, bool RequireBreaks = false)
{
    public static PlanOptions Normal { get; } = new();
}

public sealed record DayPlan(IReadOnlyList<Placement> Tasks, TimeRange? Lunch, IReadOnlyList<PlannableTask> Unplaced);

/// <summary>Расписание на простых правилах: каждая задача получает самое подходящее свободное окно.</summary>
public static class DayPlanner
{
    public const int LunchMinutes = 60;
    public const int LightDayHardMinutes = 120;
    private const int LunchOffsetMinutes = 180;
    private const int MinDayForLunchMinutes = 360;
    private const int StartStepMinutes = 15;
    private const int BufferMinutes = 15;
    private const int MinPartMinutes = 30;

    public static DayPlan Plan(EnergyProfile profile, TimeRange workday, IEnumerable<PlannableTask> tasks) =>
        PlaceAround(profile, workday, tasks, fixedRanges: [], DefaultLunchStart(workday));

    /// <summary>Перестраивает задачи вокруг занятых интервалов. Обед ставится как можно ближе к желаемому времени.</summary>
    public static DayPlan PlaceAround(EnergyProfile profile, TimeRange workday, IEnumerable<PlannableTask> tasks,
        IReadOnlyCollection<TimeRange> fixedRanges, int? lunchTarget, PlanOptions? options = null)
    {
        options ??= PlanOptions.Normal;
        var hardBudget = options.HardMinutesBudget ?? int.MaxValue;
        var occupied = fixedRanges.ToList();

        TimeRange? lunch = lunchTarget is { } target ? FindNearest(workday, occupied, target, LunchMinutes) : null;
        if (lunch is { } lunchRange) occupied.Add(lunchRange);

        var taskRanges = new List<TimeRange>();
        var placements = new List<Placement>();
        var unplaced = new List<PlannableTask>();

        foreach (var task in Order(tasks))
        {
            var isHard = task.Type != TaskType.Routine;
            if (isHard && task.DurationMinutes > hardBudget)
            {
                unplaced.Add(task);
                continue;
            }

            if (isHard) hardBudget -= task.DurationMinutes;

            var range = FindBest(profile, workday, occupied, taskRanges, task, options.RequireBreaks);
            if (range is not null)
            {
                occupied.Add(range.Value);
                taskRanges.Add(range.Value);
                placements.Add(new Placement(task, range.Value));
                continue;
            }

            var parts = SplitAcrossWindows(profile, workday, occupied, task);
            if (parts is null)
            {
                if (isHard) hardBudget += task.DurationMinutes;
                unplaced.Add(task);
                continue;
            }

            occupied.AddRange(parts);
            taskRanges.AddRange(parts);
            placements.AddRange(parts.Select(part => new Placement(task, part, IsPart: true)));
        }

        return new DayPlan(placements.OrderBy(p => p.Range.Start).ToList(), lunch, unplaced);
    }

    public static int? DefaultLunchStart(TimeRange workday) =>
        workday.Length >= MinDayForLunchMinutes ? workday.Start + LunchOffsetMinutes : null;

    /// <summary>Проверяет, что блок можно поставить на новое время, и возвращает его интервал.</summary>
    public static TimeRange ValidateMove(TimeRange workday, TimeOnly newStart, int durationMinutes,
        IEnumerable<TimeRange> pinnedRanges)
    {
        var start = TimeRange.ToMinutes(newStart);
        var range = new TimeRange(start, start + durationMinutes);

        if (range.Start < workday.Start || range.End > workday.End)
            throw new DomainRuleException("Блок должен помещаться в рабочие часы.");

        if (pinnedRanges.Any(range.Overlaps))
            throw new DomainRuleException("На это время уже закреплён другой блок.");

        return range;
    }

    // Рутина занимает то, что осталось после пиков, иначе она заберёт время важных сложных задач.
    // Сложные задачи с низким приоритетом идут после неё: важная рутина для пользователя значит больше.
    private static IEnumerable<PlannableTask> Order(IEnumerable<PlannableTask> tasks) => tasks
        .OrderBy(t => t.Type != TaskType.Routine && t.Priority >= TaskPriority.Medium ? 0 : 1)
        .ThenByDescending(t => t.Priority)
        .ThenByDescending(t => t.DurationMinutes);

    // Сначала ищем окно с небольшим перерывом после соседних задач; если день плотный, ставим вплотную.
    private static TimeRange? FindBest(EnergyProfile profile, TimeRange workday, List<TimeRange> occupied,
        List<TimeRange> taskRanges, PlannableTask task, bool requireBreaks)
    {
        var buffered = occupied.Concat(taskRanges.Select(r => new TimeRange(r.Start - BufferMinutes, r.End + BufferMinutes))).ToList();
        var best = FindBestIn(profile, workday, buffered, task);
        return best is null && !requireBreaks ? FindBestIn(profile, workday, occupied, task) : best;
    }

    private static TimeRange? FindBestIn(EnergyProfile profile, TimeRange workday, List<TimeRange> occupied, PlannableTask task)
    {
        TimeRange? best = null;
        var bestScore = double.MinValue;

        foreach (var candidate in Candidates(workday, task.DurationMinutes))
        {
            if (occupied.Any(candidate.Overlaps)) continue;

            var score = candidate.AverageSuitability(profile, task.Type);
            if (score <= bestScore) continue;

            bestScore = score;
            best = candidate;
        }

        return best;
    }

    // Длинная задача не влезает одним блоком: например, 8 часов работы при обеде посреди дня.
    private static List<TimeRange>? SplitAcrossWindows(EnergyProfile profile, TimeRange workday, List<TimeRange> occupied, PlannableTask task)
    {
        var parts = new List<TimeRange>();
        var remaining = task.DurationMinutes;

        var windows = FreeWindows(workday, occupied)
            .Where(window => window.Length >= MinPartMinutes)
            .OrderByDescending(window => window.AverageSuitability(profile, task.Type));

        foreach (var window in windows)
        {
            if (remaining == 0) break;

            var length = Math.Min(window.Length, remaining);
            var left = remaining - length;
            if (left is > 0 and < MinPartMinutes) length -= MinPartMinutes - left;
            if (length < MinPartMinutes) continue;

            parts.Add(new TimeRange(window.Start, window.Start + length));
            remaining -= length;
        }

        return remaining == 0 ? parts : null;
    }

    private static IEnumerable<TimeRange> FreeWindows(TimeRange workday, List<TimeRange> occupied)
    {
        var cursor = workday.Start;
        foreach (var range in occupied.OrderBy(r => r.Start))
        {
            if (range.Start > cursor) yield return new TimeRange(cursor, Math.Min(range.Start, workday.End));
            cursor = Math.Max(cursor, range.End);
        }

        if (cursor < workday.End) yield return new TimeRange(cursor, workday.End);
    }

    private static TimeRange? FindNearest(TimeRange workday, List<TimeRange> occupied, int target, int duration) =>
        Candidates(workday, duration)
            .Where(candidate => !occupied.Any(candidate.Overlaps))
            .Select(candidate => (TimeRange?)candidate)
            .OrderBy(candidate => Math.Abs(candidate!.Value.Start - target))
            .FirstOrDefault();

    private static IEnumerable<TimeRange> Candidates(TimeRange workday, int duration)
    {
        for (var start = workday.Start; start + duration <= workday.End; start += StartStepMinutes)
            yield return new TimeRange(start, start + duration);
    }
}
