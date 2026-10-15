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
        var salaryM = GetStaffSalaryMonthlyTotal();
        var vaccBought = _vaccineService.GetVaccinePurchasedMonthly(month);
        var expManual = expenses.Sum(e => e.Amount);
        var expTot = expManual + feedBought + medM + recurM + vaccBought + salaryM;

        var employees = await _context.Employees.AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Role = e.Role,
                MonthlySalary = e.MonthlySalary
            })
            .ToListAsync(cancellationToken);
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
            StaffSalaryMonthly = salaryM,
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
            Employees = employees,
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

    public decimal GetStaffSalaryMonthlyTotal() =>
        _context.Employees.AsNoTracking().Sum(e => e.MonthlySalary);

    public async Task<EmployeeViewModel> AddEmployeeAsync(CreateEmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Employee
        {
            Name = model.Name.Trim(),
            Role = string.IsNullOrWhiteSpace(model.Role) ? null : model.Role.Trim(),
            MonthlySalary = model.MonthlySalary
        };
        _context.Employees.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new EmployeeViewModel { Id = entity.Id, Name = entity.Name, Role = entity.Role, MonthlySalary = entity.MonthlySalary };
    }

    public async Task<EmployeeViewModel?> UpdateEmployeeAsync(int id, CreateEmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Employees.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Role = string.IsNullOrWhiteSpace(model.Role) ? null : model.Role.Trim();
        entity.MonthlySalary = model.MonthlySalary;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new EmployeeViewModel { Id = entity.Id, Name = entity.Name, Role = entity.Role, MonthlySalary = entity.MonthlySalary };
    }

    public async Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Employees.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Employees.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Read-only: finds expenses and recurring costs that look like salary, wage or salary-advance payments, which the
    /// totals count on top of the automatic salary from the Employees list. Changes nothing.
    /// </summary>
    public async Task<SalaryReconciliationViewModel> GetSalaryReconciliationAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _context.Employees.AsNoTracking().Select(e => new { e.Name, e.MonthlySalary }).ToListAsync(cancellationToken);
        var autoSalary = employees.Sum(e => e.MonthlySalary);
        var names = employees.Select(e => e.Name.Trim()).Where(n => n.Length >= 3).ToList();

        string? MatchEmployee(params string?[] texts) => names.FirstOrDefault(n =>
            texts.Any(t => t is not null && t.Contains(n, StringComparison.OrdinalIgnoreCase)));

        SalaryReconciliationRowViewModel? Assess(string source, int id, DateOnly? date, string type, string? comment, decimal amount)
        {
            var kind = SalaryExpenseRules.Classify(type);
            var commentOnly = kind == SalaryExpenseKind.None && SalaryExpenseRules.MentionsSalary(comment);
            if (kind == SalaryExpenseKind.None && !commentOnly) return null;

            var employee = MatchEmployee(type, comment);
            var row = new SalaryReconciliationRowViewModel
            {
                Source = source, Id = id, Date = date, Type = type, Comment = comment, Amount = amount, MatchedEmployee = employee
            };
            if (autoSalary <= 0)
            {
                row.Outcome = SalaryReconciliationOutcome.NotDoubleCounted;
                row.Reason = "No employee salaries are on the Employees list, so this is the only record of this payment. Keep it.";
            }
            else if (employee is not null && !commentOnly)
            {
                row.Outcome = SalaryReconciliationOutcome.ConfirmedDoubleCount;
                row.Reason = kind == SalaryExpenseKind.SalaryAdvance
                    ? $"{employee}'s full monthly salary is already counted from the Employees list; this advance is part of it and is counted again."
                    : $"{employee}'s salary is already counted from the Employees list and is counted again here.";
            }
            else
            {
                row.Outcome = SalaryReconciliationOutcome.NeedsReview;
                row.Reason = commentOnly
                    ? "The comment mentions a salary or advance but the type does not. Check whether this was a salary payment."
                    : "Looks like a salary payment but does not name anyone on the Employees list. If it was for a listed employee, it is counted twice; if it was for someone else (for example casual labour), it is not.";
            }
            return row;
        }

        var expenses = await _context.Expenses.AsNoTracking().OrderBy(e => e.Date).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.Date, e.Type, e.Comment, e.Amount }).ToListAsync(cancellationToken);
        var recurring = await _context.RecurringCosts.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);

        var expenseRows = expenses.Select(e => Assess("Expense", e.Id, e.Date, e.Type, e.Comment, e.Amount)).OfType<SalaryReconciliationRowViewModel>().ToList();
        var recurringRows = recurring.Select(r => Assess("Recurring cost", r.Id, null, r.Name, null, MapRecurring(r).MonthlyAmount))
            .OfType<SalaryReconciliationRowViewModel>().ToList();

        decimal Sum(SalaryReconciliationOutcome o) => expenseRows.Where(r => r.Outcome == o).Sum(r => r.Amount);
        return new SalaryReconciliationViewModel
        {
            AutoSalaryMonthly = autoSalary,
            EmployeeCount = employees.Count,
            Rows = [.. expenseRows, .. recurringRows],
            ConfirmedTotal = Sum(SalaryReconciliationOutcome.ConfirmedDoubleCount),
            NeedsReviewTotal = Sum(SalaryReconciliationOutcome.NeedsReview),
            NotDoubleCountedTotal = Sum(SalaryReconciliationOutcome.NotDoubleCounted),
            RecurringFlaggedMonthly = recurringRows.Where(r => r.Outcome != SalaryReconciliationOutcome.NotDoubleCounted).Sum(r => r.Amount),
            Months = expenseRows.GroupBy(r => r.Date!.Value.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => new SalaryReconciliationMonthViewModel
            {
                Month = g.Key,
                AutoSalary = autoSalary,
                ConfirmedAmount = g.Where(r => r.Outcome == SalaryReconciliationOutcome.ConfirmedDoubleCount).Sum(r => r.Amount),
                NeedsReviewAmount = g.Where(r => r.Outcome == SalaryReconciliationOutcome.NeedsReview).Sum(r => r.Amount)
            }).ToList()
        };
    }

    private static RecurringCostViewModel MapRecurring(RecurringCost entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Amount = entity.Amount,
        Period = entity.Period == RecurringCostPeriod.Year ? "year" : "month",
        MonthlyAmount = entity.Period == RecurringCostPeriod.Year ? entity.Amount / 12m : entity.Amount
    };
}
