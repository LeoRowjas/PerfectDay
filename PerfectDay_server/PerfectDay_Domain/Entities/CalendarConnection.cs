using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Entities;

/// <summary>Внешний календарь, подключённый по секретной ссылке iCal.</summary>
public class CalendarConnection
{
    private CalendarConnection()
    {
    }

    public CalendarConnection(Guid userId, CalendarProvider provider, string url, string timeZone)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Provider = provider;
        Url = url;
        TimeZone = timeZone;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public CalendarProvider Provider { get; private set; }

    /// <summary>Ссылка даёт доступ к календарю без пароля, поэтому клиенту она целиком не отдаётся.</summary>
    public string Url { get; private set; } = null!;

    /// <summary>Часовой пояс пользователя (IANA): в него переводится время событий.</summary>
    public string TimeZone { get; private set; } = null!;

    public string? Name { get; private set; }
    public bool ImportAsBusy { get; private set; } = true;
    public bool CreateTasksFromTagged { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>События, из которых уже создавались задачи: удалённая задача не появится снова.</summary>
    public List<string> ImportedTaskKeys { get; private set; } = [];

    public void UpdateSettings(bool importAsBusy, bool createTasksFromTagged)
    {
        ImportAsBusy = importAsBusy;
        CreateTasksFromTagged = createTasksFromTagged;
    }

    public void MarkSynced(string? name, string timeZone)
    {
        Name = name;
        TimeZone = timeZone;
        LastSyncedAt = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error) => LastError = error;

    public void RememberTask(string key)
    {
        if (!ImportedTaskKeys.Contains(key)) ImportedTaskKeys = [..ImportedTaskKeys, key];
    }
}
