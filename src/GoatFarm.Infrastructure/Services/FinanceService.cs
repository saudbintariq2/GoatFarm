using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class FinanceService : IFinanceService
{
    private readonly GoatFarmDbContext _context;
    private readonly IFeedService _feedService;
    private readonly IMilkService _milkService;

    public FinanceService(GoatFarmDbContext context, IFeedService feedService, IMilkService milkService)
    {
        _context = context;
        _feedService = feedService;
        _milkService = milkService;
    }

    public async Task<FinancePageViewModel> GetFinancePageAsync(string? month, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        var lv = GetLivestockValue();
        var av = GetAssetsValue();
        var cap = lv + av;
        var boughtN = await _context.Goats.CountAsync(g => g.PurchasePrice > 0, cancellationToken);

        var assets = await _context.Assets.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => new AssetViewModel { Id = a.Id, Name = a.Name, Type = a.Type, Cost = a.Cost })
            .ToListAsync(cancellationToken);

        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= monthStart && i.Date < monthEnd)
            .OrderByDescending(i => i.Date)
            .Select(i => new IncomeViewModel { Id = i.Id, Type = i.Type, Amount = i.Amount, Date = i.Date })
            .ToListAsync(cancellationToken);

        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.Date >= monthStart && e.Date < monthEnd)
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseViewModel { Id = e.Id, Type = e.Type, Amount = e.Amount, Date = e.Date })
            .ToListAsync(cancellationToken);

        var milkInc = _milkService.GetMilkIncomeMonth(month);
        var milkL = _milkService.GetMilkLitersSold(month);
        var incManual = incomes.Sum(i => i.Amount);
        var incTot = incManual + milkInc;
        var feedM = _feedService.CalculateFarmFeedMonthly();
        var expManual = expenses.Sum(e => e.Amount);
        var expTot = expManual + feedM;
        var profit = incTot - expTot;

        return new FinancePageViewModel
        {
            Month = month,
            Profit = profit,
            IsLoss = profit < 0,
            ProfitNote = profit >= 0 ? "you made money this month" : "costs were higher than income this month",
            Capital = cap,
            LivestockValue = lv,
            BoughtGoatCount = boughtN,
            TotalIncome = incTot,
            TotalExpense = expTot,
            FeedMonthly = feedM,
            MilkIncome = milkInc,
            MilkLitersSold = milkL,
            Assets = assets,
            Incomes = incomes,
            Expenses = expenses,
            NewIncome = new CreateIncomeViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            NewExpense = new CreateExpenseViewModel { Date = DateOnly.FromDateTime(DateTime.Today) }
        };
    }

    public async Task<AssetViewModel> AddAssetAsync(CreateAssetViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Asset { Name = model.Name.Trim(), Type = model.Type, Cost = model.Cost };
        _context.Assets.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new AssetViewModel { Id = entity.Id, Name = entity.Name, Type = entity.Type, Cost = entity.Cost };
    }

    public async Task<IncomeViewModel> AddIncomeAsync(CreateIncomeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Income { Type = model.Type, Amount = model.Amount, Date = model.Date };
        _context.Incomes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date };
    }

    public async Task<ExpenseViewModel> AddExpenseAsync(CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Expense { Type = model.Type, Amount = model.Amount, Date = model.Date };
        _context.Expenses.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date };
    }

    public async Task<AssetViewModel?> UpdateAssetAsync(int id, CreateAssetViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Assets.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Type = model.Type;
        entity.Cost = model.Cost;
        await _context.SaveChangesAsync(cancellationToken);
        return new AssetViewModel { Id = entity.Id, Name = entity.Name, Type = entity.Type, Cost = entity.Cost };
    }

    public async Task<IncomeViewModel?> UpdateIncomeAsync(int id, CreateIncomeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Incomes.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date };
    }

    public async Task<ExpenseViewModel?> UpdateExpenseAsync(int id, CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Expenses.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date };
    }

    public async Task<bool> DeleteAssetAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Assets.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Assets.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteIncomeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Incomes.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Incomes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteExpenseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Expenses.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Expenses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal GetLivestockValue() =>
        _context.Goats.AsNoTracking().Sum(g => g.PurchasePrice);

    public decimal GetAssetsValue() =>
        _context.Assets.AsNoTracking().Sum(a => a.Cost);

    public decimal GetCapital() => GetLivestockValue() + GetAssetsValue();
}
