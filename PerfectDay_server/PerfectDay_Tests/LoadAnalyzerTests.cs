using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Tests;

public class LoadAnalyzerTests
{
    private static readonly DateOnly Birth = new(2000, 1, 1);

    private static LoadItem Item(TaskType type, int startHour, int endHour) =>
        new(type.ToString(), type, new TimeRange(startHour * 60, endHour * 60));

    [Fact]
    public void TripleCriticalDay_IsCriticalSeverity()
    {
        var warnings = LoadAnalyzer.CriticalDayWarnings(new EnergyProfile(Birth, Birth, Chronotype.Dove));
        var warning = Assert.Single(warnings);
        Assert.Equal(WarningSeverity.Critical, warning.Severity);
        Assert.Contains("Тройной", warning.Message);
    }

    [Fact]
    public void LongHardSeries_IsDetected()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
            [Item(TaskType.Intellectual, 9, 11), Item(TaskType.Creative, 11, 13)]);

        Assert.Contains(warnings, w => w.Kind == LoadWarningKind.LongHardSeries);
    }

    [Fact]
    public void RoutineBetweenHardTasks_DoesNotCountAsHardSeriesTime()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
            [Item(TaskType.Intellectual, 9, 11), Item(TaskType.Routine, 11, 13), Item(TaskType.Creative, 13, 15)]);

        Assert.DoesNotContain(warnings, w => w.Kind == LoadWarningKind.LongHardSeries);
    }

    [Fact]
    public void TaskOnCycleDecline_IsReported()
    {
        // 21-й день: эмоциональный цикл в нижней точке.
        var profile = new EnergyProfile(Birth, Birth.AddDays(21), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile, [Item(TaskType.Creative, 10, 11)]);

        Assert.Contains(warnings, w => w.Kind == LoadWarningKind.TaskOnCycleDecline);
    }
}
