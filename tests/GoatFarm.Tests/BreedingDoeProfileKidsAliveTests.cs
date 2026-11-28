using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class BreedingDoeProfileKidsAliveTests
{
    [Fact]
    public void Build_KidsAlive_SumsLiveKidsFromKiddingRecords_NotCallerCount()
    {
        var doe = new Goat
        {
            Id = 3,
            Tag = "D3",
            Breed = "Beetal",
            Gender = GoatGender.Female,
            Status = GoatStatus.Dry
        };
        var kiddings = new List<KiddingRecord>
        {
            new() { DoeId = 3, KiddingDate = TestDb.Today.AddDays(-400), LiveKids = 2, Stillborn = 1 },
            new() { DoeId = 3, KiddingDate = TestDb.Today.AddDays(-50), LiveKids = 3, Stillborn = 0 }
        };

        var vm = BreedingDoeProfileBuilder.Build(
            doe,
            [],
            kiddings,
            kidsAlive: 99, // wrong live-goat count must be ignored when records exist
            ageLabelFromDays: d => d + "d",
            today: TestDb.Today);

        Assert.Equal(6, vm.KidsBorn); // (2+1) + 3
        Assert.Equal(5, vm.KidsAlive); // 2+3 LiveKids
    }
}
