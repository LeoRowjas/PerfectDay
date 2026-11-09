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
    Break
}

public enum LoadWarningKind
{
    CriticalDay,
    LongHardSeries,
    TooMuchHardWork,
    TaskOnCycleDecline
}

public enum WarningSeverity
{
    Info,
    Warning,
    Critical
}
