using System.Globalization;
using System.Text;
using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Analytics;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

/// <summary>Анализ эффективности: план и факт за прошедшую неделю или месяц (сегодняшний день ещё не закончен).</summary>
public sealed class AnalyticsService(IUserRepository users, IScheduleRepository schedules, ScheduleService scheduleService)
{
    private const int LightenAheadDays = 14;

    public async Task<AnalyticsDto> GetAsync(Guid userId, DateOnly today, AnalyticsPeriod period, CancellationToken ct)
    {
        var (user, birthDate, chronotype) = await GetUserAsync(userId, ct);
        var report = await AnalyzeAsync(userId, birthDate, chronotype, today, period, ct);

        var upcoming = Forecast.Create(birthDate, today, LightenAheadDays).CriticalDates.Select(c => c.Date).ToList();
        var lightened = (await schedules.ListRangeAsync(user.Id, today, today.AddDays(LightenAheadDays - 1), ct))
            .Where(s => s.IsLightened)
            .Select(s => s.Date)
            .Order()
            .ToList();

        var hardDone = report.HardDone;
        return new AnalyticsDto(
            period,
            report.From,
            report.To,
            report.Planned,
            report.Done,
            EfficiencyAnalyzer.Percent(report.Done, report.Planned),
            report.PreviousPlanned == 0 ? null : EfficiencyAnalyzer.Percent(report.PreviousDone, report.PreviousPlanned),
            hardDone,
            hardDone == 0 ? null : EfficiencyAnalyzer.Percent(report.HardDoneInPeak, hardDone),
            report.MovedManually,
            report.MostMovedType,
            report.CriticalPlanned,
            report.CriticalDone,
            report.CriticalPlanned == 0 ? null : EfficiencyAnalyzer.Percent(report.CriticalDone, report.CriticalPlanned),
            report.Days.Select(d => new DayStatDto(d.Date, d.Planned, d.Done, d.IsCritical)).ToList(),
            report.Types.Select(t => new TypeStatDto(t.Type, t.Planned, t.Done, EfficiencyAnalyzer.Percent(t.Done, t.Planned))).ToList(),
            report.Insights.Select(i => new InsightDto(JsonName(i.Kind), i.Severity, i.Title, i.Text)).ToList(),
            upcoming,
            lightened);
    }

    /// <summary>Главная рекомендация, которую можно применить сразу: облегчить критические дни на две недели вперёд.</summary>
    public async Task<ApplyRecommendationsDto> ApplyAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var (_, birthDate, _) = await GetUserAsync(userId, ct);
        var dates = Forecast.Create(birthDate, today, LightenAheadDays).CriticalDates.Select(c => c.Date).ToList();

        var lightened = new List<DateOnly>();
        foreach (var date in dates)
        {
            var schedule = await schedules.GetAsync(userId, date, ct);
            if (schedule?.IsLightened == true) continue;

            await scheduleService.SetLightenedAsync(userId, date, today, true, ct);
            lightened.Add(date);
        }

        return new ApplyRecommendationsDto(lightened);
    }

    /// <summary>Отчёт в CSV для Excel: точка с запятой и BOM, чтобы кириллица открылась без настроек.</summary>
    public async Task<byte[]> ReportAsync(Guid userId, DateOnly today, AnalyticsPeriod period, CancellationToken ct)
    {
        var data = await GetAsync(userId, today, period, ct);
        var russian = CultureInfo.GetCultureInfo("ru-RU");
        var csv = new StringBuilder();

        csv.AppendLine($"Анализ эффективности;{data.From:dd.MM.yyyy}–{data.To:dd.MM.yyyy}");
        csv.AppendLine($"Выполнено по плану;{data.Percent}%;{data.Done} из {data.Planned}");
        csv.AppendLine($"Сложное в пиковые часы;{Percent(data.PeakPercent)}");
        csv.AppendLine($"Перенесено вручную;{data.MovedManually}");
        csv.AppendLine($"В критические дни;{Percent(data.CriticalPercent)}");
        csv.AppendLine();
        csv.AppendLine("Дата;День недели;Запланировано;Выполнено;Критический день");
        foreach (var day in data.Days)
        {
            csv.AppendLine($"{day.Date:dd.MM.yyyy};{day.Date.ToString("dddd", russian)};{day.Planned};{day.Done};{(day.IsCritical ? "да" : "")}");
        }

        csv.AppendLine();
        csv.AppendLine("Тип задач;Запланировано;Выполнено;Процент");
        foreach (var type in data.Types)
            csv.AppendLine($"{TypeName(type.Type)};{type.Planned};{type.Done};{type.Percent}%");

        if (data.Insights.Count > 0)
        {
            csv.AppendLine();
            csv.AppendLine("Вывод;Пояснение");
            foreach (var insight in data.Insights) csv.AppendLine($"{Escape(insight.Title)};{Escape(insight.Text)}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private async Task<EfficiencyReport> AnalyzeAsync(Guid userId, DateOnly birthDate, Chronotype chronotype, DateOnly today,
        AnalyticsPeriod period, CancellationToken ct)
    {
        var length = period == AnalyticsPeriod.Week ? 7 : 30;
        var to = today.AddDays(-1);
        var from = to.AddDays(-(length - 1));

        // Нужны и предыдущий период для сравнения, и дни до сегодня: по ним видно, где задача была выполнена.
        var days = (await schedules.ListRangeAsync(userId, from.AddDays(-length), today, ct))
            .Select(s => new PlannedDay(s.Date, s.Blocks
                .Where(b => b.Task is not null)
                .Select(b => new PlannedBlock(b.Task!.Id, b.Task.Type, b.Task.IsCompleted, TimeRange.From(b.Start, b.End), b.IsPinned))
                .ToList()));
        return EfficiencyAnalyzer.Analyze(birthDate, chronotype, days, from, to, today);
    }

    private async Task<(User User, DateOnly BirthDate, Chronotype Chronotype)> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
        if (user.BirthDate is not { } birthDate || user.Chronotype is not { } chronotype)
            throw new ConflictException("Укажите дату рождения и хронотип в профиле.");
        return (user, birthDate, chronotype);
    }

    private static string JsonName(InsightKind kind) => kind switch
    {
        InsightKind.TimeOfDay => "timeOfDay",
        InsightKind.Postponed => "postponed",
        InsightKind.CriticalDays => "criticalDays",
        _ => "trend"
    };

    private static string Percent(int? value) => value is { } v ? $"{v}%" : "—";

    private static string Escape(string text) => text.Contains(';') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;

    private static string TypeName(TaskType type) => type switch
    {
        TaskType.Intellectual => "Интеллектуальные",
        TaskType.Physical => "Физические",
        TaskType.Creative => "Творческие",
        _ => "Рутинные"
    };
}
