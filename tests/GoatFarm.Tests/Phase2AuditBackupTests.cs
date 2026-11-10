using GoatFarm.Application.ViewModels.Admin;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class Phase2AuditBackupTests
{
    [Fact]
    public async Task QueryAuditLog_FiltersByModule()
    {
        var db = TestDb.Create();
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Module = FarmTabs.Breeding,
            Action = AuditAction.Create,
            Summary = "Breeding.RecordCross",
            UserDisplay = "Test User"
        });
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Module = FarmTabs.Finance,
            Action = AuditAction.Create,
            Summary = "Finance.AddExpense",
            UserDisplay = "Test User"
        });
        await db.SaveChangesAsync();

        var svc = new AuditService(db);
        var rows = await svc.QueryAuditLogAsync(new AuditLogQueryViewModel { Module = FarmTabs.Breeding });

        Assert.Single(rows);
        Assert.Equal(FarmTabs.Breeding, rows[0].Module);
    }

    [Fact]
    public async Task SaveStoredBackup_WritesFileUnderAppData()
    {
        var db = TestDb.Create();
        var svc = new BackupService(db);
        var saved = await svc.SaveStoredBackupAsync(new { ok = true }, "test");
        Assert.False(string.IsNullOrWhiteSpace(saved.FileName));
        var roundTrip = await svc.ReadStoredBackupAsync(saved.FileName);
        Assert.NotNull(roundTrip);
        Assert.Contains("ok", System.Text.Encoding.UTF8.GetString(roundTrip));
        try { File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "backups", saved.FileName)); } catch { /* best effort */ }
    }

    [Fact]
    public async Task ClearAllFarmData_RemovesGoats_KeepsUsers()
    {
        var db = TestDb.Create();
        db.Goats.Add(new Goat { Tag = "T1", Name = "A", Gender = GoatGender.Female, Status = GoatStatus.Milking, EventDate = TestDb.Today });
        await db.SaveChangesAsync();
        var svc = new BackupService(db, new LedgerService(db));

        await svc.ClearAllFarmDataAsync();

        Assert.Empty(await db.Goats.ToListAsync());
        Assert.True(await db.FeedPrices.AnyAsync());
        Assert.True(await db.MoneyAccounts.AnyAsync());
    }
}
