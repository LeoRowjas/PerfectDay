using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Biorhythms;

/// <summary>Несколько дней подряд, когда цикл около пика: на них стоит ставить большие дела.</summary>
public sealed record GoodPeriod(BiorhythmCycle Cycle, DateOnly From, DateOnly To, int MaxPercent);

public sealed record CriticalDate(DateOnly Date, IReadOnlyList<BiorhythmCycle> Cycles);

public sealed record ForecastResult(IReadOnlyList<GoodPeriod> GoodPeriods, IReadOnlyList<CriticalDate> CriticalDates);

public static class Forecast
{
    private const double PeakLevel = 0.9;

    public static ForecastResult Create(DateOnly birthDate, DateOnly from, int days)
    {
        var dates = Enumerable.Range(0, days).Select(from.AddDays).ToList();

        var periods = new List<GoodPeriod>();
        foreach (var cycle in BiorhythmCalculator.Cycles)
        {
            DateOnly? start = null;
            var max = 0;
            foreach (var date in dates.Append(from.AddDays(days)))
            {
                var value = BiorhythmCalculator.Value(birthDate, date, cycle);
                var isPeak = value >= PeakLevel && date < from.AddDays(days);
                if (isPeak)
                {
                    start ??= date;
                    max = Math.Max(max, BiorhythmCalculator.Percent(value));
                    continue;
                }

                if (start is { } periodStart) periods.Add(new GoodPeriod(cycle, periodStart, date.AddDays(-1), max));
                start = null;
                max = 0;
            }
        }

        var critical = dates
            .Select(date => new CriticalDate(date, BiorhythmCalculator.CriticalCycles(birthDate, date)))
            .Where(c => c.Cycles.Count > 0)
            .ToList();

        return new ForecastResult(periods.OrderBy(p => p.From).ToList(), critical);
    }
}
