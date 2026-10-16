using System.Text.Json;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class BackupRoundTripPgTests
{
    /// <summary>Same serialization as <c>DashboardController.Export</c>.</summary>
    private static async Task<string> ExportJsonAsync(GoatFarmDbContext db) =>
        JsonSerializer.Serialize(await new BackupService(db).ExportAsync(), BackupService.SerializerOptions);

    private sealed record Snapshot(
        List<string> Goats, List<string> Events, List<string> Kiddings, List<string> Tasks,
        List<string> Weights, List<string> EmptyLogs, List<string> Settings);

    /// <summary>Id-independent fingerprint of the Phase 0–3 records and their relationships.</summary>
    private static async Task<Snapshot> SnapshotAsync(GoatFarmDbContext db)
    {
        var goats = await db.Goats.AsNoTracking().Include(g => g.Sire).Include(g => g.Dam).Include(g => g.KiddingRecord)
            .OrderBy(g => g.Tag).ToListAsync();
        var tags = goats.ToDictionary(g => g.Id, g => g.Tag);
        var events = await db.BreedingEvents.AsNoTracking().OrderBy(e => e.MatedDate).ThenBy(e => e.DoeId).ToListAsync();
        var kiddings = await db.KiddingRecords.AsNoTracking().Include(k => k.BreedingEvent).Include(k => k.Kids).ToListAsync();
        var tasks = await db.FarmTasks.AsNoTracking().OrderBy(t => t.Title).ToListAsync();
        return new Snapshot(
            goats.Select(g => string.Join("|", g.Tag, g.Name, g.Gender, g.Status, g.Source, g.EventDate, g.Owner, g.HealthState,
                g.BreedingStage, g.Sire?.Tag, g.Dam?.Tag, g.SireTagText, g.DamTagText, g.IsTemporaryTag,
                g.KiddingRecord?.ClientRequestId, g.MatedDate, g.BuckTag, g.KidsCount, g.UltrasoundDate, g.PrepCrossDate,
                g.DateOfBirth, g.DateOfBirthIsEstimated)).ToList(),
            events.Select(e => string.Join("|", tags[e.DoeId], e.BuckId is null ? null : tags[e.BuckId.Value], e.BuckTagText,
                e.MatedDate, e.ScanDate, e.ScanKids, e.Outcome, e.OutcomeDate, e.Notes)).ToList(),
            kiddings.OrderBy(k => k.ClientRequestId).Select(k => string.Join("|", tags[k.DoeId], k.ClientRequestId, k.KiddingDate,
                k.LiveKids, k.Stillborn, k.Ease, k.Notes, k.BreedingEvent?.MatedDate,
                string.Join(",", k.Kids.Select(c => c.Tag).Order()))).ToList(),
            tasks.Select(t => string.Join("|", t.Title, t.Notes, t.Type, t.Priority, t.Status, t.DueDate,
                t.AssigneeUserId, t.CreatedByUserId, t.GoatId is null ? null : tags[t.GoatId.Value],
                t.StartedAtUtc is null, t.CompletedAtUtc is null)).ToList(),
            (await db.GoatWeightRecords.AsNoTracking().ToListAsync()).Select(w => $"{tags[w.GoatId]}|{w.Date}|{w.Kg}").Order().ToList(),
            (await db.BreedingEmptyLogs.AsNoTracking().ToListAsync()).Select(l => $"{tags[l.GoatId]}|{l.ScanDate}|{l.MatedDate}|{l.BuckTag}").Order().ToList(),
            (await db.AppSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync()).Select(s => $"{s.Key}={s.Value}").ToList());
    }

    [SkippableFact]
    public async Task ExportThenImport_PreservesPhase0To3RecordsAndRelationships()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        Snapshot before;
        string json;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            await FarmScenario.BuildAsync(db);
            before = await SnapshotAsync(db);
            json = await ExportJsonAsync(db);
        }
        if (Environment.GetEnvironmentVariable("GOATFARM_TEST_DUMP") is { Length: > 0 } dump)
            await File.WriteAllTextAsync(dump, json);

        Assert.Contains(before.Events, e => e.Contains(nameof(BreedingOutcome.Kidded)));
        Assert.Equal(1, before.Kiddings.Count);
        Assert.Equal(2, before.Tasks.Count);

        await using (var db = pg.CreateContext())
            await new BackupService(db).ImportAsync(json);

        await using (var db = pg.CreateContext())
        {
            var after = await SnapshotAsync(db);
            AssertSame("goats", before.Goats, after.Goats);
            AssertSame("breeding events", before.Events, after.Events);
            AssertSame("kidding records", before.Kiddings, after.Kiddings);
            AssertSame("tasks", before.Tasks, after.Tasks);
            AssertSame("weights", before.Weights, after.Weights);
            AssertSame("empty scans", before.EmptyLogs, after.EmptyLogs);
            AssertSame("settings", before.Settings, after.Settings);
        }
    }

    [SkippableFact]
    public async Task ImportFailingHalfway_RollsBackAndLeavesExistingFarmIntact()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        Snapshot before;
        string json;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            await FarmScenario.BuildAsync(db);
            before = await SnapshotAsync(db);
            json = await ExportJsonAsync(db);
        }

        // Passes validation, but the 300-character title violates varchar(200) only when tasks are written —
        // after the wipe, goats and breeding history have already been saved inside the transaction.
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["farmTasks"]![0]!["title"] = new string('x', 300);
        node["goats"]![0]!["name"] = "SHOULD NOT SURVIVE";

        await using (var db = pg.CreateContext())
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => new BackupService(db).ImportAsync(node.ToJsonString()));

        await using (var db = pg.CreateContext())
        {
            var after = await SnapshotAsync(db);
            AssertSame("goats", before.Goats, after.Goats);
            AssertSame("breeding events", before.Events, after.Events);
            AssertSame("kidding records", before.Kiddings, after.Kiddings);
            AssertSame("tasks", before.Tasks, after.Tasks);
            AssertSame("settings", before.Settings, after.Settings);
        }
    }

    [SkippableFact]
    public async Task InvalidBackup_IsRejectedBeforeAnythingIsDeleted()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        string json;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            await FarmScenario.BuildAsync(db);
            json = await ExportJsonAsync(db);
        }
        var goatsBefore = await pg.ScalarAsync("""SELECT count(*) FROM "Goats" """);

        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["breedingEvents"]![0]!["doeId"] = 999999;
        await using (var db = pg.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<GoatFarm.Application.Common.BackupValidationException>(
                () => new BackupService(db).ImportAsync(node.ToJsonString()));
            Assert.Contains(ex.Errors, e => e.Contains("doe points to a record that is not in the backup"));
        }
        await using (var db = pg.CreateContext())
            await Assert.ThrowsAsync<GoatFarm.Application.Common.BackupValidationException>(
                () => new BackupService(db).ImportAsync("{}"));

        Assert.Equal(goatsBefore, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" """));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM "KiddingRecords" """));
    }

    [SkippableFact]
    public async Task RestoringSameBackupTwice_DoesNotDuplicateRecords()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        string json;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            await FarmScenario.BuildAsync(db);
            json = await ExportJsonAsync(db);
        }
        for (var i = 0; i < 2; i++)
        {
            await using var db = pg.CreateContext();
            await new BackupService(db).ImportAsync(json);
        }

        Assert.Equal(8, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" """));
        Assert.Equal(4, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" """));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM "KiddingRecords" """));
        Assert.Equal(2, await pg.ScalarAsync("""SELECT count(*) FROM "FarmTasks" """));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM (SELECT "Key" FROM "AppSettings" GROUP BY "Key" HAVING count(*) > 1) d"""));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM "AspNetUsers" WHERE "Email" = 'staff@farm.test'"""));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM "FarmTasks" WHERE "AssigneeUserId" = 'user-staff-1'"""));
    }

    [SkippableFact]
    public async Task Export_IncludesAccessListButNeverCredentials()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        string json;
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            await FarmScenario.BuildAsync(db);
            json = await ExportJsonAsync(db);
        }

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("formatVersion").GetInt32());
        var users = doc.RootElement.GetProperty("security").GetProperty("users");
        Assert.Contains(users.EnumerateArray(), u => u.GetProperty("email").GetString() == FarmScenario.StaffEmail);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXAMPLEHASHNOTEXPORTED", json);
        Assert.DoesNotContain("SecurityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(doc.RootElement.GetProperty("farmTasks").EnumerateArray(),
            t => t.GetProperty("notes").GetString() == "اردو نوٹ");
    }

    [SkippableFact]
    public async Task LegacyV19Backup_StillImportsWithParentsWeightsAndDeathsMappedToNewIds()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await FarmScenario.SeedDefaultsAsync(db);

        const string legacy = """
        {
          "goats": [
            { "id": 1712000000001, "tag": "OLD-SIRE", "gender": "male", "status": "buck", "source": "bought", "date": "2023-01-01", "breed": "Beetal" },
            { "id": 1712000000002, "tag": "OLD-DOE", "gender": "female", "status": "dry", "source": "born", "date": "2024-01-01", "breed": "Beetal", "sireId": 1712000000001 }
          ],
          "goatWeights": [ { "goatId": 1712000000002, "date": "2024-06-01", "kg": 21.5 } ],
          "deaths": [ { "goatId": 1712000000001, "tag": "OLD-SIRE", "date": "2024-07-01", "reason": "old age" } ],
          "mixRecipe": { "wanda": 40 }
        }
        """;
        await using (var db = pg.CreateContext())
            await new BackupService(db).ImportAsync(legacy);

        await using (var db = pg.CreateContext())
        {
            var doe = await db.Goats.Include(g => g.Sire).SingleAsync(g => g.Tag == "OLD-DOE");
            Assert.Equal("OLD-SIRE", doe.Sire?.Tag);
            var weight = await db.GoatWeightRecords.SingleAsync();
            Assert.Equal(doe.Id, weight.GoatId);
            var death = await db.DeathRecords.SingleAsync();
            Assert.Equal(doe.SireId, death.GoatId);
            Assert.Equal("{\"wanda\":40}", (await db.AppSettings.SingleAsync(s => s.Key == GoatFarm.Domain.Constants.AppSettingKeys.MixRecipe)).Value);
        }
    }

    internal static void AssertSame(string what, List<string> expected, List<string> actual)
    {
        var missing = expected.Except(actual).ToList();
        var extra = actual.Except(expected).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0 && expected.Count == actual.Count,
            $"{what} changed after restore ({expected.Count} -> {actual.Count}).\nLost:\n  {string.Join("\n  ", missing)}\nGained:\n  {string.Join("\n  ", extra)}");
    }
}
