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
    private const int WeekDaysBack = 3;
    private const int WeekDaysForward = 3;
    private const int MonthDaysBack = 7;
    private const int MonthDaysForward = 23;

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            return new DashboardDto(date, false, EmptyCycles(), [], []);

        var profile = new EnergyProfile(birthDate, date, chronotype);
        var cycles = BiorhythmCalculator.Cycles
            .Select(cycle => new CycleValueDto(
                cycle,
                BiorhythmCalculator.PeriodDays(cycle),
                BiorhythmCalculator.Percent(profile.CycleValue(cycle)),
                profile.CriticalCycles.Contains(cycle)))
            .ToList();

        var recommendations = DayRecommendations.Create(profile, user.WorkdayStart, user.WorkdayEnd)
            .Select(r => new RecommendationDto(r.Activity, r.Title, r.Start, r.End, r.Text))
            .ToList();

        var warnings = LoadAnalyzer.CriticalDayWarnings(profile).Select(w => w.ToDto()).ToList();
        return new DashboardDto(date, true, cycles, recommendations, warnings);
    }

    public async Task<ChartDto> GetChartAsync(Guid userId, DateOnly date, ChartScale scale, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            throw new ConflictException("Укажите дату рождения и хронотип в профиле.");

        var points = scale switch
        {
            ChartScale.Day => HourlyPoints(birthDate, date, chronotype),
            ChartScale.Week => DailyPoints(birthDate, date.AddDays(-WeekDaysBack), date.AddDays(WeekDaysForward)),
            ChartScale.Month => DailyPoints(birthDate, date.AddDays(-MonthDaysBack), date.AddDays(MonthDaysForward)),
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
        .Select(cycle => new CycleValueDto(cycle, BiorhythmCalculator.PeriodDays(cycle), 0, false))
        .ToList();

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
}
