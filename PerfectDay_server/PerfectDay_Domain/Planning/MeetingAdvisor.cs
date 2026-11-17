using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Planning;

public sealed record Meeting(string Title, TimeRange Range);

public sealed record MeetingAdvice(string Title, TimeRange Current, TimeRange Better, int CurrentPercent, int BetterPercent);

/// <summary>Встречи нагружают эмоциональный цикл: ищет встречу, которую стоит сдвинуть на подъём.</summary>
public static class MeetingAdvisor
{
    private const int StepMinutes = 15;
    private const int MinGainPercent = 15;

    public static MeetingAdvice? Suggest(EnergyProfile profile, TimeRange workday, IReadOnlyList<Meeting> meetings)
    {
        MeetingAdvice? best = null;
        foreach (var meeting in meetings.Where(m => m.Range.Start >= workday.Start && m.Range.End <= workday.End))
        {
            var current = Percent(profile, meeting.Range);
            var others = meetings.Where(m => m != meeting).Select(m => m.Range).ToList();

            TimeRange? better = null;
            var betterPercent = current;
            for (var start = workday.Start; start + meeting.Range.Length <= workday.End; start += StepMinutes)
            {
                var candidate = new TimeRange(start, start + meeting.Range.Length);
                if (others.Any(candidate.Overlaps)) continue;

                var percent = Percent(profile, candidate);
                if (percent <= betterPercent) continue;
                better = candidate;
                betterPercent = percent;
            }

            if (better is not { } range || betterPercent - current < MinGainPercent) continue;
            if (best is null || betterPercent - current > best.BetterPercent - best.CurrentPercent)
                best = new MeetingAdvice(meeting.Title, meeting.Range, range, current, betterPercent);
        }

        return best;
    }

    private static int Percent(EnergyProfile profile, TimeRange range) =>
        BiorhythmCalculator.Percent(profile.AverageEnergy(BiorhythmCycle.Emotional, range));
}
