using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class FinanceService : IFinanceService
{
    private readonly GoatFarmDbContext _context;
    private readonly IFeedService _feedService;
    private readonly IMilkService _milkService;
    private readonly IVaccineService _vaccineService;

    public FinanceService(
        GoatFarmDbContext context,
        IFeedService feedService,
        IMilkService milkService,
        IVaccineService vaccineService)
    {
        _context = context;
        _feedService = feedService;
        _milkService = milkService;
        _vaccineService = vaccineService;
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
            .Select(a => new AssetViewModel { Id = a.Id, Name = a.Name, Type = a.Type, Cost = a.Cost, Comment = a.Comment })
            .ToListAsync(cancellationToken);

        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= monthStart && i.Date < monthEnd)
            .OrderByDescending(i => i.Date)
            .Select(i => new IncomeViewModel { Id = i.Id, Type = i.Type, Amount = i.Amount, Date = i.Date, Comment = i.Comment })
            .ToListAsync(cancellationToken);

        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.Date >= monthStart && e.Date < monthEnd)
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseViewModel { Id = e.Id, Type = e.Type, Amount = e.Amount, Date = e.Date, Comment = e.Comment })
            .ToListAsync(cancellationToken);

        var recurringCosts = await _context.RecurringCosts.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(r => new RecurringCostViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Amount = r.Amount,
                Period = r.Period == RecurringCostPeriod.Year ? "year" : "month",
                MonthlyAmount = r.Period == RecurringCostPeriod.Year ? r.Amount / 12m : r.Amount
            })
            .ToListAsync(cancellationToken);

        var ownerInvestments = await _context.OwnerInvestments.AsNoTracking()
            .Where(o => o.Date >= monthStart && o.Date < monthEnd)
            .OrderByDescending(o => o.Date)
            .Select(o => new OwnerInvestmentViewModel { Id = o.Id, Note = o.Note, Amount = o.Amount, Date = o.Date })
            .ToListAsync(cancellationToken);

        var ownerInvMonth = ownerInvestments.Sum(o => o.Amount);
        var ownerInvTotal = await _context.OwnerInvestments.AsNoTracking().SumAsync(o => o.Amount, cancellationToken);

        var milkInc = _milkService.GetMilkIncomeMonth(month);
        var milkL = _milkService.GetMilkLitersSold(month);
        var incManual = incomes.Sum(i => i.Amount);
        var incTot = incManual + milkInc;

        var feedBought = _feedService.GetFeedPurchasedMonthly(month);
        var feedKg = _feedService.GetFeedPurchasedKg(month);
        var medM = _feedService.CalculateFarmMedicineMonthly();
        var recurM = GetRecurringMonthlyTotal();
        var vaccBought = _vaccineService.GetVaccinePurchasedMonthly(month);
        var expManual = expenses.Sum(e => e.Amount);
        var expTot = expManual + feedBought + medM + recurM + vaccBought;
        var profit = incTot - expTot;
        var feedPlanBudget = _feedService.CalculateFarmFeedMonthly();

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
            FeedMonthly = feedPlanBudget,
            FeedBoughtMonthly = feedBought,
            FeedBoughtKg = feedKg,
            MedicineMonthly = medM,
            RecurringMonthly = recurM,
            VaccineBoughtMonthly = vaccBought,
            ManualExpenseMonthly = expManual,
            MilkIncome = milkInc,
            MilkLitersSold = milkL,
            OwnerInvestmentMonthTotal = ownerInvMonth,
            OwnerInvestmentTotal = ownerInvTotal,
            Assets = assets,
            Incomes = incomes,
            Expenses = expenses,
            RecurringCosts = recurringCosts,
            OwnerInvestments = ownerInvestments,
            NewIncome = new CreateIncomeViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            NewExpense = new CreateExpenseViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            NewOwnerInvestment = new CreateOwnerInvestmentViewModel { Date = DateOnly.FromDateTime(DateTime.Today) }
        };
    }

    public async Task<AssetViewModel> AddAssetAsync(CreateAssetViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Asset
        {
            Name = model.Name.Trim(),
            Type = model.Type,
            Cost = model.Cost,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.Assets.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new AssetViewModel { Id = entity.Id, Name = entity.Name, Type = entity.Type, Cost = entity.Cost, Comment = entity.Comment };
    }

    public async Task<IncomeViewModel> AddIncomeAsync(CreateIncomeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Income
        {
            Type = model.Type,
            Amount = model.Amount,
            Date = model.Date,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.Incomes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment };
    }

    public async Task<ExpenseViewModel> AddExpenseAsync(CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Expense
        {
            Type = model.Type,
            Amount = model.Amount,
            Date = model.Date,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.Expenses.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment };
    }

    public async Task<RecurringCostViewModel> AddRecurringCostAsync(CreateRecurringCostViewModel model, CancellationToken cancellationToken = default)
    {
        var period = string.Equals(model.Period, "year", StringComparison.OrdinalIgnoreCase)
            ? RecurringCostPeriod.Year
            : RecurringCostPeriod.Month;
        var entity = new RecurringCost { Name = model.Name.Trim(), Amount = model.Amount, Period = period };
        _context.RecurringCosts.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapRecurring(entity);
    }

    public async Task<OwnerInvestmentViewModel> AddOwnerInvestmentAsync(CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new OwnerInvestment
        {
            Note = model.Note.Trim(),
            Amount = model.Amount,
            Date = model.Date
        };
        _context.OwnerInvestments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new OwnerInvestmentViewModel { Id = entity.Id, Note = entity.Note, Amount = entity.Amount, Date = entity.Date };
    }

    public async Task<AssetViewModel?> UpdateAssetAsync(int id, CreateAssetViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Assets.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Type = model.Type;
        entity.Cost = model.Cost;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return new AssetViewModel { Id = entity.Id, Name = entity.Name, Type = entity.Type, Cost = entity.Cost, Comment = entity.Comment };
    }

    public async Task<IncomeViewModel?> UpdateIncomeAsync(int id, CreateIncomeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Incomes.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment };
    }

    public async Task<ExpenseViewModel?> UpdateExpenseAsync(int id, CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Expenses.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment };
    }

    public async Task<RecurringCostViewModel?> UpdateRecurringCostAsync(int id, CreateRecurringCostViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.RecurringCosts.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Amount = model.Amount;
        entity.Period = string.Equals(model.Period, "year", StringComparison.OrdinalIgnoreCase)
            ? RecurringCostPeriod.Year
            : RecurringCostPeriod.Month;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return MapRecurring(entity);
    }

    public async Task<OwnerInvestmentViewModel?> UpdateOwnerInvestmentAsync(int id, CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OwnerInvestments.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Note = model.Note.Trim();
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        await _context.SaveChangesAsync(cancellationToken);
        return new OwnerInvestmentViewModel { Id = entity.Id, Note = entity.Note, Amount = entity.Amount, Date = entity.Date };
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

    public async Task<bool> DeleteRecurringCostAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.RecurringCosts.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.RecurringCosts.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteOwnerInvestmentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OwnerInvestments.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.OwnerInvestments.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal GetLivestockValue() =>
        _context.Goats.AsNoTracking().Sum(g => g.PurchasePrice);

    public decimal GetAssetsValue() =>
        _context.Assets.AsNoTracking().Sum(a => a.Cost);

    public decimal GetCapital() => GetLivestockValue() + GetAssetsValue();

    public decimal GetRecurringMonthlyTotal() =>
        _context.RecurringCosts.AsNoTracking()
            .Sum(r => r.Period == RecurringCostPeriod.Year ? r.Amount / 12m : r.Amount);

    private static RecurringCostViewModel MapRecurring(RecurringCost entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Amount = entity.Amount,
        Period = entity.Period == RecurringCostPeriod.Year ? "year" : "month",
        MonthlyAmount = entity.Period == RecurringCostPeriod.Year ? entity.Amount / 12m : entity.Amount
    };
}
