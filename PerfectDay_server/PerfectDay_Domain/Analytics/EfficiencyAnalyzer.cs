using System.Globalization;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Domain.Analytics;

/// <summary>Блок задачи в расписании прошедшего дня.</summary>
public sealed record PlannedBlock(Guid TaskId, TaskType Type, bool IsTaskCompleted, TimeRange Range, bool IsPinned);

public sealed record PlannedDay(DateOnly Date, IReadOnlyList<PlannedBlock> Blocks);

public sealed record DayResult(DateOnly Date, int Planned, int Done, bool IsCritical);

public sealed record TypeResult(TaskType Type, int Planned, int Done);

public enum InsightKind
{
    TimeOfDay,
    Postponed,
    CriticalDays,
    Trend
}

public sealed record Insight(InsightKind Kind, WarningSeverity Severity, string Title, string Text);

public sealed record EfficiencyReport(
    DateOnly From,
    DateOnly To,
    int Planned,
    int Done,
    int PreviousPlanned,
    int PreviousDone,
    int HardDone,
    int HardDoneInPeak,
    int MovedManually,
    TaskType? MostMovedType,
    int CriticalPlanned,
    int CriticalDone,
    IReadOnlyList<DayResult> Days,
    IReadOnlyList<TypeResult> Types,
    IReadOnlyList<Insight> Insights);

/// <summary>
/// План и факт за период. Задача считается запланированной на каждый день, где у неё были блоки,
/// а выполненной — только в последний из них: если её не сделали и она перешла на завтра, вчерашний день остаётся невыполненным.
/// </summary>
public static class EfficiencyAnalyzer
{
    private const int MorningEnd = 13 * 60;
    private const int EveningStart = 16 * 60;
    private const int MinGroupSize = 2;
    private const int NoticeableDifference = 15;
    private const double PostponedShare = 0.3;
    private const int ForecastDays = 30;

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private sealed record Entry(DateOnly Date, Guid TaskId, TaskType Type, TimeRange FirstRange, bool IsDone, bool IsMoved, bool IsCritical);

    /// <param name="schedules">Расписания с блоками и задачами от начала предыдущего периода до сегодняшнего дня.</param>
    public static EfficiencyReport Analyze(DateOnly birthDate, Chronotype chronotype, IEnumerable<PlannedDay> schedules,
        DateOnly from, DateOnly to, DateOnly today)
    {
        var list = schedules.ToList();
        var lastDate = list
            .SelectMany(s => s.Blocks.Select(b => (b.TaskId, s.Date)))
            .GroupBy(x => x.TaskId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Date));

        var entries = list
            .SelectMany(s => s.Blocks
                .GroupBy(b => b.TaskId)
                .Select(g => new Entry(
                    s.Date,
                    g.Key,
                    g.First().Type,
                    g.Select(b => b.Range).MinBy(r => r.Start),
                    g.First().IsTaskCompleted && lastDate[g.Key] == s.Date,
                    g.Any(b => b.IsPinned),
                    BiorhythmCalculator.CriticalCycles(birthDate, s.Date).Count > 0)))
            .ToList();

        var length = to.DayNumber - from.DayNumber + 1;
        var current = entries.Where(e => e.Date >= from && e.Date <= to).ToList();
        var previous = entries.Where(e => e.Date >= from.AddDays(-length) && e.Date < from).ToList();

        var days = Enumerable.Range(0, length)
            .Select(from.AddDays)
            .Select(date =>
            {
                var day = current.Where(e => e.Date == date).ToList();
                return new DayResult(date, day.Count, day.Count(e => e.IsDone), BiorhythmCalculator.CriticalCycles(birthDate, date).Count > 0);
            })
            .ToList();

        var types = Enum.GetValues<TaskType>()
            .Select(type => new TypeResult(type, current.Count(e => e.Type == type), current.Count(e => e.Type == type && e.IsDone)))
            .ToList();

