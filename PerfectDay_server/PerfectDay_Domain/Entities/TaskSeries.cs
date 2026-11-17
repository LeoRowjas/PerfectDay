using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;

namespace PerfectDay_Domain.Entities;

/// <summary>
/// Периодическая задача. Повторения создаются обычными задачами на неделю вперёд; GeneratedUntil только растёт,
/// поэтому удалённое повторение не появляется снова.
/// </summary>
public class TaskSeries
{
    private TaskSeries()
    {
    }

    public TaskSeries(Guid userId, string title, TaskType type, int durationMinutes, TaskPriority priority,
        RecurrenceKind kind, IEnumerable<DayOfWeek> days, DateOnly startDate, DateOnly? endDate)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        Update(title, type, durationMinutes, priority, kind, days, startDate, endDate);
        GeneratedUntil = startDate.AddDays(-1);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public TaskType Type { get; private set; }
    public int DurationMinutes { get; private set; }
    public TaskPriority Priority { get; private set; }
    public RecurrenceKind Kind { get; private set; }

    /// <summary>Дни недели для еженедельного повтора, битовая маска: понедельник — 1, воскресенье — 64.</summary>
    public int WeekDaysMask { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    /// <summary>До какой даты включительно повторения уже созданы.</summary>
    public DateOnly GeneratedUntil { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<DayOfWeek> Days => AllDays.Where(day => (WeekDaysMask & Bit(day)) != 0).ToList();

    public void Update(string title, TaskType type, int durationMinutes, TaskPriority priority,
        RecurrenceKind kind, IEnumerable<DayOfWeek> days, DateOnly startDate, DateOnly? endDate)
    {
        var mask = days.Aggregate(0, (current, day) => current | Bit(day));
        if (kind == RecurrenceKind.Weekly && mask == 0)
            throw new DomainRuleException("Выберите хотя бы один день недели.");
        if (endDate < startDate)
            throw new DomainRuleException("Дата окончания повторов должна быть не раньше первого дня.");

        Title = title;
        Type = type;
        DurationMinutes = durationMinutes;
        Priority = priority;
        Kind = kind;
        WeekDaysMask = kind == RecurrenceKind.Weekly ? mask : 0;
        StartDate = startDate;
        EndDate = endDate;
    }

    public bool OccursOn(DateOnly date) =>
        date >= StartDate && (EndDate is null || date <= EndDate) &&
        (Kind == RecurrenceKind.Daily || (WeekDaysMask & Bit(date.DayOfWeek)) != 0);

    /// <summary>Ближайшие даты повторов начиная с from.</summary>
    public IEnumerable<DateOnly> DatesFrom(DateOnly from, DateOnly to)
    {
        for (var date = from < StartDate ? StartDate : from; date <= to && (EndDate is null || date <= EndDate); date = date.AddDays(1))
            if (OccursOn(date)) yield return date;
    }

    /// <summary>Создаёт задачи для дат, на которые повторений ещё не было, вплоть до upTo.</summary>
    public IReadOnlyList<TaskItem> Generate(DateOnly upTo)
    {
        if (upTo <= GeneratedUntil) return [];

        var created = DatesFrom(GeneratedUntil.AddDays(1), upTo)
            .Select(date => new TaskItem(UserId, Title, Type, date, DurationMinutes, Priority, Id))
            .ToList();
        GeneratedUntil = upTo;
        return created;
    }

    /// <summary>Правило изменилось: повторения после date будут созданы заново.</summary>
    public void RestartAfter(DateOnly date) => GeneratedUntil = date;

    private static readonly DayOfWeek[] AllDays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private static int Bit(DayOfWeek day) => 1 << (((int)day + 6) % 7);
}
