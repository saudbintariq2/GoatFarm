using GoatFarm.Application.Common;

namespace GoatFarm.Tests;

public class BreedingSeasonHelperTests
{
    [Fact]
    public void PickTodaySeasonBatchId_ReturnsEarliestQualifyingBatchOrFirst()
    {
        var today = new DateOnly(2026, 5, 1);
        var season = new BreedingSeasonSettings
        {
            BuildUp = 60,
            ScanAfter = 30,
            DryOff = 60,
            WeanDays = 90,
            WeanKg = 13,
            DryBeforeCross = 14,
            Batches =
            [
                new BreedingSeasonBatch { Id = "b1", Name = "Spring", From = "03-01", To = "03-15" },
                new BreedingSeasonBatch { Id = "b2", Name = "Autumn", From = "09-01", To = "09-20" }
            ]
        };

        var id = BreedingSeasonHelper.PickTodaySeasonBatchId(season, today);
        Assert.Contains(id, new[] { "b1", "b2" });
        var chosen = season.Batches.First(b => b.Id == id);
        var cross = BreedingSeasonHelper.SeasonPlan(chosen, season, today).CrossFrom;
        Assert.True(cross >= today.AddDays(-60));
    }
}
