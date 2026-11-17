namespace PerfectDay_Domain.Enums;

public enum Chronotype
{
    Lark,
    Dove,
    Owl
}

public enum TaskType
{
    Intellectual,
    Physical,
    Creative,
    Routine
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum BiorhythmCycle
{
    Physical,
    Emotional,
    Intellectual
}

public enum BlockKind
{
    Task,
    Break,
    Event
}

public enum RecurrenceKind
{
    Daily,
    Weekly
}

/// <summary>Откуда импортирован календарь; все подключаются по ссылке iCal.</summary>
public enum CalendarProvider
{
    Google,
    Yandex,
    Apple
}

/// <summary>Сервисы, через которые можно войти без пароля.</summary>
public enum ExternalProvider
{
    Yandex,
    Vk,
    Google
}

public enum LoadWarningKind
{
    CriticalDay,
    LongHardSeries,
    TooMuchHardWork,
    TaskOnCycleDecline,
    SwitchActivity
}

/// <summary>Что можно сделать с предупреждением одной кнопкой.</summary>
public enum LoadActionKind
{
    AddBreak,
    InsertWalk,
    MoveTask,
    Lighten
}

public enum CyclePhase
{
    Rising,
    Falling,
    Peak,
    Trough,
    Critical
}

public enum NotificationKind
{
    PlanReady,
    TaskStart,
    BreakSoon,
    CriticalTomorrow
}

public enum WarningSeverity
{
    Info,
    Warning,
    Critical
}
