using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Planning;

public sealed record LoadItem(string Title, TaskType Type, TimeRange Range);

public sealed record LoadWarning(LoadWarningKind Kind, WarningSeverity Severity, string Message);

/// <summary>Ищет перегрузку в расписании и даёт рекомендации на критические дни и спады.</summary>
public static class LoadAnalyzer
{
    private const int MaxHardSeriesMinutes = 180;
    private const int MaxHardMinutesPerDay = 360;
    private const int RestGapMinutes = 15;

    public static IReadOnlyList<LoadWarning> Analyze(EnergyProfile profile, IEnumerable<LoadItem> items)
    {
        var ordered = items.OrderBy(i => i.Range.Start).ToList();
        var hard = ordered.Where(i => i.Type != TaskType.Routine).ToList();

        var warnings = new List<LoadWarning>();
        AddCriticalDay(profile, warnings);
        AddLongSeries(hard, warnings);
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

    private static void AddCriticalDay(EnergyProfile profile, List<LoadWarning> warnings)
    {
        var count = profile.CriticalCycles.Count;
        if (count == 0) return;

        var cycles = string.Join(", ", profile.CriticalCycles.Select(c => PlacementExplainer.CycleName(c).ToLowerInvariant()));
        var message = count switch
        {
            1 => $"Критический день ({cycles} цикл): избегайте рискованных решений и оставьте время на отдых.",
            2 => $"Двойной критический день ({cycles}): значительно снизьте нагрузку и больше отдыхайте.",
            _ => "Тройной критический день: перенесите сложные задачи, откажитесь от рискованных решений и больше отдыхайте."
        };

        warnings.Add(new LoadWarning(LoadWarningKind.CriticalDay,
            count == 1 ? WarningSeverity.Warning : WarningSeverity.Critical, message));
    }

    private static void AddLongSeries(List<LoadItem> hard, List<LoadWarning> warnings)
    {
        var seriesStart = 0;
        var seriesEnd = -1;
        var seriesMinutes = 0;
        var reported = false;

        foreach (var item in hard)
        {
            if (seriesEnd < 0 || item.Range.Start - seriesEnd > RestGapMinutes)
            {
                seriesStart = item.Range.Start;
                seriesMinutes = 0;
                reported = false;
            }

            seriesMinutes += item.Range.Length;
            seriesEnd = item.Range.End;

            if (seriesMinutes <= MaxHardSeriesMinutes || reported) continue;

            reported = true;
            warnings.Add(new LoadWarning(LoadWarningKind.LongHardSeries, WarningSeverity.Warning,
                $"С {TimeRange.FromMinutes(seriesStart):HH\\:mm} идёт длинная серия сложных задач. " +
                "Добавьте перерыв, смените вид деятельности или перенесите часть работы."));
        }
    }

    private static void AddTooMuchHardWork(List<LoadItem> hard, List<LoadWarning> warnings)
    {
        var total = hard.Sum(i => i.Range.Length);
        if (total <= MaxHardMinutesPerDay) return;

        warnings.Add(new LoadWarning(LoadWarningKind.TooMuchHardWork, WarningSeverity.Warning,
            $"Сложных задач больше {MaxHardMinutesPerDay / 60} часов за день. Перенесите часть на более благоприятный день."));
    }

    private static void AddDeclines(EnergyProfile profile, List<LoadItem> hard, List<LoadWarning> warnings)
    {
        foreach (var group in hard.GroupBy(i => EnergyProfile.CycleFor(i.Type)!.Value))
        {
            if (profile.CycleValue(group.Key) >= 0) continue;

            var titles = string.Join(", ", group.Select(i => i.Title).Distinct().Select(title => $"«{title}»"));
            warnings.Add(new LoadWarning(LoadWarningKind.TaskOnCycleDecline, WarningSeverity.Info,
                $"{PlacementExplainer.CycleName(group.Key)} цикл на спаде, больше сил может уйти на: {titles}. " +
                "Если возможно, перенесите это на день с подъёмом цикла."));
        }
    }
}
