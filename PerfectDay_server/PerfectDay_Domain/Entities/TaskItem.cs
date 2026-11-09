using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Entities;

public class TaskItem
{
    private TaskItem()
    {
    }

    public TaskItem(Guid userId, string title, TaskType type, DateOnly dueDate, int durationMinutes, TaskPriority priority)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
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

    public void SetCompleted(bool isCompleted) => IsCompleted = isCompleted;

    public void Update(string title, TaskType type, DateOnly dueDate, int durationMinutes, TaskPriority priority)
    {
        Title = title;
        Type = type;
        DueDate = dueDate;
        DurationMinutes = durationMinutes;
        Priority = priority;
    }
}
