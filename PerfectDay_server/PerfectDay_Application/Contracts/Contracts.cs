using System.ComponentModel.DataAnnotations;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Contracts;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [MaxLength(254, ErrorMessage = "Слишком длинный email.")]
    string Email,
    [Required(ErrorMessage = "Укажите пароль.")]
    [MinLength(8, ErrorMessage = "Пароль должен быть не короче 8 символов.")]
    [MaxLength(128, ErrorMessage = "Пароль должен быть не длиннее 128 символов.")]
    string Password);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Укажите email.")] string Email,
    [Required(ErrorMessage = "Укажите пароль.")] string Password);

public sealed record AuthResult(ProfileDto Profile, string Token);

public sealed record ProfileDto(
    Guid Id,
    string Email,
    DateOnly? BirthDate,
    Chronotype? Chronotype,
    TimeOnly WorkdayStart,
    TimeOnly WorkdayEnd);

public sealed record UpdateProfileRequest(
    DateOnly? BirthDate,
    Chronotype? Chronotype,
    [Required(ErrorMessage = "Укажите начало дня.")] TimeOnly? WorkdayStart,
    [Required(ErrorMessage = "Укажите конец дня.")] TimeOnly? WorkdayEnd);

public sealed record TaskDto(
    Guid Id,
    string Title,
    TaskType Type,
    DateOnly DueDate,
    int DurationMinutes,
    TaskPriority Priority,
    bool IsCompleted);

public sealed record SetTaskCompletedRequest([Required] bool? IsCompleted);

public sealed record SaveTaskRequest(
    [Required(ErrorMessage = "Укажите название.")]
    [MaxLength(200, ErrorMessage = "Название должно быть не длиннее 200 символов.")]
    string Title,
    [Required(ErrorMessage = "Выберите тип задачи.")] TaskType? Type,
    [Required(ErrorMessage = "Укажите срок.")] DateOnly? DueDate,
    [Required(ErrorMessage = "Укажите длительность.")]
    [Range(5, 720, ErrorMessage = "Длительность должна быть от 5 минут до 12 часов.")]
    int? DurationMinutes,
    [Required(ErrorMessage = "Выберите приоритет.")] TaskPriority? Priority);

public sealed record CycleValueDto(BiorhythmCycle Cycle, int PeriodDays, int Percent, bool IsCritical);

public sealed record RecommendationDto(TaskType Activity, string Title, TimeOnly Start, TimeOnly End, string Text);

public sealed record LoadWarningDto(LoadWarningKind Kind, WarningSeverity Severity, string Message);

public sealed record DashboardDto(
    DateOnly Date,
    bool IsProfileComplete,
    IReadOnlyList<CycleValueDto> Cycles,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<LoadWarningDto> Warnings);

public enum ChartScale
{
    Day,
    Week,
    Month
}

public sealed record ChartPointDto(string At, int Physical, int Emotional, int Intellectual, bool IsCritical);

public sealed record ChartDto(ChartScale Scale, IReadOnlyList<ChartPointDto> Points);

public sealed record ScheduleBlockDto(
    Guid Id,
    Guid? TaskId,
    BlockKind Kind,
    string Title,
    TaskType? TaskType,
    TimeOnly Start,
    TimeOnly End,
    string Explanation,
    bool IsPinned,
    bool IsCompleted,
    int Part,
    int PartsTotal);

public sealed record UnplacedTaskDto(Guid Id, string Title, TaskType Type, DateOnly DueDate, int DurationMinutes);

public sealed record ScheduleDto(
    DateOnly Date,
    bool IsBuilt,
    TimeOnly WorkdayStart,
    TimeOnly WorkdayEnd,
    IReadOnlyList<ScheduleBlockDto> Blocks,
    IReadOnlyList<UnplacedTaskDto> UnplacedTasks,
    IReadOnlyList<LoadWarningDto> Warnings);

public sealed record MoveBlockRequest([Required(ErrorMessage = "Укажите новое время.")] TimeOnly? Start);
