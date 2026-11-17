using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

public sealed class BiorhythmService(IUserRepository users)
{
    private const int WeekDays = 7;
    private const int MonthDays = 30;
    private const int MaxForecastDays = 90;
    private const double PeakLevel = 0.9;

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            return new DashboardDto(date, false, false, user.Chronotype, null, EmptyCycles(), [], []);

        var profile = new EnergyProfile(birthDate, date, chronotype);
        var daysLived = date.DayNumber - birthDate.DayNumber;
        var cycles = BiorhythmCalculator.Cycles
            .Select(cycle =>
            {
                var period = BiorhythmCalculator.PeriodDays(cycle);
                var isCritical = profile.CriticalCycles.Contains(cycle);
                return new CycleValueDto(
                    cycle,
                    period,
                    ((daysLived % period) + period) % period + 1,
                    BiorhythmCalculator.Percent(profile.CycleValue(cycle)),
                    Phase(birthDate, date, cycle, isCritical),
                    isCritical);
            })
            .ToList();

        var recommendations = DayRecommendations.Create(profile, user.WorkdayStart, user.WorkdayEnd)
            .Select(r => new RecommendationDto(r.Activity, r.Title, r.Start, r.End, r.Text, r.EnergyFrom, r.EnergyTo, r.IsDeclining))
            .ToList();

        var warnings = LoadAnalyzer.CriticalDayWarnings(profile).Select(w => w.ToDto()).ToList();
        return new DashboardDto(date, true, user.OnboardedAt is null, chronotype, daysLived, cycles, recommendations, warnings);
    }

    /// <summary>Лучшие и критические дни на ближайшие дни, начиная с date.</summary>
    public async Task<ForecastDto> GetForecastAsync(Guid userId, DateOnly date, int days, CancellationToken ct)
    {
        if (days is < 1 or > MaxForecastDays)
            throw new AppValidationException($"Прогноз строится на срок от 1 до {MaxForecastDays} дней.");

        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is not { } birthDate)
            throw new ConflictException("Укажите дату рождения в профиле.");

        var forecast = Forecast.Create(birthDate, date, days);
        return new ForecastDto(date, date.AddDays(days - 1),
            forecast.GoodPeriods.Select(p => new GoodPeriodDto(p.Cycle, p.From, p.To, p.MaxPercent)).ToList(),
            forecast.CriticalDates.Select(c => new CriticalDateDto(c.Date, c.Cycles)).ToList());
    }

    private static CyclePhase Phase(DateOnly birthDate, DateOnly date, BiorhythmCycle cycle, bool isCritical)
    {
        if (isCritical) return CyclePhase.Critical;

        var value = BiorhythmCalculator.Value(birthDate, date, cycle);
        if (value >= PeakLevel) return CyclePhase.Peak;
        if (value <= -PeakLevel) return CyclePhase.Trough;
        return BiorhythmCalculator.Value(birthDate, date.AddDays(1), cycle) >= value ? CyclePhase.Rising : CyclePhase.Falling;
    }

    public async Task<ChartDto> GetChartAsync(Guid userId, DateOnly date, ChartScale scale, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            throw new ConflictException("Укажите дату рождения и хронотип в профиле.");

        var points = scale switch
        {
            ChartScale.Day => HourlyPoints(birthDate, date, chronotype),
            ChartScale.Week => DailyPoints(birthDate, date, date.AddDays(WeekDays - 1)),
            ChartScale.Month => DailyPoints(birthDate, date, date.AddDays(MonthDays - 1)),
            _ => throw new AppValidationException("Неизвестный масштаб графика.")
        };

        return new ChartDto(scale, points);
    }

    private static List<ChartPointDto> HourlyPoints(DateOnly birthDate, DateOnly date, Chronotype chronotype)
    {
        var profile = new EnergyProfile(birthDate, date, chronotype);
        var isCritical = profile.CriticalCycles.Count > 0;

        return Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var time = new TimeOnly(hour, 0);
                return new ChartPointDto(
                    $"{hour:00}:00",
                    BiorhythmCalculator.Percent(profile.Energy(BiorhythmCycle.Physical, time)),
                    BiorhythmCalculator.Percent(profile.Energy(BiorhythmCycle.Emotional, time)),
                    BiorhythmCalculator.Percent(profile.Energy(BiorhythmCycle.Intellectual, time)),
                    isCritical);
            })
            .ToList();
    }

    private static List<ChartPointDto> DailyPoints(DateOnly birthDate, DateOnly from, DateOnly to)
    {
        var points = new List<ChartPointDto>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            points.Add(new ChartPointDto(
                day.ToString("yyyy-MM-dd"),
                BiorhythmCalculator.Percent(BiorhythmCalculator.Value(birthDate, day, BiorhythmCycle.Physical)),
                BiorhythmCalculator.Percent(BiorhythmCalculator.Value(birthDate, day, BiorhythmCycle.Emotional)),
                BiorhythmCalculator.Percent(BiorhythmCalculator.Value(birthDate, day, BiorhythmCycle.Intellectual)),
                BiorhythmCalculator.CriticalCycles(birthDate, day).Count > 0));
        }

        return points;
    }

    private static List<CycleValueDto> EmptyCycles() => BiorhythmCalculator.Cycles
        .Select(cycle => new CycleValueDto(cycle, BiorhythmCalculator.PeriodDays(cycle), 0, 0, CyclePhase.Rising, false))
        .ToList();

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
}
