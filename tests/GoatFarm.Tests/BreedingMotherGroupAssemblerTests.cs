using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class BreedingMotherGroupAssemblerTests
{
    [Fact]
    public void BuildRows_UsesEffectiveGroupId_WhenStoredNull()
    {
        var doe = new Goat
        {
            Id = 7,
            Tag = "D7",
            Breed = "Beetal",
            Gender = GoatGender.Female,
            MotherGroupId = null
        };
        var kidding = new KiddingRecord
        {
            Id = 1,
            DoeId = 7,
            KiddingDate = TestDb.Today.AddDays(-40),
            LiveKids = 3,
            Stillborn = 0
        };

        var rows = BreedingMotherGroupAssembler.BuildRows(
            [doe],
            new Dictionary<int, int> { [7] = 1 },
            new Dictionary<int, KiddingRecord> { [7] = kidding },
            new Dictionary<int, KiddingRecord[]> { [7] = [kidding] });

        Assert.Single(rows);
        Assert.Equal("m3", rows[0].MotherGroupId);
        Assert.Null(rows[0].SuggestedGroupId);
    }

    [Fact]
    public void BuildRows_PrefersStoredGroupOverLastLitter()
    {
        var doe = new Goat
        {
            Id = 8,
            Tag = "D8",
            Breed = "Beetal",
            Gender = GoatGender.Female,
            MotherGroupId = "m1"
        };
        var kidding = new KiddingRecord
        {
            Id = 2,
            DoeId = 8,
            KiddingDate = TestDb.Today.AddDays(-10),
            LiveKids = 4,
            Stillborn = 0
        };

        var rows = BreedingMotherGroupAssembler.BuildRows(
            [doe],
            new Dictionary<int, int> { [8] = 1 },
            new Dictionary<int, KiddingRecord> { [8] = kidding },
            new Dictionary<int, KiddingRecord[]> { [8] = [kidding] });

        Assert.Equal("m1", rows[0].MotherGroupId);
    }
}
