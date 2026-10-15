using System.Text.Json;
using System.Text.Json.Nodes;
using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class DateOfBirthPgTests
{
    private const string BeforeDateOfBirth = "20261013155857_AddHerdQueryIndexes";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private const string GoatFingerprintSql = """
        SELECT string_agg(concat_ws('|', "Tag", "Name", "Breed", "Gender", "Status", "Source", "PurchasePrice", "EventDate",
               "MatedDate", "Owner", "HealthState", "BreedingStage", "IsArchived"), ';' ORDER BY "Tag")
        FROM "Goats"
        """;

    private static async Task<string> ScalarTextAsync(PostgresTestDatabase pg, string sql)
    {
        await using var conn = new Npgsql.NpgsqlConnection(pg.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        return Convert.ToString(await cmd.ExecuteScalarAsync())!;
    }

    [SkippableFact]
    public async Task Migration_AddsEmptyDateOfBirthToExistingGoats_WithoutChangingThem()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync(migrate: false);
        await using (var ctx = pg.CreateContext())
            await ctx.GetService<IMigrator>().MigrateAsync(BeforeDateOfBirth);
        // The current model already has DateOfBirth, so rows for the old schema are written by SQL.
        await pg.ExecuteAsync("""
            INSERT INTO "Goats" ("Tag","Breed","Gender","Status","Source","PurchasePrice","EventDate","IsArchived","HealthState",
                                 "BreedingStage","IsTemporaryTag","CreatedDate","UpdatedDate")
            VALUES ('LEGACY-BOUGHT','Beetal',0,3,0,40000,'2026-07-01',false,0,0,false,now(),now()),
                   ('LEGACY-BORN','Beetal',0,3,1,0,'2025-01-01',false,0,0,false,now(),now());
            """);
        var before = await ScalarTextAsync(pg, GoatFingerprintSql);

        await using (var ctx = pg.CreateContext())
            await ctx.Database.MigrateAsync();

        Assert.Equal(before, await ScalarTextAsync(pg, GoatFingerprintSql));
        Assert.Equal(2, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" WHERE "DateOfBirth" IS NULL AND NOT "DateOfBirthIsEstimated" """));

        await using (var ctx = pg.CreateContext())
        {
            var bought = await new GoatService(ctx).GetByTagAsync("LEGACY-BOUGHT");
            Assert.Null(bought!.AgeDays);
            Assert.Equal("age unknown", bought.AgeLabel);
            var doe = await ctx.Goats.AsNoTracking().SingleAsync(g => g.Tag == "LEGACY-BOUGHT");
            Assert.Contains("age is unknown", BreedingRules.AgeVerificationWarning(doe, Today));
        }
    }

    [SkippableFact]
    public async Task Rollback_DropsOnlyTheDateOfBirthColumns()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var ctx = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(ctx);
            await FarmScenario.BuildAsync(ctx);
        }
        var before = await ScalarTextAsync(pg, GoatFingerprintSql);

        await using (var ctx = pg.CreateContext())
            await ctx.GetService<IMigrator>().MigrateAsync(BeforeDateOfBirth);

        Assert.Equal(before, await ScalarTextAsync(pg, GoatFingerprintSql));
        Assert.Equal(0, await pg.ScalarAsync("""
            SELECT count(*) FROM information_schema.columns WHERE table_name = 'Goats' AND column_name LIKE 'DateOfBirth%'
            """));
    }

    [SkippableFact]
    public async Task AgeFilterAndDateOfBirth_WorkInSql()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var ctx = pg.CreateContext())
        {
            var goats = new GoatService(ctx);
            CreateGoatViewModel M(string tag, GoatSource source, DateOnly date, DateOnly? dob = null, bool est = false) => new()
            {
                Tag = tag, Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Dry, Source = source,
                EventDate = date, DateOfBirth = dob, DateOfBirthIsEstimated = est
            };
            await goats.CreateAsync(M("BORN-80", GoatSource.Born, Today.AddDays(-80)));
            await goats.CreateAsync(M("BOUGHT-80", GoatSource.Bought, Today.AddDays(-3), Today.AddDays(-80), est: true));
            await goats.CreateAsync(M("BOUGHT-NONE", GoatSource.Bought, Today.AddDays(-80)));
        }
        await using (var ctx = pg.CreateContext())
        {
            var page = await new GoatService(ctx).GetHerdPageAsync("age:70:90", 1, 50);
            Assert.Equal(["BORN-80", "BOUGHT-80"], page.Goats.Select(g => g.Tag).Order());
            var est = page.Goats.Single(g => g.Tag == "BOUGHT-80");
            Assert.True(est.AgeIsEstimated);
            Assert.Equal(Today.AddDays(-80), est.DateOfBirth);
        }
    }

    [SkippableFact]
    public async Task Backup_RoundTripsDateOfBirth_AndRejectsAFutureOne()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        string json;
        await using (var ctx = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(ctx);
            var goats = new GoatService(ctx);
            await goats.CreateAsync(new CreateGoatViewModel { Tag = "B-EST", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Dry,
                Source = GoatSource.Bought, EventDate = Today.AddDays(-10), DateOfBirth = Today.AddDays(-500), DateOfBirthIsEstimated = true });
            await goats.CreateAsync(new CreateGoatViewModel { Tag = "B-NONE", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Dry,
                Source = GoatSource.Bought, EventDate = Today.AddDays(-10) });
            json = JsonSerializer.Serialize(await new BackupService(ctx).ExportAsync(), BackupService.SerializerOptions);
        }

        var bad = JsonNode.Parse(json)!;
        bad["goats"]!.AsArray().First(g => (string?)g!["tag"] == "B-NONE")!["dateOfBirth"] = Today.AddDays(5).ToString("yyyy-MM-dd");
        await using (var ctx = pg.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<BackupValidationException>(() => new BackupService(ctx).ImportAsync(bad.ToJsonString()));
            Assert.Contains(ex.Errors, e => e.Contains("date of birth in the future"));
        }

        await using (var ctx = pg.CreateContext())
            await new BackupService(ctx).ImportAsync(json);
        await using (var ctx = pg.CreateContext())
        {
            var restored = await ctx.Goats.AsNoTracking().OrderBy(g => g.Tag).ToListAsync();
            Assert.Equal(["B-EST", "B-NONE"], restored.Select(g => g.Tag));
            Assert.Equal(Today.AddDays(-500), restored[0].DateOfBirth);
            Assert.True(restored[0].DateOfBirthIsEstimated);
            Assert.Null(restored[1].DateOfBirth);
            Assert.False(restored[1].DateOfBirthIsEstimated);
        }
    }
}
