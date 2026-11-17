using PerfectDay_Application.Contracts;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

internal static class Mappings
{
    public static ProfileDto ToDto(this User user) =>
        new(user.Id, user.Email, user.BirthDate, user.Chronotype, user.WorkdayStart, user.WorkdayEnd, user.CreatedAt,
            new ReminderSettingsDto(user.RemindBeforeTask, user.RestReminders, user.BrowserNotifications),
            user.PasswordHash is not null,
            user.ExternalLogins.Select(l => l.Provider).Order().ToList());

    public static TaskDto ToDto(this TaskItem task) =>
        new(task.Id, task.Title, task.Type, task.DueDate, task.DurationMinutes, task.Priority, task.IsCompleted,
            task.SeriesId, task.Series?.ToRepeatDto(), task.ExternalKey is not null);

    public static RepeatDto ToRepeatDto(this TaskSeries series) => new(series.Kind, series.Days, series.EndDate);

    public static LoadWarningDto ToDto(this LoadWarning warning) =>
        new(warning.Id, warning.Kind, warning.Severity, warning.Title, warning.Message, warning.Action?.ToDto());

    private static LoadActionDto ToDto(this LoadAction action) =>
        new(action.Kind, action.Start is { } start ? TimeRange.FromMinutes(start) : null, action.DurationMinutes,
            action.TaskId, action.TargetDate);
}
