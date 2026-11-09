using Microsoft.EntityFrameworkCore;
using Npgsql;
using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Infrastructure.Persistence;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class TaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> ListAsync(Guid userId, TaskType? type, CancellationToken ct)
    {
        var query = db.Tasks.Where(t => t.UserId == userId);
        if (type is not null) query = query.Where(t => t.Type == type);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.CreatedAt).ToListAsync(ct);
    }

    public Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.Tasks.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> ListPlannableAsync(Guid userId, DateOnly date, bool includeOverdue, CancellationToken ct) =>
        await db.Tasks
            .Where(t => t.UserId == userId && !t.IsCompleted && (includeOverdue ? t.DueDate <= date : t.DueDate == date))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public void Add(TaskItem task) => db.Tasks.Add(task);
    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}

internal sealed class ScheduleRepository(AppDbContext db) : IScheduleRepository
{
    public Task<DaySchedule?> GetAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        db.Schedules
            .Include(s => s.Blocks).ThenInclude(b => b.Task)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date, ct);

    public async Task<IReadOnlyList<DaySchedule>> ListContainingTaskAsync(Guid taskId, CancellationToken ct) =>
        await db.Schedules
            .Include(s => s.Blocks)
            .Where(s => s.Blocks.Any(b => b.TaskId == taskId))
            .ToListAsync(ct);

    public void Add(DaySchedule schedule) => db.Schedules.Add(schedule);

    public async Task RemoveFromDateAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        db.Schedules.RemoveRange(await db.Schedules.Where(s => s.UserId == userId && s.Date >= date).ToListAsync(ct));
}

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private const string UniqueViolationCode = "23505";

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: UniqueViolationCode })
        {
            throw new ConflictException("Такая запись уже существует.");
        }
    }
}
