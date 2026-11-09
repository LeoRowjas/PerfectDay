using Microsoft.EntityFrameworkCore;
using PerfectDay_Domain.Entities;

namespace PerfectDay_Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<DaySchedule> Schedules => Set<DaySchedule>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Email).HasMaxLength(254);
            user.Property(u => u.Chronotype).HasConversion<string>().HasMaxLength(16);
            user.HasIndex(u => u.Email).IsUnique();
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
        });

        modelBuilder.Entity<DaySchedule>(schedule =>
        {
            schedule.ToTable("day_schedules");
            schedule.Property(s => s.Id).ValueGeneratedNever();
            schedule.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            schedule.HasIndex(s => new { s.UserId, s.Date }).IsUnique();
            schedule.HasMany(s => s.Blocks).WithOne().HasForeignKey(b => b.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            schedule.Navigation(s => s.Blocks).HasField("_blocks").UsePropertyAccessMode(PropertyAccessMode.Field);
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
