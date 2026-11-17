namespace PerfectDay_Domain.Entities;

public class DaySchedule
{
    private readonly List<ScheduleBlock> _blocks = [];

    private DaySchedule()
    {
    }

    public DaySchedule(Guid userId, DateOnly date)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Date = date;
        GeneratedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public IReadOnlyCollection<ScheduleBlock> Blocks => _blocks;

    /// <summary>Облегчённый день: сложной работы меньше, паузы между задачами обязательны.</summary>
    public bool IsLightened { get; private set; }

    /// <summary>Предупреждения о нагрузке, которые пользователь решил оставить без изменений.</summary>
    public List<string> DismissedWarnings { get; private set; } = [];

    public void ReplaceBlocks(IEnumerable<ScheduleBlock> blocks)
    {
        _blocks.Clear();
        _blocks.AddRange(blocks);
        GeneratedAt = DateTime.UtcNow;
    }

    public void RemoveBlock(ScheduleBlock block) => _blocks.Remove(block);

    public void AddBlocks(IEnumerable<ScheduleBlock> blocks) => _blocks.AddRange(blocks);

    public void SetLightened(bool isLightened) => IsLightened = isLightened;

    public void DismissWarning(string warningId)
    {
        if (!DismissedWarnings.Contains(warningId)) DismissedWarnings = [..DismissedWarnings, warningId];
    }
}
