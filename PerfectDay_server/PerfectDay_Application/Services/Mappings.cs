using PerfectDay_Application.Contracts;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

internal static class Mappings
{
    public static ProfileDto ToDto(this User user) =>
        new(user.Id, user.Email, user.BirthDate, user.Chronotype, user.WorkdayStart, user.WorkdayEnd);

    public static TaskDto ToDto(this TaskItem task) =>
        new(task.Id, task.Title, task.Type, task.DueDate, task.DurationMinutes, task.Priority, task.IsCompleted);

    public static LoadWarningDto ToDto(this LoadWarning warning) =>
        new(warning.Kind, warning.Severity, warning.Message);
}
