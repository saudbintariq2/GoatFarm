using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class BreedingReportAggregatorTests
{
    [Fact]
    public void BatchDoeIds_IncludesBreedingGroupMembersForBatch()
    {
        var batch = new BreedingSeasonBatch { Id = "main", Name = "Main group" };
        var plan = new BreedingSeasonPlan
        {
            Batch = batch,
            CrossFrom = new DateOnly(2026, 10, 1),
            CrossTo = new DateOnly(2026, 11, 16),
            KidFrom = new DateOnly(2027, 3, 1),
            KidTo = new DateOnly(2027, 4, 30),
            WeanTo = new DateOnly(2027, 8, 1)
        };
        var crossTo = BreedingReportAggregator.EffectiveCrossTo(plan, new DateOnly(2026, 11, 21));
        var d101 = TestDb.Doe("D-101"); d101.Id = 101;
        var d102 = TestDb.Doe("D-102"); d102.Id = 102;
        var live = new List<Goat> { d101, d102 };
        var groups = new List<BreedingGroupDto>
        {
            new()
            {
                Id = "BK-07",
                BirthBatchId = "main",
                Members =
                [
                    new BreedingGroupMemberDto { GoatId = 101 },
                    new BreedingGroupMemberDto { GoatId = 102 }
                ]
            }
        };

        var ids = BreedingReportAggregator.BatchDoeIds(live, batch, plan, crossTo, groups);

        Assert.Equal(2, ids.Count);
        Assert.Contains(101, ids);
        Assert.Contains(102, ids);
    }

    [Fact]
    public void EffectiveCrossTo_ExtendsThroughTodayWhileSeasonInProgress()
    {
        var batch = new BreedingSeasonBatch { Id = "main", Name = "Main" };
        var plan = new BreedingSeasonPlan
        {
            Batch = batch,
            CrossFrom = new DateOnly(2026, 10, 1),
            CrossTo = new DateOnly(2026, 11, 16),
            WeanTo = new DateOnly(2027, 8, 1)
        };
        var today = new DateOnly(2026, 11, 21);

        var crossTo = BreedingReportAggregator.EffectiveCrossTo(plan, today);

        Assert.Equal(today, crossTo);
    }

    [Fact]
    public void EffectiveCrossFrom_PullsBackWhenBuckInBeforeNominalSeasonWindow()
    {
        var batch = new BreedingSeasonBatch { Id = "b1", Name = "Main group" };
        var plan = new BreedingSeasonPlan
        {
            Batch = batch,
            CrossFrom = new DateOnly(2027, 4, 4),
            CrossTo = new DateOnly(2027, 4, 18),
            WeanTo = new DateOnly(2028, 8, 1)
        };
        var today = new DateOnly(2026, 11, 21);
        var d105 = TestDb.Doe("D-105"); d105.Id = 105; d105.MatedDate = new DateOnly(2026, 11, 17);
        var batchDoes = new HashSet<int> { 105 };
        var groups = new List<BreedingGroupDto>
        {
            new()
            {
                BirthBatchId = "b1",
                Start = "2026-11-17",
                Members = [new BreedingGroupMemberDto { GoatId = 105 }]
            }
        };

        var from = BreedingReportAggregator.EffectiveCrossFrom(
            plan, today, batchDoes, [d105], [], groups, batch);

        Assert.Equal(new DateOnly(2026, 11, 17), from);
    }

    [Fact]
    public void FilterSeasonEvents_IncludesEarlyCrossesInExtendedWindow()
    {
        var batchDoes = new HashSet<int> { 105 };
        var events = new List<BreedingEvent>
        {
            new() { DoeId = 105, MatedDate = new DateOnly(2026, 11, 17), Outcome = BreedingOutcome.Open }
        };
        var filtered = BreedingReportAggregator.FilterSeasonEvents(
            events, batchDoes, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 21), null);

        Assert.Single(filtered);
    }
}