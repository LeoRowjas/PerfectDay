using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Entities;

public class ScheduleBlock
{
    private ScheduleBlock()
    {
    }

    public ScheduleBlock(Guid scheduleId, Guid? taskId, BlockKind kind, string title, TimeOnly start, TimeOnly end, string explanation)
    {
        Id = Guid.CreateVersion7();
        ScheduleId = scheduleId;
        TaskId = taskId;
        Kind = kind;
        Title = title;
        Start = start;
        End = end;
        Explanation = explanation;
    }

    public Guid Id { get; private set; }
    public Guid ScheduleId { get; private set; }
    public Guid? TaskId { get; private set; }
    public TaskItem? Task { get; private set; }
    public BlockKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public TimeOnly Start { get; private set; }
    public TimeOnly End { get; private set; }
    public string Explanation { get; private set; } = null!;

    /// <summary>Блок перенесён пользователем: планировщик больше его не двигает.</summary>
    public bool IsPinned { get; private set; }

    public void Reschedule(TimeOnly start, TimeOnly end, string explanation)
    {
        Start = start;
        End = end;
        Explanation = explanation;
    }

    public void Pin() => IsPinned = true;

    public void Unpin() => IsPinned = false;
}
