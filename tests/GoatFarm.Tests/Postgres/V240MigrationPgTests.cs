using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GoatFarm.Tests.Postgres;

/// <summary>Runs AddV240Foundation against a database holding pre-V240 (V41) farm data.</summary>
[Trait("Category", "Postgres")]
public class V240MigrationPgTests
{
    private const string BeforeV240 = "20260919091701_AddV41Features";
    private const string V240 = "20261013145451_AddV240Foundation";

    // Gender 0 Female / 1 Male; Status 1 Milking, 2 Pregnant, 3 Dry, 4 Buck.
    private const string LegacyGoats = """
        INSERT INTO "Goats" ("Tag","Name","Comment","Breed","Gender","Status","Source","PurchasePrice","EventDate",
                             "PrepCrossDate","MatedDate","BuckTag","KidsCount","UltrasoundDate","IsArchived","CreatedDate","UpdatedDate")
        VALUES
          ('BUCK1',      'Raja', NULL, 'Beetal', 1, 4, 0, 50000, '2022-01-01', NULL, NULL, NULL, NULL, NULL, false, now(), now()),
          ('D-PLAIN',    NULL, 'اردو', 'Beetal', 0, 3, 1, 0, '2023-01-01', NULL, NULL, NULL, NULL, NULL, false, now(), now()),
          ('D-PREP',     NULL, NULL, 'Beetal', 0, 3, 1, 0, '2023-01-01', '2026-09-20', NULL, NULL, NULL, NULL, false, now(), now()),
          ('D-CROSS',    NULL, NULL, 'Beetal', 0, 3, 1, 0, '2023-01-01', NULL, '2026-08-01', 'buck1', NULL, NULL, false, now(), now()),
          ('D-PREG',     NULL, NULL, 'Beetal', 0, 2, 1, 0, '2023-01-01', NULL, '2026-06-01', 'BUCK1', 2, '2026-07-15', false, now(), now()),
          ('D-STATUS',   NULL, NULL, 'Beetal', 0, 2, 1, 0, '2023-01-01', NULL, NULL, NULL, NULL, NULL, false, now(), now()),
          ('D-GONEBUCK', NULL, NULL, 'Beetal', 0, 1, 1, 0, '2023-01-01', NULL, '2026-09-01', 'SOLD-BUCK', NULL, NULL, false, now(), now()),
          ('D-FEMBUCK',  NULL, NULL, 'Beetal', 0, 3, 1, 0, '2023-01-01', NULL, '2026-09-05', 'D-PLAIN', NULL, NULL, false, now(), now());
        """;

    private const string GoatFingerprintSql = """
        SELECT string_agg(concat_ws('|', "Tag", "Name", "Comment", "Breed", "Gender", "Status", "Source", "PurchasePrice",
               "EventDate", "PrepCrossDate", "MatedDate", "BuckTag", "KidsCount", "UltrasoundDate", "IsArchived"), ';' ORDER BY "Tag")
        FROM "Goats"
        """;

    private static async Task<PostgresTestDatabase> CreateLegacyDatabaseAsync()
    {
        var pg = await PostgresTestDatabase.CreateAsync(migrate: false);
        await using var ctx = pg.CreateContext();
        await ctx.GetService<IMigrator>().MigrateAsync(BeforeV240);
        await pg.ExecuteAsync(LegacyGoats);
        return pg;
    }

    private static async Task<string> FingerprintAsync(PostgresTestDatabase pg)
    {
        await using var conn = new Npgsql.NpgsqlConnection(pg.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(GoatFingerprintSql, conn);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    [SkippableFact]
    public async Task Upgrade_KeepsGoatsIntactAndMapsStagesWithoutInferringPregnancy()
    {
        await using var pg = await CreateLegacyDatabaseAsync();
        var before = await FingerprintAsync(pg);

        await using (var ctx = pg.CreateContext())
            await ctx.Database.MigrateAsync();

        Assert.Equal(before, await FingerprintAsync(pg));
        Assert.Equal(8, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" """));

        async Task<long> Stage(string tag) => await pg.ScalarAsync($"""SELECT "BreedingStage" FROM "Goats" WHERE "Tag" = '{tag}'""");
        Assert.Equal(0, await Stage("D-PLAIN"));
        Assert.Equal(1, await Stage("D-PREP"));
        Assert.Equal(2, await Stage("D-CROSS"));
        Assert.Equal(3, await Stage("D-PREG"));
        // Status says Pregnant but there is no mating record: the migration must not invent a breeding cycle.
        Assert.Equal(0, await Stage("D-STATUS"));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" e JOIN "Goats" g ON g."Id" = e."DoeId" WHERE g."Tag" IN ('D-STATUS','D-PLAIN','D-PREP','BUCK1')"""));

