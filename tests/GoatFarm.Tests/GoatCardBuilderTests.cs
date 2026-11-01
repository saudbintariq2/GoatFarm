using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class GoatCardBuilderTests
{
    [Fact]
    public void CollectPedigreeNodes_includes_subject_and_parents()
    {
        var subject = new Goat { Id = 1, Tag = "100", Gender = GoatGender.Female, Breed = "Beetal", SireId = 2, DamId = 3 };
        var sire = new Goat { Id = 2, Tag = "S1", Gender = GoatGender.Male, Breed = "Beetal" };
        var dam = new Goat { Id = 3, Tag = "D1", Gender = GoatGender.Female, Breed = "Beetal" };
        var byId = new Dictionary<int, Goat> { [1] = subject, [2] = sire, [3] = dam };

        var nodes = GoatCardBuilder.CollectPedigreeNodes(subject, byId, 3);

        Assert.Contains(nodes, n => n.Id == 1);
        Assert.Contains(nodes, n => n.Id == 2);
        Assert.Contains(nodes, n => n.Id == 3);
    }

    [Fact]
    public void Siblings_lists_same_dam_excluding_self()
    {
        var damId = 10;
        var subject = new Goat { Id = 1, Tag = "K1", Gender = GoatGender.Female, DamId = damId, Source = GoatSource.Born, EventDate = new DateOnly(2025, 1, 1) };
        var sib = new Goat { Id = 2, Tag = "K2", Gender = GoatGender.Male, DamId = damId, Source = GoatSource.Born, EventDate = new DateOnly(2025, 1, 1) };
        var other = new Goat { Id = 3, Tag = "X", Gender = GoatGender.Female, DamId = 99, Source = GoatSource.Born, EventDate = new DateOnly(2025, 2, 1) };

        var sibs = GoatCardBuilder.Siblings(subject, [subject, sib, other]);

        Assert.Single(sibs);
        Assert.Equal("K2", sibs[0].Tag);
        Assert.True(sibs[0].Twin);
    }
}
