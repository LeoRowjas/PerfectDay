using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    void Add(User user);
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> ListAsync(Guid userId, TaskType? type, CancellationToken ct);
    Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct);

    /// <summary>Невыполненные задачи со сроком на дату; при includeOverdue ещё и просроченные.</summary>
    Task<IReadOnlyList<TaskItem>> ListPlannableAsync(Guid userId, DateOnly date, bool includeOverdue, CancellationToken ct);

    void Add(TaskItem task);
    void Remove(TaskItem task);
}

public interface IScheduleRepository
{
    Task<DaySchedule?> GetAsync(Guid userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DaySchedule>> ListContainingTaskAsync(Guid taskId, CancellationToken ct);
    void Add(DaySchedule schedule);
    Task RemoveFromDateAsync(Guid userId, DateOnly date, CancellationToken ct);
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