        // One history row per crossed doe; only the scanned-positive one is ConfirmedPregnant.
        Assert.Equal(4, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" """));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" WHERE "Outcome" = 1 AND "ScanKids" = 2 AND "ScanDate" = '2026-07-15'"""));
        Assert.Equal(3, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" WHERE "Outcome" = 0 AND "ScanDate" IS NULL AND "ScanKids" IS NULL"""));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" e JOIN "Goats" g ON g."Id" = e."DoeId" WHERE e."MatedDate" <> g."MatedDate" """));

        // Buck matched case-insensitively and only to a male; unknown or female "bucks" keep the text only.
        Assert.Equal(2, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" e JOIN "Goats" b ON b."Id" = e."BuckId" WHERE b."Tag" = 'BUCK1'"""));
        Assert.Equal(2, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" WHERE "BuckId" IS NULL AND "BuckTagText" IN ('SOLD-BUCK','D-PLAIN')"""));

        // Parent links and kidding links cannot be derived from legacy data and stay empty.
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" WHERE "SireId" IS NOT NULL OR "DamId" IS NOT NULL OR "KiddingRecordId" IS NOT NULL"""));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM "Goats" WHERE "HealthState" <> 0 OR "IsTemporaryTag" """));
    }

    [SkippableFact]
    public async Task RerunningIdempotentScript_CreatesNoDuplicateEvents()
    {
        await using var pg = await CreateLegacyDatabaseAsync();
        string script;
        await using (var ctx = pg.CreateContext())
        {
            await ctx.Database.MigrateAsync();
            script = ctx.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        }

        await pg.ExecuteAsync(script);
        await pg.ExecuteAsync(script);

        Assert.Equal(4, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" """));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM (SELECT "DoeId" FROM "BreedingEvents" WHERE "Outcome" IN (0,1) GROUP BY "DoeId" HAVING count(*) > 1) d"""));
        Assert.Equal(1, await pg.ScalarAsync($"""SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = '{V240}'"""));
    }

    [SkippableFact]
    public async Task Rollback_RestoresV41SchemaAndKeepsLegacyGoatData()
    {
        await using var pg = await CreateLegacyDatabaseAsync();
        var before = await FingerprintAsync(pg);
        await using (var ctx = pg.CreateContext())
        {
            await ctx.Database.MigrateAsync();
            await ctx.GetService<IMigrator>().MigrateAsync(BeforeV240);
        }

        Assert.Equal(before, await FingerprintAsync(pg));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM information_schema.tables WHERE table_name IN ('BreedingEvents','KiddingRecords','FarmTasks')"""));
        Assert.Equal(0, await pg.ScalarAsync("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Goats' AND column_name IN ('BreedingStage','SireId','DamId','Owner','HealthState','KiddingRecordId')"""));

        await using (var ctx = pg.CreateContext())
            await ctx.Database.MigrateAsync();
        Assert.Equal(4, await pg.ScalarAsync("""SELECT count(*) FROM "BreedingEvents" """));
    }

    [SkippableFact]
    public async Task Schema_HasExpectedForeignKeyRulesIndexesAndDefaults()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();

        async Task<string> DeleteRule(string constraint) =>
            (await QueryStringAsync(pg, $"""SELECT delete_rule FROM information_schema.referential_constraints WHERE constraint_name = '{constraint}'"""))!;

        // History must never be silently destroyed by deleting a goat.
        Assert.Equal("RESTRICT", await DeleteRule("FK_Goats_Goats_SireId"));
        Assert.Equal("RESTRICT", await DeleteRule("FK_Goats_Goats_DamId"));
        Assert.Equal("RESTRICT", await DeleteRule("FK_BreedingEvents_Goats_DoeId"));
        Assert.Equal("RESTRICT", await DeleteRule("FK_BreedingEvents_Goats_BuckId"));
        Assert.Equal("RESTRICT", await DeleteRule("FK_KiddingRecords_Goats_DoeId"));
        Assert.Equal("RESTRICT", await DeleteRule("FK_KiddingRecords_BreedingEvents_BreedingEventId"));
        Assert.Equal("SET NULL", await DeleteRule("FK_Goats_KiddingRecords_KiddingRecordId"));
        Assert.Equal("SET NULL", await DeleteRule("FK_FarmTasks_Goats_GoatId"));
        Assert.Equal("SET NULL", await DeleteRule("FK_FarmTasks_AspNetUsers_AssigneeUserId"));

        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_KiddingRecords_ClientRequestId' AND indexdef LIKE 'CREATE UNIQUE%'"""));
        Assert.Equal(1, await pg.ScalarAsync("""SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_BreedingEvents_DoeId_Outcome'"""));

        Assert.Equal("NO|0", await QueryStringAsync(pg, """SELECT is_nullable || '|' || column_default FROM information_schema.columns WHERE table_name = 'Goats' AND column_name = 'BreedingStage'"""));
        Assert.Equal("NO", await QueryStringAsync(pg, """SELECT is_nullable FROM information_schema.columns WHERE table_name = 'BreedingEvents' AND column_name = 'DoeId'"""));
        Assert.Equal("YES", await QueryStringAsync(pg, """SELECT is_nullable FROM information_schema.columns WHERE table_name = 'BreedingEvents' AND column_name = 'BuckId'"""));
    }

    private static async Task<string?> QueryStringAsync(PostgresTestDatabase pg, string sql)
    {
        await using var conn = new Npgsql.NpgsqlConnection(pg.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }
}
