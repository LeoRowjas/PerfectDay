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
    TimeOnly WorkdayEnd,
    DateTime CreatedAt,
    ReminderSettingsDto Reminders,
    bool HasPassword,
    IReadOnlyList<ExternalProvider> ExternalLogins);

public sealed record ReminderSettingsDto(
    [Required] bool? BeforeTask,
    [Required] bool? Rest,
    [Required] bool? Browser);

public sealed record UpdateProfileRequest(
    DateOnly? BirthDate,
    Chronotype? Chronotype,
    [Required(ErrorMessage = "Укажите начало дня.")] TimeOnly? WorkdayStart,
    [Required(ErrorMessage = "Укажите конец дня.")] TimeOnly? WorkdayEnd);

/// <summary>Правило повтора: каждый день или по дням недели, до даты или без окончания.</summary>
public sealed record RepeatDto(
    [Required(ErrorMessage = "Выберите, как повторять.")] RecurrenceKind? Kind,
    IReadOnlyList<DayOfWeek>? Days,
    DateOnly? Until);

/// <param name="Repeat">Правило серии, если задача — повторение периодической.</param>
/// <param name="IsFromCalendar">Задача создана из события календаря с пометкой «#задача».</param>
public sealed record TaskDto(
    Guid Id,
    string Title,
    TaskType Type,
    DateOnly DueDate,
    int DurationMinutes,
    TaskPriority Priority,
    bool IsCompleted,
    Guid? SeriesId,
    RepeatDto? Repeat,
    bool IsFromCalendar);

public sealed record SetTaskCompletedRequest([Required] bool? IsCompleted);

public sealed record SetTaskDayRequest([Required(ErrorMessage = "Укажите день.")] DateOnly? DueDate);

public sealed record SaveTaskRequest(
    [Required(ErrorMessage = "Укажите название.")]
    [MaxLength(200, ErrorMessage = "Название должно быть не длиннее 200 символов.")]
    string Title,
    [Required(ErrorMessage = "Выберите тип задачи.")] TaskType? Type,
    [Required(ErrorMessage = "Укажите срок.")] DateOnly? DueDate,
    [Required(ErrorMessage = "Укажите длительность.")]
    [Range(5, 720, ErrorMessage = "Длительность должна быть от 5 минут до 12 часов.")]
    int? DurationMinutes,
    [Required(ErrorMessage = "Выберите приоритет.")] TaskPriority? Priority,
    RepeatDto? Repeat = null);

public sealed record CycleValueDto(
    BiorhythmCycle Cycle,
    int PeriodDays,
    int DayOfCycle,
    int Percent,
    CyclePhase Phase,
    bool IsCritical);

public sealed record RecommendationDto(
    TaskType Activity,
    string Title,
    TimeOnly Start,
    TimeOnly End,
    string Text,
    int EnergyFrom,
    int EnergyTo,
    bool IsDeclining);

public sealed record LoadActionDto(
    LoadActionKind Kind,
    TimeOnly? Start,
    int? DurationMinutes,
    Guid? TaskId,
    DateOnly? TargetDate);

public sealed record LoadWarningDto(
    string Id,
    LoadWarningKind Kind,
    WarningSeverity Severity,
    string Title,
    string Message,
    LoadActionDto? Action);

/// <param name="IsFirstVisit">Расписание ещё ни разу не строилось: дашборд показывает быстрый старт.</param>
public sealed record DashboardDto(
    DateOnly Date,
    bool IsProfileComplete,
    bool IsFirstVisit,
    Chronotype? Chronotype,
    int? DaysLived,
    IReadOnlyList<CycleValueDto> Cycles,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<LoadWarningDto> Warnings);

public sealed record GoodPeriodDto(BiorhythmCycle Cycle, DateOnly From, DateOnly To, int MaxPercent);

public sealed record CriticalDateDto(DateOnly Date, IReadOnlyList<BiorhythmCycle> Cycles);

public sealed record ForecastDto(DateOnly From, DateOnly To, IReadOnlyList<GoodPeriodDto> GoodPeriods, IReadOnlyList<CriticalDateDto> CriticalDates);

public enum ChartScale
{
    Day,
    Week,
    Month
}

public sealed record ChartPointDto(string At, int Physical, int Emotional, int Intellectual, bool IsCritical);

public sealed record ChartDto(ChartScale Scale, IReadOnlyList<ChartPointDto> Points);

/// <param name="EnergyPercent">Средняя энергия цикла задачи в это время; у рутины и перерывов её нет.</param>
public sealed record ScheduleBlockDto(
    Guid Id,
    Guid? TaskId,
    BlockKind Kind,
    string Title,
    TaskType? TaskType,
    TaskPriority? Priority,
    TimeOnly Start,
    TimeOnly End,
    string Explanation,
    int? EnergyPercent,
    bool IsPinned,
    bool IsCompleted,
    int Part,
    int PartsTotal);

public sealed record UnplacedTaskDto(Guid Id, string Title, TaskType Type, DateOnly DueDate, int DurationMinutes);

public sealed record LoadSummaryDto(
    int HardMinutes,
    int WorkdayMinutes,
    int LongestSeriesMinutes,
    int LongestSeriesCount,
    int RestMinutes,
    bool IsOverloaded);

