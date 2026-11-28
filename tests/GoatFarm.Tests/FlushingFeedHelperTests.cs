using GoatFarm.Application.Common;

namespace GoatFarm.Tests;

public class FlushingFeedHelperTests
{
    [Fact]
    public void FindSecondGroup_MatchesNameCaseInsensitive()
    {
        var season = new BreedingSeasonSettings
        {
            Batches =
            [
                new BreedingSeasonBatch { Id = "b1", Name = "Main group", From = "09-01", To = "09-15" },
                new BreedingSeasonBatch { Id = "b2", Name = "second group", From = "03-01", To = "03-15" }
            ]
        };

        var second = FlushingFeedHelper.FindSecondGroup(season);
        Assert.NotNull(second);
        Assert.Equal("b2", second!.Id);
        Assert.True(FlushingFeedHelper.IsSecondGroup(second));
    }
}
