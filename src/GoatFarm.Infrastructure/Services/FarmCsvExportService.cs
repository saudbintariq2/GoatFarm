using System.Globalization;
using System.Text;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class FarmCsvExportService : IFarmCsvExportService
{
    private readonly GoatFarmDbContext _context;
    private readonly IFeedService _feedService;
    public FarmCsvExportService(GoatFarmDbContext context, IFeedService feedService)
    {
        _context = context;
        _feedService = feedService;
    }

    public async Task<byte[]> ExportHerdAsync(CancellationToken cancellationToken = default)
    {
        var goats = await _context.Goats.AsNoTracking()
            .OrderBy(g => g.Tag)
            .Select(g => new { g.Tag, g.Breed, g.Gender, g.Status, g.HealthState, g.PurchasePrice, g.IsArchived, g.EventDate })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Tag,Breed,Gender,Status,Health,PurchasePrice,Archived,EventDate");
        foreach (var g in goats)
        {
            sb.Append(CsvHelper.Cell(g.Tag)).Append(',')
                .Append(CsvHelper.Cell(g.Breed)).Append(',')
                .Append(CsvHelper.Cell(g.Gender.ToString())).Append(',')
                .Append(CsvHelper.Cell(g.Status.ToString())).Append(',')
                .Append(CsvHelper.Cell(g.HealthState.ToString())).Append(',')
                .Append(g.PurchasePrice.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(g.IsArchived ? "yes" : "no").Append(',')
                .AppendLine(CsvHelper.Cell(g.EventDate.ToString("yyyy-MM-dd")));
        }
        return CsvHelper.ToFile(sb.ToString());
    }

    public async Task<byte[]> ExportFinanceAsync(string? month, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var (start, end) = MonthHelper.GetMonthRange(month);
        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= start && i.Date < end)
            .OrderByDescending(i => i.Date)
            .ToListAsync(cancellationToken);
        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.Date >= start && e.Date < end)
            .OrderByDescending(e => e.Date)
            .ToListAsync(cancellationToken);
        var milk = await _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .SumAsync(s => s.Amount, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"# Month,{month}");
        sb.AppendLine("Type,Date,Category,Amount,Notes");
        foreach (var i in incomes)
            sb.Append("Income,").Append(i.Date).Append(',').Append(CsvHelper.Cell(i.Type)).Append(',')
                .Append(i.Amount.ToString(CultureInfo.InvariantCulture)).Append(',').AppendLine(CsvHelper.Cell(i.Comment));
        if (milk > 0)
            sb.Append("Income,,").Append(CsvHelper.Cell("Milk sales")).Append(',')
                .Append(milk.ToString(CultureInfo.InvariantCulture)).AppendLine(",(from milk module)");
        foreach (var e in expenses)
            sb.Append("Expense,").Append(e.Date).Append(',').Append(CsvHelper.Cell(e.Type)).Append(',')
                .Append(e.Amount.ToString(CultureInfo.InvariantCulture)).Append(',').AppendLine(CsvHelper.Cell(e.Comment));
        return CsvHelper.ToFile(sb.ToString());
    }

    public async Task<byte[]> ExportFeedAsync(CancellationToken cancellationToken = default)
    {
        var stock = await _feedService.GetFeedStockRowsAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Feed,StockKg,FullKg,PurchasedKg,UsedKg,DailyKg,DaysLeft");
        foreach (var s in stock)
        {
            sb.Append(CsvHelper.Cell(s.DisplayName)).Append(',')
                .Append(s.StockKg.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.StockKgFull.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.PurchasedKg.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.UsedKg.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(s.KgPerDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                .AppendLine(s.DaysLeft?.ToString(CultureInfo.InvariantCulture) ?? "");
        }
        return CsvHelper.ToFile(sb.ToString());
    }

    public async Task<byte[]> ExportHealthAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.VaccinationHistories.AsNoTracking()
            .Include(h => h.Goat)
            .Include(h => h.Vaccine)
            .OrderByDescending(h => h.VaccinationDate)
            .Take(5000)
            .ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Date,GoatTag,Vaccine");
        foreach (var h in rows)
        {
            sb.Append(h.VaccinationDate).Append(',')
                .Append(CsvHelper.Cell(h.Goat?.Tag)).Append(',')
                .AppendLine(CsvHelper.Cell(h.Vaccine?.Name));
        }
        return CsvHelper.ToFile(sb.ToString());
    }

    public async Task<byte[]> ExportBreedingAsync(CancellationToken cancellationToken = default)
    {
        var does = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived && g.Gender == GoatGender.Female)
            .OrderBy(g => g.Tag)
            .Select(g => new { g.Tag, g.Status, g.MatedDate, g.KidsCount, g.NextCrossDate })
            .ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Tag,Status,MatedDate,KidsCount,NextCrossDate,ConfirmedPregnant");
        foreach (var g in does)
        {
            var goat = new Domain.Entities.Goat
            {
                Tag = g.Tag,
                Gender = GoatGender.Female,
                MatedDate = g.MatedDate,
                KidsCount = g.KidsCount,
                IsArchived = false
            };
            sb.Append(CsvHelper.Cell(g.Tag)).Append(',')
                .Append(CsvHelper.Cell(g.Status.ToString())).Append(',')
                .Append(g.MatedDate?.ToString("yyyy-MM-dd") ?? "").Append(',')
                .Append(g.KidsCount?.ToString() ?? "").Append(',')
                .Append(g.NextCrossDate?.ToString("yyyy-MM-dd") ?? "").Append(',')
                .AppendLine(PregnancyRules.IsConfirmedPregnant(goat) ? "yes" : "no");
        }
        return CsvHelper.ToFile(sb.ToString());
    }

    public async Task<byte[]> ExportTasksAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _context.FarmTasks.AsNoTracking()
            .Where(t => t.Status != FarmTaskStatus.Completed)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Title,Status,DueDate,Priority,Notes");
        foreach (var t in tasks)
        {
            sb.Append(CsvHelper.Cell(t.Title)).Append(',')
                .Append(CsvHelper.Cell(t.Status.ToString())).Append(',')
                .Append(t.DueDate?.ToString("yyyy-MM-dd") ?? "").Append(',')
                .Append(CsvHelper.Cell(t.Priority.ToString())).Append(',')
                .AppendLine(CsvHelper.Cell(t.Notes));
        }
        return CsvHelper.ToFile(sb.ToString());
    }
}
