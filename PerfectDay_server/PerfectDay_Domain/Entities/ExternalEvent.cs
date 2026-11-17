namespace PerfectDay_Domain.Entities;

/// <summary>
/// Событие из внешнего календаря на конкретный день, во времени пользователя.
/// Повторяющиеся события хранятся по одному на каждое повторение; при синхронизации всё заменяется.
/// </summary>
public class ExternalEvent
{
    private ExternalEvent()
    {
    }

    public ExternalEvent(Guid connectionId, Guid userId, string uid, string title, DateOnly date, TimeOnly start, TimeOnly end, bool isAllDay)
    {
        Id = Guid.CreateVersion7();
        ConnectionId = connectionId;
        UserId = userId;
        Uid = uid;
        Title = title;
        Date = date;
        Start = start;
        End = end;
        IsAllDay = isAllDay;
    }

    public Guid Id { get; private set; }
    public Guid ConnectionId { get; private set; }
    public Guid UserId { get; private set; }
    public string Uid { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DateOnly Date { get; private set; }
    public TimeOnly Start { get; private set; }
    public TimeOnly End { get; private set; }
    public bool IsAllDay { get; private set; }

    public const string TaskTag = "#задача";

    public bool IsTaggedAsTask => Title.Contains(TaskTag, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ключ повторения: по нему из события создаётся не больше одной задачи.</summary>
    public string Key => $"{Uid}|{Date:yyyy-MM-dd}";
}
