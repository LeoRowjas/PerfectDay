using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Biorhythms;

/// <summary>
/// Энергия человека в конкретный момент дня: значение биоритма на дату задаёт общий уровень,
/// а суточная кривая хронотипа определяет, на какие часы приходятся подъёмы и спады.
/// </summary>
public sealed class EnergyProfile
{
    private const double CycleWeight = 0.6;
    private const double CircadianWeight = 0.4;
    private const double PeakWidthHours = 2.0;
    private const double SecondWaveDelayHours = 5.0;
    private const double SecondWaveStrength = 0.6;

    private readonly Dictionary<BiorhythmCycle, double> _values;

    public EnergyProfile(DateOnly birthDate, DateOnly date, Chronotype chronotype)
    {
        Date = date;
        Chronotype = chronotype;
        _values = BiorhythmCalculator.Cycles.ToDictionary(
            cycle => cycle,
            cycle => BiorhythmCalculator.Value(birthDate, date, cycle));
        CriticalCycles = BiorhythmCalculator.CriticalCycles(birthDate, date);
    }

    public DateOnly Date { get; }
    public Chronotype Chronotype { get; }
    public IReadOnlyList<BiorhythmCycle> CriticalCycles { get; }

    public double CycleValue(BiorhythmCycle cycle) => _values[cycle];

    /// <summary>Энергия цикла в указанное время, от −1 до +1.</summary>
    public double Energy(BiorhythmCycle cycle, TimeOnly time)
    {
        var circadian = Circadian(cycle, time.ToTimeSpan().TotalHours);
        return CycleWeight * _values[cycle] + CircadianWeight * (2 * circadian - 1);
    }

    /// <summary>Насколько время подходит для задачи этого типа: чем больше, тем лучше.</summary>
    public double Suitability(TaskType type, TimeOnly time) => type switch
    {
        TaskType.Intellectual => Energy(BiorhythmCycle.Intellectual, time),
        TaskType.Physical => Energy(BiorhythmCycle.Physical, time),
        TaskType.Creative => Energy(BiorhythmCycle.Emotional, time),
        TaskType.Routine => -BiorhythmCalculator.Cycles.Max(cycle => Energy(cycle, time)),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static BiorhythmCycle? CycleFor(TaskType type) => type switch
    {
        TaskType.Intellectual => BiorhythmCycle.Intellectual,
        TaskType.Physical => BiorhythmCycle.Physical,
        TaskType.Creative => BiorhythmCycle.Emotional,
        _ => null
    };

    /// <summary>Положение на суточной кривой хронотипа: 1 — пик, 0 — спад.</summary>
    public double CircadianLevel(BiorhythmCycle cycle, TimeOnly time) => Circadian(cycle, time.ToTimeSpan().TotalHours);

    private double Circadian(BiorhythmCycle cycle, double hour)
    {
        var peak = PeakHour(cycle, Chronotype);
        var firstWave = Gaussian(hour - peak);
        var secondWave = SecondWaveStrength * Gaussian(hour - peak - SecondWaveDelayHours);
        return Math.Max(firstWave, secondWave);
    }

    private static double Gaussian(double distance) =>
        Math.Exp(-distance * distance / (2 * PeakWidthHours * PeakWidthHours));

    // У «сов» пики на 3–4 часа позже, чем у «жаворонков».
    private static double PeakHour(BiorhythmCycle cycle, Chronotype chronotype)
    {
        var larkPeak = cycle switch
        {
            BiorhythmCycle.Intellectual => 9.5,
            BiorhythmCycle.Emotional => 11.0,
            BiorhythmCycle.Physical => 14.0,
            _ => throw new ArgumentOutOfRangeException(nameof(cycle))
        };

        return larkPeak + chronotype switch
        {
            Chronotype.Lark => 0,
            Chronotype.Dove => 1.75,
            Chronotype.Owl => 3.5,
            _ => throw new ArgumentOutOfRangeException(nameof(chronotype))
        };
    }
}
