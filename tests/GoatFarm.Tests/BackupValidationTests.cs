using System.Text.Json.Nodes;
using GoatFarm.Application.Common;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests;

public class BackupValidationTests
{
    private static JsonObject ValidV2() => new()
    {
        ["formatVersion"] = 2,
        ["app"] = BackupFormat.AppMarker,
        ["goats"] = new JsonArray(
            new JsonObject { ["id"] = 1, ["tag"] = "SIRE", ["eventDate"] = "2023-01-01" },
            new JsonObject { ["id"] = 2, ["tag"] = "DOE", ["eventDate"] = "2024-01-01", ["sireId"] = 1 },
            new JsonObject { ["id"] = 3, ["tag"] = "KID", ["eventDate"] = "2026-01-01", ["damId"] = 2, ["kiddingRecordId"] = 10 }),
        ["breedingEvents"] = new JsonArray(new JsonObject { ["id"] = 5, ["doeId"] = 2, ["buckId"] = 1, ["matedDate"] = "2025-08-01" }),
        ["kiddingRecords"] = new JsonArray(new JsonObject
        {
            ["id"] = 10, ["doeId"] = 2, ["breedingEventId"] = 5, ["kiddingDate"] = "2026-01-01",
            ["clientRequestId"] = "11111111-2222-3333-4444-555555555555"
        }),
        ["farmTasks"] = new JsonArray(new JsonObject { ["id"] = 1, ["title"] = "Check kid", ["goatId"] = 3 })
    };

    private static async Task<IReadOnlyList<string>> RejectAsync(JsonNode backup)
    {
        await using var db = TestDb.Create();
        var ex = await Assert.ThrowsAsync<BackupValidationException>(() => new BackupService(db).ImportAsync(backup.ToJsonString()));
        Assert.NotEmpty(ex.Errors);
        return ex.Errors;
    }

    [Fact]
    public async Task NewerFormatVersion_IsRejected()
    {
        var backup = ValidV2();
        backup["formatVersion"] = 3;
        Assert.Contains(await RejectAsync(backup), e => e.Contains("version 3"));
    }

    [Theory]
    [InlineData("goats")]
    [InlineData("breedingEvents")]
    [InlineData("kiddingRecords")]
    [InlineData("farmTasks")]
    public async Task MissingRequiredSection_IsRejected(string section)
    {
        var backup = ValidV2();
        backup.Remove(section);
        Assert.Contains(await RejectAsync(backup), e => e.Contains($"'{section}' is missing"));
    }

    [Fact]
    public async Task EmptyOrForeignFile_IsRejected()
    {
        await RejectAsync(new JsonObject());
        await RejectAsync(new JsonArray());
        var other = ValidV2();
        other["app"] = "SomethingElse";
        await RejectAsync(other);
    }

    [Fact]
    public async Task DuplicateIdsTagsAndRequestIds_AreRejected()
    {
        var backup = ValidV2();
        backup["goats"]!.AsArray().Add(new JsonObject { ["id"] = 2, ["tag"] = "doe", ["eventDate"] = "2024-01-01" });
        var kid = (JsonObject)backup["kiddingRecords"]![0]!.DeepClone();
        kid["id"] = 11;
        backup["kiddingRecords"]!.AsArray().Add(kid);

        var errors = await RejectAsync(backup);
        Assert.Contains(errors, e => e.Contains("Goat id 2 appears more than once"));
        Assert.Contains(errors, e => e.Contains("Tag 'doe' appears more than once"));
        Assert.Contains(errors, e => e.Contains("request id") && e.Contains("more than once"));
    }

    [Theory]
    [InlineData("goats", 1, "sireId", 99)]
    [InlineData("goats", 2, "kiddingRecordId", 99)]
    [InlineData("breedingEvents", 0, "doeId", 99)]
    [InlineData("breedingEvents", 0, "buckId", 99)]
    [InlineData("kiddingRecords", 0, "breedingEventId", 99)]
    [InlineData("farmTasks", 0, "goatId", 99)]
    public async Task BrokenRelationship_IsRejected(string section, int index, string property, int value)
    {
        var backup = ValidV2();
        backup[section]![index]![property] = value;
        Assert.Contains(await RejectAsync(backup), e => e.Contains("points to a record that is not in the backup"));
    }

    [Fact]
    public async Task GoatAsItsOwnParent_IsRejected()
    {
        var backup = ValidV2();
        backup["goats"]![1]!["damId"] = 2;
        Assert.Contains(await RejectAsync(backup), e => e.Contains("its own parent"));
    }

    [Fact]
    public async Task LegacyFileWithDuplicateTags_IsRejected()
    {
        var legacy = new JsonObject
        {
            ["goats"] = new JsonArray(new JsonObject { ["tag"] = "A1" }, new JsonObject { ["tag"] = "a1" })
        };
        Assert.Contains(await RejectAsync(legacy), e => e.Contains("more than once"));
    }
}
