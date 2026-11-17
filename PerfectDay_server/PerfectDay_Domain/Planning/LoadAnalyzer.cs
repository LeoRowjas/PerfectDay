using System.Globalization;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Planning;

public sealed record LoadItem(Guid? TaskId, string Title, TaskType Type, TimeRange Range);

/// <summary>Исправление, которое клиент может применить одной кнопкой.</summary>
public sealed record LoadAction(
    LoadActionKind Kind,
    int? Start = null,
    int? DurationMinutes = null,
    Guid? TaskId = null,
    DateOnly? TargetDate = null);

/// <summary>Id стабилен между запросами: по нему пользователь скрывает предупреждение.</summary>
public sealed record LoadWarning(
    string Id,
    LoadWarningKind Kind,
    WarningSeverity Severity,
    string Title,
    string Message,
    LoadAction? Action = null);

public sealed record LoadSummary(
    int HardMinutes,
    int WorkdayMinutes,
    int LongestSeriesMinutes,
    int LongestSeriesCount,
    int RestMinutes,
    bool IsOverloaded);

/// <summary>Ищет перегрузку в расписании и даёт рекомендации на критические дни и спады.</summary>
public static class LoadAnalyzer
{
    public const int SuggestedBreakMinutes = 15;
    public const int SuggestedWalkMinutes = 20;

    private const int MaxHardSeriesMinutes = 180;
    private const int MaxHardMinutesPerDay = 360;
    private const int RestGapMinutes = 15;
    private const int BreakAfterMinutes = 90;
    private const int SwitchActivityMinutes = 120;
    private const int LookAheadDays = 14;

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<LoadWarning> Analyze(EnergyProfile profile, IEnumerable<LoadItem> items)
    {
        var ordered = items.OrderBy(i => i.Range.Start).ToList();
        var hard = ordered.Where(i => i.Type != TaskType.Routine).ToList();
        var series = Series(hard);

        var warnings = new List<LoadWarning>();
        AddCriticalDay(profile, warnings);
        AddLongSeries(series, warnings);
        AddSwitchActivity(series, warnings);
        AddTooMuchHardWork(hard, warnings);
        AddDeclines(profile, hard, warnings);
        return warnings;
    }

    public static IReadOnlyList<LoadWarning> CriticalDayWarnings(EnergyProfile profile)
    {
        var warnings = new List<LoadWarning>();
        AddCriticalDay(profile, warnings);
        return warnings;
    }

    public static LoadSummary Summarize(IEnumerable<LoadItem> items, IEnumerable<TimeRange> rests, TimeRange workday,
        IEnumerable<LoadWarning> warnings)
    {
        var hard = items.Where(i => i.Type != TaskType.Routine).OrderBy(i => i.Range.Start).ToList();
        var longest = Series(hard).MaxBy(s => s.Sum(i => i.Range.Length)) ?? [];
        var isOverloaded = warnings.Any(w => w.Kind is LoadWarningKind.LongHardSeries or LoadWarningKind.TooMuchHardWork);

        return new LoadSummary(
            hard.Sum(i => i.Range.Length),
            workday.Length,
            longest.Sum(i => i.Range.Length),
            longest.Select(i => i.TaskId).Distinct().Count(),
            rests.Sum(r => r.Length),
            isOverloaded);
    }

    // Серия: сложные задачи, между которыми меньше 15 минут отдыха.
    private static List<List<LoadItem>> Series(List<LoadItem> hard)
    {
        var result = new List<List<LoadItem>>();
        foreach (var item in hard)
        {
            var current = result.LastOrDefault();
            if (current is null || item.Range.Start - current[^1].Range.End >= RestGapMinutes)
                result.Add([item]);
            else
                current.Add(item);
        }

        return result;
    }

    private static void AddCriticalDay(EnergyProfile profile, List<LoadWarning> warnings)
    {
        var count = profile.CriticalCycles.Count;
        if (count == 0) return;

        var cycles = string.Join(", ", profile.CriticalCycles.Select(c => PlacementExplainer.CycleName(c).ToLowerInvariant()));
        var (title, message) = count switch
        {
            1 => ("Критический день",
                $"Критический день ({cycles} цикл): избегайте рискованных решений и оставьте время на отдых."),
            2 => ("Двойной критический день",
                $"Двойной критический день ({cycles}): значительно снизьте нагрузку и больше отдыхайте."),
            _ => ("Тройной критический день",
                "Тройной критический день: перенесите сложные задачи, откажитесь от рискованных решений и больше отдыхайте.")
        };

        warnings.Add(new LoadWarning("critical", LoadWarningKind.CriticalDay,
            count == 1 ? WarningSeverity.Warning : WarningSeverity.Critical, title, message,
            new LoadAction(LoadActionKind.Lighten)));
    }

