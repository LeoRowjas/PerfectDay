using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

/// <summary>
/// Внешние календари по ссылке iCal. События хранятся на месяц вперёд и обновляются при синхронизации;
/// занятые встречи становятся закреплёнными блоками расписания, события с «#задача» — задачами.
/// </summary>
public sealed class CalendarService(
    IUserRepository users,
    ICalendarRepository calendars,
    ICalendarFeedReader reader,
    ITaskRepository tasks,
    IScheduleRepository schedules,
    ScheduleService scheduleService,
    IUnitOfWork unitOfWork)
{
    private const int SyncDaysAhead = 31;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(15);
    private const int MinTaskMinutes = 5;
    private const int MaxTaskMinutes = 720;

    public async Task<CalendarsDto> GetAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        return await ToDtoAsync(user, today, ct);
    }

    public async Task<CalendarsDto> ConnectAsync(Guid userId, DateOnly today, ConnectCalendarRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        var provider = request.Provider!.Value;
        var url = request.Url.Trim();

        var existing = await calendars.ListConnectionsAsync(userId, ct);
        if (existing.Any(c => c.Provider == provider))
            throw new ConflictException($"Календарь {CalendarRules.ProviderName(provider)} уже подключён. Отключите его, чтобы подключить другой.");

        var connection = new CalendarConnection(userId, provider, url, request.TimeZone);
        CalendarFeed feed;
        try
        {
            feed = await reader.ReadAsync(url, request.TimeZone, today.AddDays(-1), today.AddDays(SyncDaysAhead), ct);
        }
        catch (CalendarFeedException e)
        {
            throw new AppValidationException(e.Message);
        }

        calendars.Add(connection);
        await ApplyFeedAsync(connection, feed, request.TimeZone, today, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await scheduleService.RefreshCalendarEventsAsync(userId, today, ct);

        return await ToDtoAsync(user, today, ct);
    }

    public async Task<CalendarsDto> UpdateSettingsAsync(Guid userId, Guid id, DateOnly today, CalendarSettingsRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        var connection = await GetConnectionAsync(userId, id, ct);

        connection.UpdateSettings(request.ImportAsBusy!.Value, request.CreateTasksFromTagged!.Value);
        if (connection.CreateTasksFromTagged)
            CreateTasks(connection, await calendars.ListEventsAsync(userId, today, today.AddDays(SyncDaysAhead), ct), today);

        await unitOfWork.SaveChangesAsync(ct);
        await scheduleService.RefreshCalendarEventsAsync(userId, today, ct);
        return await ToDtoAsync(user, today, ct);
    }

    /// <summary>Обновляет календари, которые давно не синхронизировались (или все, если force).</summary>
    public async Task<CalendarsDto> SyncAsync(Guid userId, DateOnly today, SyncCalendarsRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        var changed = false;
        foreach (var connection in await calendars.ListConnectionsAsync(userId, ct))
        {
            if (!request.Force && connection.LastSyncedAt > DateTime.UtcNow - SyncInterval) continue;

            try
            {
                var feed = await reader.ReadAsync(connection.Url, request.TimeZone, today.AddDays(-1), today.AddDays(SyncDaysAhead), ct);
                await ApplyFeedAsync(connection, feed, request.TimeZone, today, ct);
                changed = true;
            }
            catch (CalendarFeedException e)
            {
                connection.MarkFailed(e.Message);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        if (changed) await scheduleService.RefreshCalendarEventsAsync(userId, today, ct);
        return await ToDtoAsync(user, today, ct);
    }

    public async Task<CalendarsDto> DisconnectAsync(Guid userId, Guid id, DateOnly today, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, ct);
        calendars.Remove(await GetConnectionAsync(userId, id, ct));
        await unitOfWork.SaveChangesAsync(ct);
        await scheduleService.RefreshCalendarEventsAsync(userId, today, ct);
        return await ToDtoAsync(user, today, ct);
    }

    private async Task ApplyFeedAsync(CalendarConnection connection, CalendarFeed feed, string timeZone, DateOnly today, CancellationToken ct)
    {
        var events = feed.Events
            .Select(e => new ExternalEvent(connection.Id, connection.UserId, e.Uid, Trim(e.Title, 200), e.Date, e.Start, e.End, e.IsAllDay))
            .ToList();

        await calendars.ReplaceEventsAsync(connection, events, ct);
        connection.MarkSynced(feed.Name is { Length: > 0 } name ? Trim(name, 200) : null, timeZone);
        CreateTasks(connection, events, today);
    }

    private void CreateTasks(CalendarConnection connection, IEnumerable<ExternalEvent> events, DateOnly today)
    {
        if (!connection.CreateTasksFromTagged) return;

        foreach (var item in events.Where(e => e.ConnectionId == connection.Id && e.IsTaggedAsTask && !e.IsAllDay && e.Date >= today))
        {
            if (connection.ImportedTaskKeys.Contains(item.Key)) continue;

            var title = item.Title.Replace(ExternalEvent.TaskTag, "", StringComparison.OrdinalIgnoreCase).Trim();
            var minutes = Math.Clamp(TimeRange.From(item.Start, item.End).Length, MinTaskMinutes, MaxTaskMinutes);
            tasks.Add(new TaskItem(connection.UserId, title.Length == 0 ? "Задача из календаря" : title, TaskType.Routine,
                item.Date, minutes, TaskPriority.Medium, externalKey: $"{connection.Id}|{item.Key}"));
            connection.RememberTask(item.Key);
        }
    }

    private async Task<CalendarsDto> ToDtoAsync(User user, DateOnly today, CancellationToken ct)
    {
        var connections = await calendars.ListConnectionsAsync(user.Id, ct);
        var byId = connections.ToDictionary(c => c.Id);
        var events = (await calendars.ListEventsAsync(user.Id, today, today, ct))
            .Where(e => byId.ContainsKey(e.ConnectionId))
            .OrderBy(e => !e.IsAllDay).ThenBy(e => e.Start)
            .ToList();

        var workday = TimeRange.From(user.WorkdayStart, user.WorkdayEnd);
        var schedule = await schedules.GetAsync(user.Id, today, ct);
        var taskBlocks = schedule?.Blocks.Where(b => b.Kind == BlockKind.Task).OrderBy(b => b.Start).ToList() ?? [];

        var items = events.Select(e => ToEventDto(e, byId[e.ConnectionId], workday, taskBlocks)).ToList();

        MeetingAdviceDto? advice = null;
        if (user.BirthDate is { } birthDate && user.Chronotype is { } chronotype)
        {
            var meetings = events
                .Where(e => CalendarRules.IsBusy(e, byId[e.ConnectionId]))
                .Select(e => new Meeting(e.Title, TimeRange.From(e.Start, e.End)))
                .ToList();
            if (MeetingAdvisor.Suggest(new EnergyProfile(birthDate, today, chronotype), workday, meetings) is { } suggestion)
            {
                advice = new MeetingAdviceDto("Встреча на пике энергии",
                    $"«{suggestion.Title}» в {suggestion.Current.StartTime:HH\\:mm} попадает на спад эмоционального цикла " +
                    $"({Signed(suggestion.CurrentPercent)}%). Если время можно сдвинуть, лучше {suggestion.Better.StartTime:HH\\:mm}–" +
                    $"{suggestion.Better.EndTime:HH\\:mm} ({Signed(suggestion.BetterPercent)}%).",
                    suggestion.Better.StartTime, suggestion.Better.EndTime);
            }
        }

        return new CalendarsDto(
            connections.OrderBy(c => c.Provider).Select(ToDto).ToList(),
            today,
            items,
            advice);
    }

    private static ImportedEventDto ToEventDto(ExternalEvent item, CalendarConnection connection, TimeRange workday,
        List<ScheduleBlock> taskBlocks)
    {
        var range = TimeRange.From(item.Start, item.End);
        var (effect, note) = item switch
        {
            { IsAllDay: true } => (ImportedEventEffect.Outside, "Событие на весь день, на расписание не влияет"),
            _ when CalendarRules.IsTask(item, connection) => (ImportedEventEffect.Task, "Добавлено в задачи"),
            _ when !connection.ImportAsBusy => (ImportedEventEffect.Ignored, "Импорт занятого времени выключен"),
            _ when range.End <= workday.Start => (ImportedEventEffect.Outside, "До рабочего дня, на расписание не влияет"),
            _ when range.Start >= workday.End => (ImportedEventEffect.Outside, "После рабочего дня, на расписание не влияет"),
            _ => (ImportedEventEffect.Busy, BusyNote(range, taskBlocks))
        };

        return new ImportedEventDto(item.Title, connection.Provider, item.IsAllDay ? null : item.Start,
            item.IsAllDay ? null : item.End, item.IsAllDay, effect, note);
    }

    private static string BusyNote(TimeRange range, List<ScheduleBlock> taskBlocks)
    {
        var next = taskBlocks.FirstOrDefault(b => TimeRange.ToMinutes(b.Start) >= range.End && TimeRange.ToMinutes(b.Start) - range.End <= 30);
        return next is null
            ? "Время занято, задачи стоят вокруг встречи"
            : $"Задачи сдвинуты: «{next.Task?.Title ?? next.Title}» начнётся в {next.Start:HH\\:mm}";
    }

    private static CalendarConnectionDto ToDto(CalendarConnection connection) =>
        new(connection.Id, connection.Provider, connection.Name, UrlHint(connection.Url), connection.ImportAsBusy,
            connection.CreateTasksFromTagged, connection.LastSyncedAt, connection.LastError);

    // Ссылка секретная: показываем только адрес сервиса.
    private static string UrlHint(string url) =>
        Uri.TryCreate(url.Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out var uri)
            ? uri.Host
            : "ссылка iCal";

    private static string Signed(int percent) => percent > 0 ? $"+{percent}" : percent < 0 ? $"−{-percent}" : "0";

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max];

    private async Task<CalendarConnection> GetConnectionAsync(Guid userId, Guid id, CancellationToken ct) =>
        await calendars.GetConnectionAsync(userId, id, ct) ?? throw new NotFoundException("Этот календарь уже отключён.");

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
}
