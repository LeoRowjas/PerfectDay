using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Planning;

/// <summary>Формирует текст «почему задача стоит в это время».</summary>
public static class PlacementExplainer
{
    private const double GoodCircadianLevel = 0.5;

    public const string LunchExplanation =
        "Обед и короткий отдых в середине рабочего дня помогают сохранить силы на вторую половину.";

    public static string ForPlanned(EnergyProfile profile, TaskType type, TimeRange range)
    {
        var text = type == TaskType.Routine ? RoutineText(range) : PeakText(profile, type, range);
        return text + CriticalNote(profile, type);
    }

    public static string ForPart(EnergyProfile profile, TaskType type, TimeRange range) =>
        "Задача длиннее свободных окон дня, поэтому разбита на части. " + ForPlanned(profile, type, range);

    public static string ForManualMove(EnergyProfile profile, TaskType type, TimeRange range) =>
        $"Вы поставили задачу на {Format(range)}, планировщик больше её не двигает. " +
        ForPlanned(profile, type, range);

    private static string RoutineText(TimeRange range) =>
        $"Рутинная задача стоит на {Format(range)}: в эти часы энергии меньше, " +
        "поэтому пиковое время остаётся для более сложной работы.";

    private static string PeakText(EnergyProfile profile, TaskType type, TimeRange range)
    {
        var cycle = EnergyProfile.CycleFor(type)!.Value;
        var percent = BiorhythmCalculator.Percent(profile.CycleValue(cycle));
        var value = percent > 0 ? $"+{percent}" : percent < 0 ? $"−{-percent}" : "0";
        var phase = percent >= 0
            ? "цикл на подъёме, день подходит для такой работы"
            : "цикл на спаде, поэтому стоит оставить запас времени";

        var chronotype = ChronotypeName(profile.Chronotype);
        var midpoint = TimeRange.FromMinutes((range.Start + range.End) / 2);
        var window = profile.CircadianLevel(cycle, midpoint) >= GoodCircadianLevel
            ? $"Окно {Format(range)} совпадает с дневным подъёмом для хронотипа «{chronotype}»."
            : $"Окно {Format(range)} не попадает на дневной подъём для хронотипа «{chronotype}»: более удачные часы заняты или недоступны.";

        return $"{CycleName(cycle)} цикл сегодня {value}%: {phase}. {window}";
    }

    private static string CriticalNote(EnergyProfile profile, TaskType type)
    {
        if (profile.CriticalCycles.Count == 0 || type == TaskType.Routine) return "";
        return " Сегодня критический день: по возможности снизьте нагрузку и избегайте рискованных решений.";
    }

    public static string CycleName(BiorhythmCycle cycle) => cycle switch
    {
        BiorhythmCycle.Physical => "Физический",
        BiorhythmCycle.Emotional => "Эмоциональный",
        BiorhythmCycle.Intellectual => "Интеллектуальный",
        _ => throw new ArgumentOutOfRangeException(nameof(cycle))
    };

    private static string ChronotypeName(Chronotype chronotype) => chronotype switch
    {
        Chronotype.Lark => "жаворонок",
        Chronotype.Dove => "голубь",
        Chronotype.Owl => "сова",
        _ => throw new ArgumentOutOfRangeException(nameof(chronotype))
    };

    private static string Format(TimeRange range) => $"{range.StartTime:HH\\:mm}–{range.EndTime:HH\\:mm}";
}