public sealed record ScheduleDto(
    DateOnly Date,
    bool IsBuilt,
    bool IsLightened,
    TimeOnly WorkdayStart,
    TimeOnly WorkdayEnd,
    IReadOnlyList<ScheduleBlockDto> Blocks,
    IReadOnlyList<UnplacedTaskDto> UnplacedTasks,
    IReadOnlyList<LoadWarningDto> Warnings,
    LoadSummaryDto Load);

public sealed record MoveBlockRequest([Required(ErrorMessage = "Укажите новое время.")] TimeOnly? Start);

public enum BreakKind
{
    Rest,
    Walk
}

public sealed record AddBreakRequest(
    [Required(ErrorMessage = "Укажите время перерыва.")] TimeOnly? Start,
    [Required(ErrorMessage = "Укажите длительность перерыва.")]
    [Range(5, 120, ErrorMessage = "Перерыв должен быть от 5 минут до 2 часов.")]
    int? DurationMinutes,
    [Required] BreakKind? Kind);

public sealed record SetLightenedRequest([Required] bool? IsLightened);

public sealed record DismissWarningRequest([Required, MaxLength(100)] string WarningId);

/// <summary>Блок задачи до и после переноса; у блоков, которые остаются на месте, From и To совпадают.</summary>
public sealed record BlockChangeDto(string Title, TimeOnly From, TimeOnly To);

/// <summary>Что будет, если перенести блок: энергия на новом и старом месте и как сдвинется остаток дня.</summary>
public sealed record MovePreviewDto(
    TimeOnly Start,
    TimeOnly End,
    int? EnergyPercent,
    int? PreviousEnergyPercent,
    string? Warning,
    IReadOnlyList<BlockChangeDto> Changes);

public sealed record NotificationDto(
    string Id,
    NotificationKind Kind,
    string Title,
    string Text,
    TimeOnly At,
    bool IsRead,
    bool IsUpcoming);

public sealed record NotificationsDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public sealed record MarkNotificationsReadRequest([Required, MaxLength(50)] IReadOnlyList<string> Ids);

public enum AnalyticsPeriod
{
    Week,
    Month
}

public sealed record DayStatDto(DateOnly Date, int Planned, int Done, bool IsCritical);

public sealed record TypeStatDto(TaskType Type, int Planned, int Done, int Percent);

public sealed record InsightDto(string Kind, WarningSeverity Severity, string Title, string Text);

/// <param name="PeakPercent">Доля выполненных сложных задач, которые стояли на подъёме своего цикла.</param>
/// <param name="UpcomingCriticalDates">Критические дни на две недели вперёд: их можно облегчить кнопкой.</param>
public sealed record AnalyticsDto(
    AnalyticsPeriod Period,
    DateOnly From,
    DateOnly To,
    int Planned,
    int Done,
    int Percent,
    int? PreviousPercent,
    int HardDone,
    int? PeakPercent,
    int MovedManually,
    TaskType? MostMovedType,
    int CriticalPlanned,
    int CriticalDone,
    int? CriticalPercent,
    IReadOnlyList<DayStatDto> Days,
    IReadOnlyList<TypeStatDto> Types,
    IReadOnlyList<InsightDto> Insights,
    IReadOnlyList<DateOnly> UpcomingCriticalDates,
    IReadOnlyList<DateOnly> LightenedDates);

public sealed record ApplyRecommendationsDto(IReadOnlyList<DateOnly> LightenedDates);

public sealed record CalendarConnectionDto(
    Guid Id,
    CalendarProvider Provider,
    string? Name,
    string UrlHint,
    bool ImportAsBusy,
    bool CreateTasksFromTagged,
    DateTime? LastSyncedAt,
    string? LastError);

public enum ImportedEventEffect
{
    /// <summary>Время занято, задачи стоят вокруг.</summary>
    Busy,
    /// <summary>Вне рабочего дня или на весь день: на расписание не влияет.</summary>
    Outside,
    /// <summary>Из события создана задача.</summary>
    Task,
    /// <summary>Импорт занятого времени выключен.</summary>
    Ignored
}

public sealed record ImportedEventDto(
    string Title,
    CalendarProvider Provider,
    TimeOnly? Start,
    TimeOnly? End,
    bool IsAllDay,
    ImportedEventEffect Effect,
    string Note);

public sealed record MeetingAdviceDto(string Title, string Text, TimeOnly Start, TimeOnly End);

public sealed record CalendarsDto(
    IReadOnlyList<CalendarConnectionDto> Connections,
    DateOnly Date,
    IReadOnlyList<ImportedEventDto> Events,
    MeetingAdviceDto? Advice);

public sealed record ConnectCalendarRequest(
    [Required(ErrorMessage = "Выберите календарь.")] CalendarProvider? Provider,
    [Required(ErrorMessage = "Вставьте ссылку на календарь.")]
    [MaxLength(2000, ErrorMessage = "Слишком длинная ссылка.")]
    string Url,
    [Required, MaxLength(64)] string TimeZone);

public sealed record CalendarSettingsRequest(
    [Required] bool? ImportAsBusy,
    [Required] bool? CreateTasksFromTagged);

public sealed record SyncCalendarsRequest([Required, MaxLength(64)] string TimeZone, bool Force = false);

public sealed record ExternalProviderDto(ExternalProvider Provider, bool IsEnabled);
