using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests;

public class VaccineDueTests
{
    private static int DaysUntil(DateOnly date) =>
        (int)Math.Ceiling((date.ToDateTime(TimeOnly.MinValue) - DateTime.Today).TotalDays);

    [Fact]
    public void Repeat_WithMinAge_YoungKidNotDue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var kid = new Goat
        {
            Tag = "K-301",
            Status = GoatStatus.Kid,
            Gender = GoatGender.Female,
            EventDate = today.AddDays(-5)
        };
        var ppr = new Vaccine
        {
            Name = "PPR",
            Scope = VaccineScope.All,
            RuleType = VaccineRuleType.Repeat,
            Months = 12,
            MinAgeDays = 120
        };

        var next = VaccineDueHelper.NextDue(kid, ppr, [], DaysUntil);
        Assert.Null(next);
    }

    [Fact]
    public void BeforeKidding_DueDaysBeforeExpectedKidding()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var mated = today.AddDays(-100);
        var due = BreedingHelper.ExpectedKidding(mated);
        var doe = new Goat
        {
            Tag = "D-104",
            Status = GoatStatus.Pregnant,
            Gender = GoatGender.Female,
            MatedDate = mated,
            KidsCount = 2
        };
        var et = new Vaccine
        {
            Name = "ET booster (pre-kidding)",
            Scope = VaccineScope.Pregnant,
            RuleType = VaccineRuleType.BeforeKidding,
            Days = 30
        };

        var next = VaccineDueHelper.NextDue(doe, et, [], DaysUntil);
        Assert.NotNull(next);
        Assert.Equal(due.AddDays(-30), next.Value.Date);
    }

    [Fact]
    public void BeforeKidding_NotDueAfterShotThisPregnancy()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var mated = today.AddDays(-100);
        var doe = new Goat
        {
            Tag = "D-104",
            Gender = GoatGender.Female,
            MatedDate = mated,
            KidsCount = 2
        };
        var et = new Vaccine
        {
            Id = 9,
            Name = "ET booster",
            Scope = VaccineScope.Pregnant,
            RuleType = VaccineRuleType.BeforeKidding,
            Days = 30
        };
        var log = new List<VaccinationHistory>
        {
            new() { GoatId = doe.Id, VaccineId = et.Id, VaccinationDate = mated.AddDays(10) }
        };

        var next = VaccineDueHelper.NextDue(doe, et, log, DaysUntil);
        Assert.Null(next);
    }

    [Fact]
    public async Task GetDueCounts_ExcludesYoungKidsFromRepeatVaccines()
    {
        var db = TestDb.Create();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var kid = new Goat { Tag = "K-302", Status = GoatStatus.Kid, EventDate = today.AddDays(-5) };
        db.Goats.Add(kid);
        db.Vaccines.Add(new Vaccine
        {
            Name = "FMD",
            Scope = VaccineScope.All,
            RuleType = VaccineRuleType.Repeat,
            Months = 6,
            MinAgeDays = 90
        });
        await db.SaveChangesAsync();

        var (dueNow, _) = await new VaccineService(db).GetDueCountsAsync();
        Assert.Equal(0, dueNow);
    }
}