using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Domain.Biorhythms;

/// <summary>Лучшее окно дня для вида деятельности. На спаде цикла окно остаётся, но дело лучше отложить.</summary>
public sealed record Recommendation(
    TaskType Activity,
    string Title,
    TimeOnly Start,
    TimeOnly End,
    string Text,
    int EnergyFrom,
    int EnergyTo,
    bool IsDeclining);

public static class DayRecommendations
{
    private const int WindowMinutes = 120;
    private const int StepMinutes = 15;

    public static IReadOnlyList<Recommendation> Create(EnergyProfile profile, TimeOnly workdayStart, TimeOnly workdayEnd)
    {
        var chronotype = PlacementExplainer.ChronotypeName(profile.Chronotype);
        return new[]
            {
                Build(profile, workdayStart, workdayEnd, TaskType.Intellectual,
                    ("Сложная умственная работа",
                        p => $"Интеллектуальный цикл {Signed(p)}%, а для хронотипа «{chronotype}» это часы наибольшей ясности."),
                    ("Сложную учёбу лучше отложить",
                        p => $"Интеллектуальный цикл на спаде ({Signed(p)}%): лучше повторение, чем новые сложные темы.")),
                Build(profile, workdayStart, workdayEnd, TaskType.Physical,
                    ("Спорт и активные дела",
                        p => $"Физический цикл на подъёме ({Signed(p)}%): хорошее время для тренировки или прогулки."),
                    ("Тяжёлые тренировки лучше отложить",
                        p => $"Физический цикл на спаде ({Signed(p)}%): выберите прогулку или растяжку.")),
                Build(profile, workdayStart, workdayEnd, TaskType.Creative,
                    ("Общение и творчество",
                        p => $"Эмоциональный цикл на подъёме ({Signed(p)}%): подойдут встречи, презентации, творческие задачи."),
                    ("Общение лучше отложить",
                        p => $"Эмоциональный цикл на спаде ({Signed(p)}%). Важные переговоры и презентации лучше перенести.")),
                Build(profile, workdayStart, workdayEnd, TaskType.Routine,
                    ("Рутина и мелкие дела", _ => "После пиков энергия снижается: разберите почту и закройте простые задачи."),
                    ("Рутина и мелкие дела", _ => "После пиков энергия снижается: разберите почту и закройте простые задачи."))
            }
            .OrderBy(r => r.IsDeclining)
            .ThenBy(r => r.Start)
            .ToList();
    }

    private static Recommendation Build(EnergyProfile profile, TimeOnly workdayStart, TimeOnly workdayEnd, TaskType activity,
        (string Title, Func<int, string> Text) rising, (string Title, Func<int, string> Text) declining)
    {
        var dayStart = TimeRange.ToMinutes(workdayStart);
        var dayEnd = TimeRange.ToMinutes(workdayEnd);
        var length = Math.Min(WindowMinutes, dayEnd - dayStart);

        var bestStart = dayStart;
        var bestScore = double.MinValue;
        for (var start = dayStart; start + length <= dayEnd; start += StepMinutes)
        {
            var score = new TimeRange(start, start + length).AverageSuitability(profile, activity);
            if (score <= bestScore) continue;
            bestScore = score;
            bestStart = start;
        }

        var window = new TimeRange(bestStart, bestStart + length);
        var cycle = EnergyProfile.CycleFor(activity);
        var percent = cycle is null ? 0 : BiorhythmCalculator.Percent(profile.CycleValue(cycle.Value));
        var isDeclining = cycle is not null && percent < 0;
        var (from, to) = EnergyRange(profile, cycle, window);
        var (title, text) = isDeclining ? declining : rising;

        return new Recommendation(activity, title, window.StartTime, window.EndTime, text(percent), from, to, isDeclining);
    }

    // Для рутины берётся самый сильный из циклов: показывает, насколько энергии в эти часы мало.
    private static (int From, int To) EnergyRange(EnergyProfile profile, BiorhythmCycle? cycle, TimeRange window)
    {
        var values = new List<int>();
        for (var minute = window.Start; minute <= window.End; minute += StepMinutes)
        {
            var time = TimeRange.FromMinutes(Math.Min(minute, 24 * 60 - 1));
            var energy = cycle is { } c
                ? profile.Energy(c, time)
                : BiorhythmCalculator.Cycles.Max(any => profile.Energy(any, time));
            values.Add(BiorhythmCalculator.Percent(energy));
        }

        return (values.Min(), values.Max());
    }

    private static string Signed(int percent) => percent > 0 ? $"+{percent}" : percent < 0 ? $"−{-percent}" : "0";
}
