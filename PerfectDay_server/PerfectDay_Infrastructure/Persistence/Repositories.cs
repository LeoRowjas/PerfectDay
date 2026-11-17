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
        db.Users.Include(u => u.ExternalLogins).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.Include(u => u.ExternalLogins).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByExternalLoginAsync(ExternalProvider provider, string subject, CancellationToken ct) =>
        db.Users.Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.ExternalLogins.Any(l => l.Provider == provider && l.Subject == subject), ct);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class TaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> ListAsync(Guid userId, TaskType? type, CancellationToken ct)
    {
        var query = db.Tasks.Include(t => t.Series).Where(t => t.UserId == userId);
        if (type is not null) query = query.Where(t => t.Type == type);
        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.CreatedAt).ToListAsync(ct);
    }

    public Task<TaskItem?> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.Tasks.Include(t => t.Series).FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> ListPlannableAsync(Guid userId, DateOnly date, bool includeOverdue, CancellationToken ct) =>
        await db.Tasks
            .Where(t => t.UserId == userId && !t.IsCompleted && (includeOverdue ? t.DueDate <= date : t.DueDate == date))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> ListOpenInSeriesAsync(Guid seriesId, DateOnly from, CancellationToken ct) =>
        await db.Tasks.Where(t => t.SeriesId == seriesId && !t.IsCompleted && t.DueDate >= from).ToListAsync(ct);

    public async Task<IReadOnlyList<TaskSeries>> ListSeriesBehindAsync(Guid userId, DateOnly upTo, CancellationToken ct) =>
        await db.TaskSeries
            .Where(s => s.UserId == userId && s.GeneratedUntil < upTo && (s.EndDate == null || s.GeneratedUntil < s.EndDate))
            .ToListAsync(ct);

    public Task<TaskSeries?> GetSeriesAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.TaskSeries.FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id, ct);

    public void Add(TaskItem task) => db.Tasks.Add(task);
    public void AddRange(IEnumerable<TaskItem> tasks) => db.Tasks.AddRange(tasks);
    public void Remove(TaskItem task) => db.Tasks.Remove(task);
    public void AddSeries(TaskSeries series) => db.TaskSeries.Add(series);
    public void RemoveSeries(TaskSeries series) => db.TaskSeries.Remove(series);
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

    public async Task<IReadOnlyList<DaySchedule>> ListRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Schedules
            .Include(s => s.Blocks).ThenInclude(b => b.Task)
            .Where(s => s.UserId == userId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

    public void Add(DaySchedule schedule) => db.Schedules.Add(schedule);

    public async Task RemoveFromDateAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        db.Schedules.RemoveRange(await db.Schedules.Where(s => s.UserId == userId && s.Date >= date).ToListAsync(ct));
}

internal sealed class NotificationReadRepository(AppDbContext db) : INotificationReadRepository
{
    public async Task<IReadOnlySet<string>> ListReadKeysAsync(Guid userId, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return new HashSet<string>();
        var read = await db.NotificationReads
            .Where(r => r.UserId == userId && keys.Contains(r.Key))
            .Select(r => r.Key)
            .ToListAsync(ct);
        return read.ToHashSet();
    }

    public void Add(NotificationRead read) => db.NotificationReads.Add(read);
}

internal sealed class CalendarRepository(AppDbContext db) : ICalendarRepository
{
    public async Task<IReadOnlyList<CalendarConnection>> ListConnectionsAsync(Guid userId, CancellationToken ct) =>
        await db.CalendarConnections.Where(c => c.UserId == userId).ToListAsync(ct);

    public Task<CalendarConnection?> GetConnectionAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.CalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id, ct);

    public async Task<IReadOnlyList<ExternalEvent>> ListEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.ExternalEvents
            .Where(e => e.UserId == userId && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date).ThenBy(e => e.Start)
            .ToListAsync(ct);

    public async Task ReplaceEventsAsync(CalendarConnection connection, IEnumerable<ExternalEvent> events, CancellationToken ct)
    {
        db.ExternalEvents.RemoveRange(await db.ExternalEvents.Where(e => e.ConnectionId == connection.Id).ToListAsync(ct));
        db.ExternalEvents.AddRange(events);
    }

    public void Add(CalendarConnection connection) => db.CalendarConnections.Add(connection);
    public void Remove(CalendarConnection connection) => db.CalendarConnections.Remove(connection);
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
