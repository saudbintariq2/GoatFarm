using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class GeneralSecurityTests
{
    [Fact]
    public void CsvHelper_EscapesFormulaInjection()
    {
        Assert.Equal("'=1+1", CsvHelper.Cell("=1+1"));
    }

    [Fact]
    public async Task FarmCsvExport_Herd_IncludesTags()
    {
        var db = TestDb.Create();
        db.Goats.Add(new Goat { Tag = "T100", Gender = GoatGender.Female, Status = GoatStatus.Dry });
        await db.SaveChangesAsync();
        var feed = new FeedService(db, new GoatService(db), new MilkService(db));
        var svc = new FarmCsvExportService(db, feed);
        var bytes = await svc.ExportHerdAsync();
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("T100", text);
    }

    [Fact]
    public void FarmRecordNumberFormat_MatchesV242Pattern()
    {
        var n = FarmRecordNumberFormat.Format("MR", "k7q", new DateOnly(2026, 11, 7), 1);
        Assert.Equal("MR-20261107-K7Q-001", n);
    }
}
