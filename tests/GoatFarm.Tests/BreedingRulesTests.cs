using GoatFarm.Application.Common;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class BreedingRulesTests
{
    [Fact]
    public void EligibilityError_AllowsAdultDoe() =>
        Assert.Null(BreedingRules.EligibilityError(TestDb.Doe("D1", ageDays: 400), TestDb.Today));

    [Fact]
    public void EligibilityError_BlocksDoeYoungerThan300Days()
    {
        var error = BreedingRules.EligibilityError(TestDb.Doe("D1", ageDays: 200), TestDb.Today);
        Assert.Contains("200 days old", error);
    }

    [Fact]
    public void EligibilityError_BlocksMales() =>
        Assert.NotNull(BreedingRules.EligibilityError(TestDb.Buck("B1"), TestDb.Today));

    [Theory]
    [InlineData(GoatStatus.Meat)]
    [InlineData(GoatStatus.Sale)]
    public void EligibilityError_BlocksNonBreedingStatuses(GoatStatus status) =>
        Assert.NotNull(BreedingRules.EligibilityError(TestDb.Doe("D1", status: status), TestDb.Today));

    [Fact]
    public void EligibilityError_BlocksDoeAlreadyCrossed()
    {
        var doe = TestDb.Doe("D1");
        doe.MatedDate = TestDb.Today.AddDays(-10);
        Assert.Contains("already crossed", BreedingRules.EligibilityError(doe, TestDb.Today));
    }

    [Fact]
    public void EligibilityError_BlocksKidOfUnknownAge()
    {
        var doe = TestDb.Doe("D1", status: GoatStatus.Kid);
        doe.Source = GoatSource.Bought;
        Assert.NotNull(BreedingRules.EligibilityError(doe, TestDb.Today));
    }

    private static Dictionary<int, PedigreeNode> Herd(params PedigreeNode[] nodes) => nodes.ToDictionary(n => n.Id);

    [Fact]
    public void InbreedingWarnings_FlagsSireCrossedToDaughter()
    {
        var buck = new PedigreeNode(1, "B1", null, null);
        var doe = new PedigreeNode(2, "D1", 1, null);
        Assert.Contains(BreedingRules.InbreedingWarnings(doe, buck, Herd(buck, doe)), w => w.Contains("sire (father)"));
    }

    [Fact]
    public void InbreedingWarnings_FlagsDamCrossedToSon()
    {
        var doe = new PedigreeNode(1, "D1", null, null);
        var buck = new PedigreeNode(2, "B1", null, 1);
        Assert.Contains(BreedingRules.InbreedingWarnings(doe, buck, Herd(buck, doe)), w => w.Contains("dam (mother)"));
    }

    [Fact]
    public void InbreedingWarnings_FlagsFullAndHalfSiblings()
    {
        var full = BreedingRules.InbreedingWarnings(new(3, "D", 1, 2), new(4, "B", 1, 2), Herd()).ToList();
        var half = BreedingRules.InbreedingWarnings(new(3, "D", 1, 2), new(4, "B", 1, 9), Herd()).ToList();
        Assert.Contains(full, w => w.Contains("full siblings"));
        Assert.Contains(half, w => w.Contains("half siblings"));
    }

    [Fact]
    public void InbreedingWarnings_FlagsSharedGrandparent()
    {
        var grand = new PedigreeNode(1, "G", null, null);
        var p1 = new PedigreeNode(2, "P1", 1, null);
        var p2 = new PedigreeNode(3, "P2", 1, null);
        var doe = new PedigreeNode(4, "D", 2, null);
        var buck = new PedigreeNode(5, "B", 3, null);
        Assert.Contains(BreedingRules.InbreedingWarnings(doe, buck, Herd(grand, p1, p2, doe, buck)), w => w.Contains("grandparent"));
    }

    [Fact]
    public void InbreedingWarnings_EmptyForUnrelatedGoats() =>
        Assert.Empty(BreedingRules.InbreedingWarnings(new(1, "D", 10, 11), new(2, "B", 20, 21), Herd()));
}
