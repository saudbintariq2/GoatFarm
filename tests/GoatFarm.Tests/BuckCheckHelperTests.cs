using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;

namespace GoatFarm.Tests;

public class BuckCheckHelperTests
{
    private static BuckHealthCheck Check(DateOnly date) => new() { BuckId = 1, CheckDate = date };

    [Fact]
    public void Evaluate_BuckInUpcoming_WithoutCheck_IsDue()
    {
        var today = new DateOnly(2026, 5, 1);
        var buckIn = today.AddDays(20);
        var status = BuckCheckHelper.Evaluate(null, today, buckIn);
        Assert.Equal(BuckCheckState.Due, status.State);
        Assert.Equal(buckIn.AddDays(-14), status.DueDate);
        Assert.Contains("needed", status.BucksLineHtml, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Buck check for", status.TodayTitle("BK-1"));
    }

    [Fact]
    public void Evaluate_BuckAlreadyIn_WithoutValidCheck_IsMissed()
    {
        var today = new DateOnly(2026, 5, 1);
        var buckIn = today.AddDays(-3);
        var stale = Check(buckIn.AddDays(-40));
        var status = BuckCheckHelper.Evaluate(stale, today, buckIn);
        Assert.Equal(BuckCheckState.Missed, status.State);
        Assert.Contains("missed", status.BucksLineHtml, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Buck check missed", status.TodayTitle("BK-1"));
    }

    [Fact]
    public void Evaluate_ValidCheckForBuckIn_IsDone_SameForTodayAndBucks()
    {
        var today = new DateOnly(2026, 5, 1);
        var buckIn = today.AddDays(10);
        var check = Check(today.AddDays(-5));
        var status = BuckCheckHelper.Evaluate(check, today, buckIn);
        Assert.Equal(BuckCheckState.Done, status.State);
        Assert.True(status.IsOk);
        Assert.Contains("done", status.BucksLineHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_NoBuckIn_UsesSixtyDayCurrency()
    {
        var today = new DateOnly(2026, 5, 1);
        Assert.Equal(BuckCheckState.Done, BuckCheckHelper.Evaluate(Check(today.AddDays(-30)), today).State);
        Assert.Equal(BuckCheckState.Missed, BuckCheckHelper.Evaluate(Check(today.AddDays(-70)), today).State);
        Assert.Equal(BuckCheckState.Due, BuckCheckHelper.Evaluate(null, today).State);
    }
}
