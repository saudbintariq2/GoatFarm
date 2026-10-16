using GoatFarm.Application.Assistant;

namespace GoatFarm.Tests.Assistant;

public class AssistantDateRangeTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    [Fact]
    public void LastMonth_IsPreviousCalendarMonth()
    {
        var (from, to, monthKey) = AssistantDateRange.LastMonth(Today);
        Assert.Equal(new DateOnly(2026, 9, 1), from);
        Assert.Equal(new DateOnly(2026, 9, 30), to);
        Assert.Equal("2026-09", monthKey);
    }

    [Theory]
    [InlineData("lastmonth", "2026-09")]
    [InlineData("last month", "2026-09")]
    [InlineData("2026-09", "2026-09")]
    [InlineData(null, "2026-10")]
    [InlineData("this month", "2026-10")]
    public void TryParseMonth_ResolvesLastMonthAndCurrent(string? input, string expected)
    {
        Assert.True(AssistantDateRange.TryParseMonth(input, Today, out var key, out var error));
        Assert.Null(error);
        Assert.Equal(expected, key);
    }

    [Fact]
    public void TryParseInclusiveRange_RejectsInvertedAndOverlongRanges()
    {
        Assert.False(AssistantDateRange.TryParseInclusiveRange("2026-10-10", "2026-10-01", null, Today,
            out _, out _, out _, out var inverted));
        Assert.Contains("end date", inverted, StringComparison.OrdinalIgnoreCase);

        Assert.False(AssistantDateRange.TryParseInclusiveRange("2024-01-01", "2026-10-01", null, Today,
            out _, out _, out _, out var overlong));
        Assert.Contains("366", overlong);
    }

    [Fact]
    public void TryParseInclusiveRange_FromLastMonthToken()
    {
        Assert.True(AssistantDateRange.TryParseInclusiveRange(null, null, "lastmonth", Today,
            out var from, out var to, out _, out var error));
        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 9, 1), from);
        Assert.Equal(new DateOnly(2026, 9, 30), to);
    }

    [Fact]
    public void LastMonth_CrossesYearBoundary()
    {
        var (from, to, monthKey) = AssistantDateRange.LastMonth(new DateOnly(2026, 1, 5));
        Assert.Equal(new DateOnly(2025, 12, 1), from);
        Assert.Equal(new DateOnly(2025, 12, 31), to);
        Assert.Equal("2025-12", monthKey);
    }

    [Theory]
    [InlineData("10/01/2026", "2026-10-16")]
    [InlineData("2026-10-16T00:00:00", "2026-10-16")]
    [InlineData("October 16, 2026", "2026-10-16")]
    [InlineData("1999-12-31", "2026-10-01")]
    [InlineData("2101-01-01", "2026-10-16")]
    public void TryParseInclusiveRange_RejectsAmbiguousAndOutOfRangeDates(string from, string to)
    {
        Assert.False(AssistantDateRange.TryParseInclusiveRange(from, to, null, Today,
            out _, out _, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParseMonth_RejectsAmbiguousSeptember()
    {
        Assert.False(AssistantDateRange.TryParseMonth("September", Today, out _, out var error));
        Assert.Contains("yyyy-MM", error);
    }
}
