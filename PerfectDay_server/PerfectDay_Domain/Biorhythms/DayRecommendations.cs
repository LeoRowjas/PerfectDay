using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Domain.Biorhythms;

public sealed record Recommendation(TaskType Activity, string Title, TimeOnly Start, TimeOnly End, string Text);

public static class DayRecommendations
{
    private const int WindowMinutes = 120;
    private const int StepMinutes = 15;

    public static IReadOnlyList<Recommendation> Create(EnergyProfile profile, TimeOnly workdayStart, TimeOnly workdayEnd)
    {
        return
        [
            Build(profile, workdayStart, workdayEnd, TaskType.Intellectual, "Сложная работа и учёба",
                "Ясность ума на подъёме: возьмите анализ, отчёты, новый материал.",
                "Интеллектуальный цикл на спаде: лучше повторение, чем новые сложные темы."),
            Build(profile, workdayStart, workdayEnd, TaskType.Physical, "Спорт и активность",
                "Тело готово к нагрузке: хорошее время для тренировки.",
                "Физический цикл на спаде: выберите прогулку вместо тяжёлой тренировки."),
            Build(profile, workdayStart, workdayEnd, TaskType.Creative, "Общение и творчество",
                "Настроение на подъёме: подойдут встречи, презентации, творческие задачи.",
                "Эмоциональный цикл на спаде: лучше работать в тишине и не назначать важных переговоров."),
            Build(profile, workdayStart, workdayEnd, TaskType.Routine, "Рутина и отдых",
                "Энергии меньше всего: письма, административные дела и короткий отдых.",
                "Энергии меньше всего: письма, административные дела и короткий отдых.")
        ];
    }

    private static Recommendation Build(EnergyProfile profile, TimeOnly workdayStart, TimeOnly workdayEnd,
        TaskType activity, string title, string risingText, string decliningText)
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

        var cycle = EnergyProfile.CycleFor(activity);
        var rising = cycle is null || profile.CycleValue(cycle.Value) >= 0;
        var window = new TimeRange(bestStart, bestStart + length);
        return new Recommendation(activity, title, window.StartTime, window.EndTime, rising ? risingText : decliningText);
    }
}
