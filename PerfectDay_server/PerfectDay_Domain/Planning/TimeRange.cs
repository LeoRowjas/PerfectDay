using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Planning;

/// <summary>Полуоткрытый интервал времени внутри дня, в минутах от полуночи.</summary>
public readonly record struct TimeRange(int Start, int End)
{
    private const int SampleStepMinutes = 15;

    public int Length => End - Start;
    public TimeOnly StartTime => FromMinutes(Start);
    public TimeOnly EndTime => FromMinutes(End);

    public static TimeRange From(TimeOnly start, TimeOnly end) => new(ToMinutes(start), ToMinutes(end));
    public static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;
    public static TimeOnly FromMinutes(int minutes) => new(minutes / 60, minutes % 60);

    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;

    public double AverageSuitability(EnergyProfile profile, TaskType type)
    {
        var total = 0d;
        var samples = 0;
        for (var minute = Start; minute < End; minute += SampleStepMinutes)
        {
            total += profile.Suitability(type, FromMinutes(minute));
            samples++;
        }

        return samples == 0 ? double.MinValue : total / samples;
    }
}
