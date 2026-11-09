using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Biorhythms;

public static class BiorhythmCalculator
{
    private const double ZeroTolerance = 1e-9;

    public static IReadOnlyList<BiorhythmCycle> Cycles { get; } =
        [BiorhythmCycle.Physical, BiorhythmCycle.Emotional, BiorhythmCycle.Intellectual];

    public static int PeriodDays(BiorhythmCycle cycle) => cycle switch
    {
        BiorhythmCycle.Physical => 23,
        BiorhythmCycle.Emotional => 28,
        BiorhythmCycle.Intellectual => 33,
        _ => throw new ArgumentOutOfRangeException(nameof(cycle))
    };

    /// <summary>Значение цикла на дату в диапазоне от −1 до +1.</summary>
    public static double Value(DateOnly birthDate, DateOnly date, BiorhythmCycle cycle)
    {
        var days = date.DayNumber - birthDate.DayNumber;
        var value = Math.Sin(2 * Math.PI * days / PeriodDays(cycle));
        return Math.Abs(value) < ZeroTolerance ? 0 : value;
    }

    public static int Percent(double value) => (int)Math.Round(value * 100);

    /// <summary>Критический день: цикл равен нулю или переходит через ноль до следующей даты.</summary>
    public static bool IsCritical(DateOnly birthDate, DateOnly date, BiorhythmCycle cycle)
    {
        var today = Value(birthDate, date, cycle);
        var tomorrow = Value(birthDate, date.AddDays(1), cycle);
        return today == 0 || today * tomorrow < 0;
    }

    public static IReadOnlyList<BiorhythmCycle> CriticalCycles(DateOnly birthDate, DateOnly date) =>
        Cycles.Where(cycle => IsCritical(birthDate, date, cycle)).ToList();
}
