namespace PerfectDay_Domain.Entities;

/// <summary>Отметка, что пользователь прочитал уведомление. Сами уведомления вычисляются из расписания.</summary>
public class NotificationRead
{
    private NotificationRead()
    {
    }

    public NotificationRead(Guid userId, string key)
    {
        UserId = userId;
        Key = key;
        ReadAt = DateTime.UtcNow;
    }

    public Guid UserId { get; private set; }
    public string Key { get; private set; } = null!;
    public DateTime ReadAt { get; private set; }
}
