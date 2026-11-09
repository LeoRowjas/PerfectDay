using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Tests;

public class BiorhythmCalculatorTests
{
    private static readonly DateOnly Birth = new(2000, 1, 1);

    [Theory]
    [InlineData(BiorhythmCycle.Physical, 23)]
    [InlineData(BiorhythmCycle.Emotional, 28)]
    [InlineData(BiorhythmCycle.Intellectual, 33)]
    public void Value_RepeatsEveryPeriod(BiorhythmCycle cycle, int period)
    {
        var date = Birth.AddDays(1000);
        Assert.Equal(
            BiorhythmCalculator.Value(Birth, date, cycle),
            BiorhythmCalculator.Value(Birth, date.AddDays(period), cycle), 9);
    }

    [Fact]
    public void Emotional_PeaksOnSeventhDay()
    {
        Assert.Equal(1, BiorhythmCalculator.Value(Birth, Birth.AddDays(7), BiorhythmCycle.Emotional), 9);
        Assert.Equal(-1, BiorhythmCalculator.Value(Birth, Birth.AddDays(21), BiorhythmCycle.Emotional), 9);
    }

    [Fact]
    public void BirthDate_IsTripleCritical() =>
        Assert.Equal(3, BiorhythmCalculator.CriticalCycles(Birth, Birth).Count);

    [Fact]
    public void Critical_WhenValueIsExactlyZero()
    {
        Assert.True(BiorhythmCalculator.IsCritical(Birth, Birth.AddDays(14), BiorhythmCycle.Emotional));
        Assert.False(BiorhythmCalculator.IsCritical(Birth, Birth.AddDays(13), BiorhythmCycle.Emotional));
    }

    [Fact]
    public void Critical_WhenCycleCrossesZeroBeforeNextDay()
    {
        // Период 23: между 11-м и 12-м днём значение меняет знак с + на −.
        Assert.True(BiorhythmCalculator.IsCritical(Birth, Birth.AddDays(11), BiorhythmCycle.Physical));
        Assert.False(BiorhythmCalculator.IsCritical(Birth, Birth.AddDays(12), BiorhythmCycle.Physical));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1234)]
    [InlineData(9876)]
    public void Value_StaysInRange(int days)
    {
        foreach (var cycle in BiorhythmCalculator.Cycles)
        {
            var value = BiorhythmCalculator.Value(Birth, Birth.AddDays(days), cycle);
            Assert.InRange(value, -1, 1);
        }
    }
}