    private static void AddLongSeries(List<List<LoadItem>> series, List<LoadWarning> warnings)
    {
        foreach (var items in series)
        {
            var minutes = items.Sum(i => i.Range.Length);
            if (minutes < MaxHardSeriesMinutes) continue;

            var start = items[0].Range.Start;
            var end = items[^1].Range.End;
            var count = items.Select(i => i.TaskId).Distinct().Count();
            var what = count == 1 ? "сложная работа идёт" : $"{CountWords(count)} идут";

            warnings.Add(new LoadWarning($"series:{start}", LoadWarningKind.LongHardSeries, WarningSeverity.Warning,
                "Длинная серия сложных задач",
                $"С {Time(start)} до {Time(end)} {what} без паузы. Добавьте {SuggestedBreakMinutes} минут отдыха, " +
                "чтобы сохранить концентрацию.",
                new LoadAction(LoadActionKind.AddBreak, BreakStart(items), SuggestedBreakMinutes)));
        }
    }

    // Перерыв ставится на стыке задач ближе к полутора часам работы, а если задача одна — внутри неё.
    private static int BreakStart(List<LoadItem> items)
    {
        var worked = 0;
        for (var i = 0; i < items.Count - 1; i++)
        {
            worked += items[i].Range.Length;
            if (worked >= BreakAfterMinutes) return items[i].Range.End;
        }

        return items.Count > 1 ? items[^2].Range.End : items[0].Range.Start + BreakAfterMinutes;
    }

    private static void AddSwitchActivity(List<List<LoadItem>> series, List<LoadWarning> warnings)
    {
        foreach (var items in series.Where(s => s.Sum(i => i.Range.Length) < MaxHardSeriesMinutes))
        {
            for (var i = 0; i < items.Count - 1; i++)
            {
                var (first, second) = (items[i], items[i + 1]);
                if (first.Type != second.Type || first.TaskId == second.TaskId) continue;
                if (first.Range.Length + second.Range.Length < SwitchActivityMinutes) continue;

                warnings.Add(new LoadWarning($"switch:{first.Range.End}", LoadWarningKind.SwitchActivity, WarningSeverity.Info,
                    "Смените вид деятельности",
                    $"«{first.Title}» и «{second.Title}» идут подряд и нагружают один цикл. " +
                    $"Прогулка {SuggestedWalkMinutes} минут в {Time(first.Range.End)} восстановит концентрацию.",
                    new LoadAction(LoadActionKind.InsertWalk, first.Range.End, SuggestedWalkMinutes)));
                break;
            }
        }
    }

    private static void AddTooMuchHardWork(List<LoadItem> hard, List<LoadWarning> warnings)
    {
        var total = hard.Sum(i => i.Range.Length);
        if (total <= MaxHardMinutesPerDay) return;

        warnings.Add(new LoadWarning("too-much", LoadWarningKind.TooMuchHardWork, WarningSeverity.Warning,
            "Слишком много сложной работы",
            $"Сложных задач больше {MaxHardMinutesPerDay / 60} часов за день. Перенесите часть на более благоприятный день."));
    }

    private static void AddDeclines(EnergyProfile profile, List<LoadItem> hard, List<LoadWarning> warnings)
    {
        foreach (var task in hard.GroupBy(i => i.TaskId))
        {
            var first = task.First();
            var cycle = EnergyProfile.CycleFor(first.Type)!.Value;
            var value = profile.CycleValue(cycle);
            if (value >= 0) continue;

            var target = BestDayAhead(profile, cycle);
            var advice = target is { } date
                ? $"Лучше перенести на {date.ToString("d MMMM", Russian)}: цикл будет на подъёме."
                : "Если возможно, перенесите задачу на день с подъёмом цикла.";

            warnings.Add(new LoadWarning($"decline:{task.Key}", LoadWarningKind.TaskOnCycleDecline, WarningSeverity.Info,
                $"{TypeName(first.Type)} задача на спаде цикла",
                $"«{first.Title}» стоит на {Time(first.Range.Start)}, а {PlacementExplainer.CycleName(cycle).ToLowerInvariant()} " +
                $"цикл {Signed(BiorhythmCalculator.Percent(value))}%. {advice}",
                target is null || task.Key is null ? null : new LoadAction(LoadActionKind.MoveTask, TaskId: task.Key, TargetDate: target)));
        }
    }

    /// <summary>Ближайшие две недели: день с самым высоким значением цикла, если цикл там на подъёме.</summary>
    public static DateOnly? BestDayAhead(EnergyProfile profile, BiorhythmCycle cycle)
    {
        var best = Enumerable.Range(1, LookAheadDays)
            .Select(offset => profile.Date.AddDays(offset))
            .MaxBy(day => BiorhythmCalculator.Value(profile.BirthDate, day, cycle));

        return BiorhythmCalculator.Value(profile.BirthDate, best, cycle) > 0 ? best : null;
    }

    private static string CountWords(int count) => count switch
    {
        2 => "две сложные задачи",
        3 => "три сложные задачи",
        4 => "четыре сложные задачи",
        _ => $"{count} сложных задач"
    };

    private static string TypeName(TaskType type) => type switch
    {
        TaskType.Intellectual => "Интеллектуальная",
        TaskType.Physical => "Физическая",
        TaskType.Creative => "Творческая",
        _ => "Рутинная"
    };

    private static string Signed(int percent) => percent > 0 ? $"+{percent}" : percent < 0 ? $"−{-percent}" : "0";

    private static string Time(int minutes) => TimeRange.FromMinutes(minutes).ToString("HH\\:mm", CultureInfo.InvariantCulture);
}
