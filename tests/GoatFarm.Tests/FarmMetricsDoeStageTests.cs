using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class FarmMetricsDoeStageTests
{
    private static Goat Doe(Action<Goat>? configure = null)
    {
        var g = new Goat
        {
            Tag = "D-TEST",
            Gender = GoatGender.Female,
            Status = GoatStatus.Milking,
            Breed = "Tedda"
        };
        configure?.Invoke(g);
        return g;
    }

    [Fact]
    public void Confirmed_pregnant_doe_shows_due_and_dry_off_job()
    {
        var today = new DateOnly(2026, 11, 22);
        var mated = new DateOnly(2026, 11, 17);
        var g = Doe(d =>
        {
            d.MatedDate = mated;
            d.KidsCount = 2;
        });

        var result = DoeStageResolver.Resolve(g, null, null, today);

        Assert.Equal("pregnant", result.StatusKey);
        Assert.True(result.IsPregnant);
        Assert.Equal(BreedingHelper.ExpectedKidding(mated), result.DueDate);
        Assert.Contains("Dry off", result.NextJob ?? "");
    }

    [Fact]
    public void Recent_kidding_within_60_days_is_kidded_nursing_not_getting_ready()
    {
        var kidDate = new DateOnly(2026, 10, 1);
        var today = new DateOnly(2026, 11, 22);
        var g = Doe(d => d.Status = GoatStatus.Milking);
        var kid = new KiddingRecord { DoeId = g.Id, KiddingDate = kidDate, LiveKids = 2 };

        var result = DoeStageResolver.Resolve(g, null, kid, today);

        Assert.Equal("kidded", result.StatusKey);
        Assert.Equal(FarmDoeStage.KiddedNursing, result.Stage);
    }

    [Fact]
    public void Summarize_counts_groups_waiting_and_rescan_together()
    {
        var counts = new Dictionary<FarmDoeStage, int>
        {
            [FarmDoeStage.WaitingScan] = 2,
            [FarmDoeStage.RescanDue] = 1,
            [FarmDoeStage.Pregnant] = 3,
            [FarmDoeStage.Dry] = 1
        };

        var s = DoeStageResolver.SummarizeCounts(counts);

        Assert.Equal(3, s.WaitingScan);
        Assert.Equal(4, s.Pregnant);
    }
}