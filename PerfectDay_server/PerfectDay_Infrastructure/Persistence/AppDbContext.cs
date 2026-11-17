using Microsoft.EntityFrameworkCore;
using PerfectDay_Domain.Entities;

namespace PerfectDay_Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<DaySchedule> Schedules => Set<DaySchedule>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<TaskSeries> TaskSeries => Set<TaskSeries>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<CalendarConnection> CalendarConnections => Set<CalendarConnection>();
    public DbSet<ExternalEvent> ExternalEvents => Set<ExternalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Email).HasMaxLength(254);
            user.Property(u => u.Chronotype).HasConversion<string>().HasMaxLength(16);
            user.HasIndex(u => u.Email).IsUnique();
            user.HasMany(u => u.ExternalLogins).WithOne().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            user.Navigation(u => u.ExternalLogins).HasField("_externalLogins").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ExternalLogin>(login =>
        {
            login.ToTable("external_logins");
            login.HasKey(l => new { l.Provider, l.Subject });
            login.Property(l => l.Provider).HasConversion<string>().HasMaxLength(16);
            login.Property(l => l.Subject).HasMaxLength(200);
            login.HasIndex(l => l.UserId);
        });

        modelBuilder.Entity<TaskSeries>(series =>
        {
            series.ToTable("task_series");
            series.Property(s => s.Id).ValueGeneratedNever();
            series.Property(s => s.Title).HasMaxLength(200);
            series.Property(s => s.Type).HasConversion<string>().HasMaxLength(16);
            series.Property(s => s.Priority).HasConversion<string>().HasMaxLength(16);
            series.Property(s => s.Kind).HasConversion<string>().HasMaxLength(16);
            series.Ignore(s => s.Days);
            series.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            series.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<CalendarConnection>(connection =>
        {
            connection.ToTable("calendar_connections");
            connection.Property(c => c.Id).ValueGeneratedNever();
            connection.Property(c => c.Provider).HasConversion<string>().HasMaxLength(16);
            connection.Property(c => c.Url).HasMaxLength(2000);
            connection.Property(c => c.TimeZone).HasMaxLength(64);
            connection.Property(c => c.Name).HasMaxLength(200);
            connection.Property(c => c.LastError).HasMaxLength(500);
            connection.Property(c => c.ImportedTaskKeys).HasDefaultValueSql("'{}'::text[]");
            connection.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            connection.HasIndex(c => new { c.UserId, c.Provider }).IsUnique();
        });

        modelBuilder.Entity<ExternalEvent>(item =>
        {
            item.ToTable("external_events");
            item.Property(e => e.Id).ValueGeneratedNever();
            item.Property(e => e.Uid).HasMaxLength(500);
            item.Property(e => e.Title).HasMaxLength(200);
            item.HasOne<CalendarConnection>().WithMany().HasForeignKey(e => e.ConnectionId).OnDelete(DeleteBehavior.Cascade);
            item.HasIndex(e => new { e.UserId, e.Date });
            item.HasIndex(e => e.ConnectionId);
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.ToTable("tasks");
            task.Property(t => t.Id).ValueGeneratedNever();
            task.Property(t => t.Title).HasMaxLength(200);
            task.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
            task.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
            task.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            task.HasIndex(t => new { t.UserId, t.DueDate });
            task.Property(t => t.ExternalKey).HasMaxLength(600);
            task.HasOne(t => t.Series).WithMany().HasForeignKey(t => t.SeriesId).OnDelete(DeleteBehavior.SetNull);
            task.HasIndex(t => t.SeriesId);
        });

        modelBuilder.Entity<DaySchedule>(schedule =>
        {
            schedule.ToTable("day_schedules");
            schedule.Property(s => s.Id).ValueGeneratedNever();
            schedule.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            schedule.HasIndex(s => new { s.UserId, s.Date }).IsUnique();
            schedule.HasMany(s => s.Blocks).WithOne().HasForeignKey(b => b.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            schedule.Navigation(s => s.Blocks).HasField("_blocks").UsePropertyAccessMode(PropertyAccessMode.Field);
            schedule.Property(s => s.DismissedWarnings).HasDefaultValueSql("'{}'::text[]");
        });

        modelBuilder.Entity<NotificationRead>(read =>
        {
            read.ToTable("notification_reads");
            read.HasKey(r => new { r.UserId, r.Key });
            read.Property(r => r.Key).HasMaxLength(100);
            read.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleBlock>(block =>
        {
            block.ToTable("schedule_blocks");
            block.Property(b => b.Id).ValueGeneratedNever();
            block.Property(b => b.Title).HasMaxLength(200);
            block.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16);
            block.Property(b => b.Explanation).HasMaxLength(1000);
            block.HasOne(b => b.Task).WithMany().HasForeignKey(b => b.TaskId).OnDelete(DeleteBehavior.Cascade);
            block.HasIndex(b => b.TaskId);
        });
    }
}
