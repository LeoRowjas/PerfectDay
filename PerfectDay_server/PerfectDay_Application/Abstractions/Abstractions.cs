using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByExternalLoginAsync(ExternalProvider provider, string subject, CancellationToken ct);
    void Add(User user);
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> ListAsync(Guid userId, TaskType? type, CancellationToken ct);
    Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct);

    /// <summary>Невыполненные задачи со сроком на дату; при includeOverdue ещё и просроченные.</summary>
    Task<IReadOnlyList<TaskItem>> ListPlannableAsync(Guid userId, DateOnly date, bool includeOverdue, CancellationToken ct);

    /// <summary>Невыполненные повторения серии со сроком не раньше from.</summary>
    Task<IReadOnlyList<TaskItem>> ListOpenInSeriesAsync(Guid seriesId, DateOnly from, CancellationToken ct);

    Task<IReadOnlyList<TaskSeries>> ListSeriesBehindAsync(Guid userId, DateOnly upTo, CancellationToken ct);
    Task<TaskSeries?> GetSeriesAsync(Guid userId, Guid id, CancellationToken ct);

    void Add(TaskItem task);
    void AddRange(IEnumerable<TaskItem> tasks);
    void Remove(TaskItem task);
    void AddSeries(TaskSeries series);
    void RemoveSeries(TaskSeries series);
}

public interface IScheduleRepository
{
    Task<DaySchedule?> GetAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DaySchedule>> ListContainingTaskAsync(Guid taskId, CancellationToken ct);

    /// <summary>Расписания с блоками и задачами за даты from–to включительно.</summary>
    Task<IReadOnlyList<DaySchedule>> ListRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct);

    void Add(DaySchedule schedule);
    Task RemoveFromDateAsync(Guid userId, DateOnly date, CancellationToken ct);
}

public interface INotificationReadRepository
{
    Task<IReadOnlySet<string>> ListReadKeysAsync(Guid userId, IReadOnlyCollection<string> keys, CancellationToken ct);
    void Add(NotificationRead read);
}

public interface ICalendarRepository
{
    Task<IReadOnlyList<CalendarConnection>> ListConnectionsAsync(Guid userId, CancellationToken ct);
    Task<CalendarConnection?> GetConnectionAsync(Guid userId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ExternalEvent>> ListEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct);
    Task ReplaceEventsAsync(CalendarConnection connection, IEnumerable<ExternalEvent> events, CancellationToken ct);
    void Add(CalendarConnection connection);
    void Remove(CalendarConnection connection);
}

/// <summary>Событие из файла iCal во времени пользователя; событие на несколько дней разбито по дням.</summary>
public sealed record FeedEvent(string Uid, string Title, DateOnly Date, TimeOnly Start, TimeOnly End, bool IsAllDay);

public sealed record CalendarFeed(string? Name, IReadOnlyList<FeedEvent> Events);

public interface ICalendarFeedReader
{
    /// <summary>Загружает календарь по ссылке. Ошибки сообщает через CalendarFeedException.</summary>
    Task<CalendarFeed> ReadAsync(string url, string timeZone, DateOnly from, DateOnly to, CancellationToken ct);
}

/// <summary>Кто вошёл через внешний сервис. Email подтверждён, если провайдер это гарантирует.</summary>
public sealed record ExternalIdentity(ExternalProvider Provider, string Subject, string? Email, bool IsEmailVerified);

/// <summary>OAuth 2.0 с PKCE: адрес страницы входа провайдера и обмен кода на данные пользователя.</summary>
public interface IExternalAuthProvider
{
    ExternalProvider Provider { get; }
    bool IsConfigured { get; }
    string BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge);

    /// <param name="callback">Все параметры, с которыми провайдер вернул пользователя (VK ID присылает device_id).</param>
    Task<ExternalIdentity> ExchangeAsync(string redirectUri, string code, string codeVerifier,
        IReadOnlyDictionary<string, string> callback, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    string CreateToken(User user);
}
