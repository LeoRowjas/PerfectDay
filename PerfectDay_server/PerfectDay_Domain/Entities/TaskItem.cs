using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Entities;

public class TaskItem
{
    private TaskItem()
    {
    }

    public TaskItem(Guid userId, string title, TaskType type, DateOnly dueDate, int durationMinutes, TaskPriority priority,
        Guid? seriesId = null, string? externalKey = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        SeriesId = seriesId;
        ExternalKey = externalKey;
        Update(title, type, dueDate, durationMinutes, priority);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public TaskType Type { get; private set; }
    public DateOnly DueDate { get; private set; }
    public int DurationMinutes { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>Повторение периодической задачи.</summary>
    public Guid? SeriesId { get; private set; }

    public TaskSeries? Series { get; private set; }

    /// <summary>Задача создана из события календаря с пометкой «#задача».</summary>
    public string? ExternalKey { get; private set; }

    public void AttachToSeries(TaskSeries series)
    {
        SeriesId = series.Id;
        Series = series;
    }

    public void DetachFromSeries()
    {
        SeriesId = null;
        Series = null;
    }

    public void SetCompleted(bool isCompleted) => IsCompleted = isCompleted;

    public void MoveTo(DateOnly dueDate) => DueDate = dueDate;

    public void Update(string title, TaskType type, DateOnly dueDate, int durationMinutes, TaskPriority priority)
    {
        Title = title;
        Type = type;
        DueDate = dueDate;
        DurationMinutes = durationMinutes;
        Priority = priority;
    }
}