        var hardDone = current.Where(e => e.IsDone && e.Type != TaskType.Routine).ToList();
        var inPeak = hardDone.Count(e =>
            new EnergyProfile(birthDate, e.Date, chronotype).AverageEnergy(EnergyProfile.CycleFor(e.Type)!.Value, e.FirstRange) > 0);

        var moved = current.Where(e => e.IsMoved).ToList();
        TaskType? mostMoved = moved.Count == 0 ? null : moved.GroupBy(e => e.Type).MaxBy(g => g.Count())!.Key;
        var critical = current.Where(e => e.IsCritical).ToList();

        var report = new EfficiencyReport(from, to, current.Count, current.Count(e => e.IsDone),
            previous.Count, previous.Count(e => e.IsDone), hardDone.Count, inPeak, moved.Count, mostMoved,
            critical.Count, critical.Count(e => e.IsDone), days, types, []);

        return report with { Insights = Insights(birthDate, chronotype, current, report, length, today) };
    }

    private static List<Insight> Insights(DateOnly birthDate, Chronotype chronotype, List<Entry> entries,
        EfficiencyReport report, int length, DateOnly today)
    {
        var insights = new List<Insight>();
        if (report.Planned == 0) return insights;

        var forecast = Forecast.Create(birthDate, today, ForecastDays);
        AddTimeOfDay(chronotype, entries, insights);
        AddPostponed(entries, forecast, today, insights);
        AddCriticalDays(report, forecast, today, insights);
        AddTrend(report, length, insights);
        return insights;
    }

    private static void AddTimeOfDay(Chronotype chronotype, List<Entry> entries, List<Insight> insights)
    {
        var hard = entries.Where(e => e.Type != TaskType.Routine).ToList();
        var morning = hard.Where(e => e.FirstRange.Start < MorningEnd).ToList();
        var evening = hard.Where(e => e.FirstRange.Start >= EveningStart).ToList();
        if (morning.Count < MinGroupSize || evening.Count < MinGroupSize) return;

        var morningPercent = Percent(morning.Count(e => e.IsDone), morning.Count);
        var eveningPercent = Percent(evening.Count(e => e.IsDone), evening.Count);
        if (morningPercent - eveningPercent >= NoticeableDifference)
        {
            var advice = chronotype == Chronotype.Owl
                ? "Возможно, вам подойдёт хронотип «голубь»: проверьте его в профиле."
                : "Планировщик уже ставит сложное на утро.";
            insights.Add(new Insight(InsightKind.TimeOfDay, WarningSeverity.Info, "Утро — ваше время",
                $"До 13:00 вы выполняете {morningPercent}% сложных задач, после 16:00 — только {eveningPercent}%. {advice}"));
        }
        else if (eveningPercent - morningPercent >= NoticeableDifference)
        {
            var advice = chronotype == Chronotype.Lark
                ? "Возможно, вам подойдёт хронотип «голубь» или «сова»: проверьте его в профиле."
                : "Планировщик уже сдвигает сложное ближе к вечеру.";
            insights.Add(new Insight(InsightKind.TimeOfDay, WarningSeverity.Info, "Вечер — ваше время",
                $"После 16:00 вы выполняете {eveningPercent}% сложных задач, до 13:00 — только {morningPercent}%. {advice}"));
        }
    }

    private static void AddPostponed(List<Entry> entries, ForecastResult forecast, DateOnly today, List<Insight> insights)
    {
        var worst = entries
            .GroupBy(e => e.Type)
            .Select(g => (Type: g.Key, Moved: g.Count(e => e.IsMoved), Total: g.Count()))
            .Where(x => x.Moved >= MinGroupSize && x.Moved >= x.Total * PostponedShare)
            .OrderByDescending(x => (double)x.Moved / x.Total)
            .FirstOrDefault();
        if (worst.Total == 0) return;

        var text = $"{Percent(worst.Moved, worst.Total)}% {TypeGenitive(worst.Type)} задач перенесены вручную.";
        if (EnergyProfile.CycleFor(worst.Type) is { } cycle &&
            forecast.GoodPeriods.FirstOrDefault(p => p.Cycle == cycle && p.To >= today) is { } period)
        {
            text += $" Попробуйте ставить их на подъём {CycleGenitive(cycle)} цикла: {Range(period.From, period.To)}.";
        }
        else if (worst.Type == TaskType.Routine)
        {
            text += " Возможно, на них уходит больше времени, чем указано: увеличьте длительность.";
        }

        insights.Add(new Insight(InsightKind.Postponed, WarningSeverity.Info, $"{TypePlural(worst.Type)} задачи переносятся", text));
    }

    private static void AddCriticalDays(EfficiencyReport report, ForecastResult forecast, DateOnly today,
        List<Insight> insights)
    {
        if (report.CriticalPlanned == 0) return;

        var worst = report.Days.Where(d => d.IsCritical && d.Planned > 0).MinBy(d => (double)d.Done / d.Planned)!;
        var next = forecast.CriticalDates.FirstOrDefault(c => c.Date > today);
        var percent = Percent(worst.Done, worst.Planned);
        var result = percent switch
        {
            100 => "выполнен весь план",
            0 => "не выполнено ни одной задачи",
            >= 45 and <= 55 => "выполнена половина плана",
            _ => $"выполнено {worst.Done} из {worst.Planned} задач"
        };

        var text = $"В критический день {worst.Date.ToString("d MMMM", Russian)} {result}.";
        var severity = WarningSeverity.Info;
        if (percent < Percent(report.Done, report.Planned))
        {
            text += " В следующий раз примите облегчённое расписание.";
            severity = WarningSeverity.Warning;
        }

        if (next is not null) text += $" Ближайший — {next.Date.ToString("d MMMM", Russian)}.";
        insights.Add(new Insight(InsightKind.CriticalDays, severity, "Критические дни", text));
    }

    private static void AddTrend(EfficiencyReport report, int length, List<Insight> insights)
    {
        if (report.PreviousPlanned == 0) return;

        var now = Percent(report.Done, report.Planned);
        var before = Percent(report.PreviousDone, report.PreviousPlanned);
        var isWeek = length <= 7;
        var (title, severity) = (now - before) switch
        {
            >= 5 => (isWeek ? "Лучше прошлой недели" : "Лучше прошлого месяца", WarningSeverity.Info),
            <= -5 => (isWeek ? "Хуже прошлой недели" : "Хуже прошлого месяца", WarningSeverity.Warning),
            _ => (isWeek ? "Как на прошлой неделе" : "Как в прошлом месяце", WarningSeverity.Info)
        };

        insights.Add(new Insight(InsightKind.Trend, severity, title,
            $"План выполнен на {now}% против {before}% {(isWeek ? "неделей" : "месяцем")} раньше."));
    }

    public static int Percent(int part, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * part / total);

    private static string Range(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("d MMMM", Russian)
        : from.Month == to.Month ? $"{from.Day}–{to.ToString("d MMMM", Russian)}"
        : $"{from.ToString("d MMMM", Russian)} — {to.ToString("d MMMM", Russian)}";

    private static string TypePlural(TaskType type) => type switch
    {
        TaskType.Intellectual => "Интеллектуальные",
        TaskType.Physical => "Физические",
        TaskType.Creative => "Творческие",
        _ => "Рутинные"
    };

    private static string TypeGenitive(TaskType type) => type switch
    {
        TaskType.Intellectual => "интеллектуальных",
        TaskType.Physical => "физических",
        TaskType.Creative => "творческих",
        _ => "рутинных"
    };

    private static string CycleGenitive(BiorhythmCycle cycle) => cycle switch
    {
        BiorhythmCycle.Physical => "физического",
        BiorhythmCycle.Emotional => "эмоционального",
        _ => "интеллектуального"
    };
}
