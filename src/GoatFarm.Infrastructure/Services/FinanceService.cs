using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public partial class FinanceService : IFinanceService
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

    public Task<FinancePageViewModel> GetFinancePageAsync(string? month, CancellationToken cancellationToken = default) =>
        GetFinancePageAsync(month, FinancePageLoadOptions.Full, cancellationToken);

    public async Task<FinancePageViewModel> GetFinancePageAsync(string? month, FinancePageLoadOptions options, CancellationToken cancellationToken = default)
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
        var expManual = expenses.Where(e => !FinanceExpenseRules.IsDuplicateVaccinePurchase(e.Type)).Sum(e => e.Amount);
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

        var page = new FinancePageViewModel
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
            NewOwnerInvestment = new CreateOwnerInvestmentViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            DisplayIncome = incTot,
            DisplayExpense = expTot
        };
        await EnrichAccountingAsync(page, monthStart, monthEnd, options.IncludeTabRegisters, cancellationToken);
        if (options.IncludeIntegrityAndBalance)
        {
            page.Integrity = await GetIntegrityCheckAsync(cancellationToken);
            page.BalanceSheet = await BuildBalanceSheetAsync(page, cancellationToken);
            page.MonthlyClosing = await BuildMonthlyClosingRowsAsync(month, cancellationToken);
        }
        else
        {
            page.Integrity = new FinanceIntegrityViewModel();
            page.BalanceSheet = new FinanceBalanceSheetViewModel();
        }
        return page;
    }

    public async Task<FinanceMonthSnapshotViewModel> GetFinanceMonthSnapshotAsync(string? month, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var page = await GetFinancePageAsync(month, new FinancePageLoadOptions
        {
            IncludeTabRegisters = false,
            IncludeIntegrityAndBalance = false
        }, cancellationToken);

        return new FinanceMonthSnapshotViewModel
        {
            Month = month,
            TotalIncome = page.DisplayIncome,
            TotalExpense = page.DisplayExpense,
            Profit = page.Profit,
            IsLoss = page.IsLoss,
            Capital = page.Capital,
            IncomeLines = page.IncomeLines,
            ExpenseLines = page.ExpenseLines,
            Incomes = page.Incomes,
            Expenses = page.Expenses
        };
    }

    public async Task<FinanceStatementViewModel> GetStatementAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        var endExclusive = to.AddDays(1);
        var months = Math.Max(0, Math.Round((to.DayNumber - from.DayNumber) / 30.44m, 1));
        if (months == 0 && to >= from) months = 1;

        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= from && i.Date < endExclusive)
            .GroupBy(i => i.Type)
            .Select(g => new FinanceLineViewModel { Label = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var milk = await _context.MilkSales.AsNoTracking().Where(s => s.Date >= from && s.Date < endExclusive).SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0;
        var products = await _context.ProductSales.AsNoTracking()
            .Where(s => s.Date >= from && s.Date < endExclusive)
            .GroupBy(s => s.ProductName)
            .Select(g => new FinanceLineViewModel { Label = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var invoices = await _context.CustomerInvoices.AsNoTracking()
            .Where(i => i.Date >= from && i.Date < endExclusive && i.Status != InvoiceStatus.Void)
            .Where(i => !i.Description.StartsWith("Product sale:") && !i.Description.StartsWith("Live goat sale"))
            .SumAsync(i => (decimal?)i.Amount, cancellationToken) ?? 0;

        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.Date >= from && e.Date < endExclusive)
            .GroupBy(e => e.Type)
            .Select(g => new FinanceLineViewModel { Label = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var feed = await _context.FeedPurchases.AsNoTracking().Where(f => f.Date >= from && f.Date < endExclusive).SumAsync(f => (decimal?)f.Amount, cancellationToken) ?? 0;
        var vacc = await _context.VaccinePurchases.AsNoTracking().Where(v => v.Date >= from && v.Date < endExclusive).SumAsync(v => (decimal?)v.Amount, cancellationToken) ?? 0;
        var salary = GetStaffSalaryMonthlyTotal() * months;
        var recurring = GetRecurringMonthlyTotal() * months;
        var medicine = _feedService.CalculateFarmMedicineMonthly() * months;

        var inLines = new List<FinanceLineViewModel>();
        if (milk > 0) inLines.Add(new FinanceLineViewModel { Label = "Milk sales", Amount = milk });
        inLines.AddRange(products.Where(p => p.Amount > 0));
        if (invoices > 0) inLines.Add(new FinanceLineViewModel { Label = "Other farm sale", Amount = invoices });
        inLines.AddRange(incomes.Where(i => i.Amount > 0));
        var outLines = new List<FinanceLineViewModel>();
        if (feed > 0) outLines.Add(new FinanceLineViewModel { Label = "Feed bought", Amount = feed });
        if (medicine > 0) outLines.Add(new FinanceLineViewModel { Label = "Medicine (plan)", Amount = medicine });
        if (vacc > 0) outLines.Add(new FinanceLineViewModel { Label = "Vaccines bought", Amount = vacc });
        if (salary > 0) outLines.Add(new FinanceLineViewModel { Label = "Salaries", Amount = salary });
        if (recurring > 0) outLines.Add(new FinanceLineViewModel { Label = "Fixed & recurring", Amount = recurring });
        outLines.AddRange(expenses.Where(e => e.Amount > 0));

        return new FinanceStatementViewModel
        {
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd"),
            Income = inLines.Sum(x => x.Amount),
            Expense = outLines.Sum(x => x.Amount),
            IncomeLines = inLines.OrderByDescending(x => x.Amount).ToList(),
            ExpenseLines = outLines.OrderByDescending(x => x.Amount).ToList()
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
        EnsureMoneyAccount(model.AccountId);
        var entity = new Income
        {
            Type = model.Type,
            Amount = model.Amount,
            Date = model.Date,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.Incomes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await SyncCashMovementAsync($"fin-income:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Income, "Linked income", entity.Type, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment, AccountId = model.AccountId };
    }

    public async Task<ExpenseViewModel> AddExpenseAsync(CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        if (FinanceExpenseRules.IsDuplicateVaccinePurchase(model.Type))
            throw new InvalidOperationException("Vaccine costs belong on the Health page — not as a manual Vaccines expense.");
        EnsureMoneyAccount(model.AccountId);
        var entity = new Expense
        {
            Type = model.Type,
            Amount = model.Amount,
            Date = model.Date,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.Expenses.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await SyncCashMovementAsync($"fin-expense:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Expense, "Linked expense", entity.Type, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment, AccountId = model.AccountId };
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
        EnsureMoneyAccount(model.AccountId);
        var entity = new OwnerInvestment
        {
            Note = ComposeFundingNote(model),
            Amount = model.Amount,
            Date = model.Date
        };
        _context.OwnerInvestments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await SyncCashMovementAsync($"fin-funding:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Income, "Shareholder funding", entity.Note, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new OwnerInvestmentViewModel { Id = entity.Id, Note = entity.Note, Amount = entity.Amount, Date = entity.Date, AccountId = model.AccountId };
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
        EnsureMoneyAccount(model.AccountId);
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        await SyncCashMovementAsync($"fin-income:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Income, "Linked income", entity.Type, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new IncomeViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment, AccountId = model.AccountId };
    }

    public async Task<ExpenseViewModel?> UpdateExpenseAsync(int id, CreateExpenseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Expenses.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        EnsureMoneyAccount(model.AccountId);
        entity.Type = model.Type;
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        await SyncCashMovementAsync($"fin-expense:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Expense, "Linked expense", entity.Type, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseViewModel { Id = entity.Id, Type = entity.Type, Amount = entity.Amount, Date = entity.Date, Comment = entity.Comment, AccountId = model.AccountId };
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
        EnsureMoneyAccount(model.AccountId);
        entity.Note = ComposeFundingNote(model);
        entity.Amount = model.Amount;
        entity.Date = model.Date;
        await SyncCashMovementAsync($"fin-funding:{entity.Id}", entity.Date, entity.Amount, model.AccountId, LedgerTransactionKind.Income, "Shareholder funding", entity.Note, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new OwnerInvestmentViewModel { Id = entity.Id, Note = entity.Note, Amount = entity.Amount, Date = entity.Date, AccountId = model.AccountId };
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
        await RemoveLinkedCashAsync($"fin-income:{id}", cancellationToken);
        _context.Incomes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteExpenseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Expenses.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        await RemoveLinkedCashAsync($"fin-expense:{id}", cancellationToken);
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
        await RemoveLinkedCashAsync($"fin-funding:{id}", cancellationToken);
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

    private static void EnsureMoneyAccount(int? accountId)
    {
        if (accountId is null or <= 0)
            throw new InvalidOperationException(
                "Choose a Cash or Bank account. Every receipt and payment must post to an account — add one under Bank & Cash if needed.");
    }

    private static string ComposeFundingNote(CreateOwnerInvestmentViewModel model)
    {
        var purpose = model.Note.Trim();
        if (string.IsNullOrWhiteSpace(model.Shareholder)) return purpose;
        var kind = string.Equals(model.FundingType, "topup", StringComparison.OrdinalIgnoreCase)
            ? "Monthly top-up"
            : "Capital investment";
        return $"{model.Shareholder.Trim()} · {kind} · {purpose}";
    }

    private async Task RemoveLinkedCashAsync(string reference, CancellationToken cancellationToken)
    {
        var existing = await _context.LedgerTransactions.FirstOrDefaultAsync(t => t.Reference == reference, cancellationToken);
        if (existing is not null) _context.LedgerTransactions.Remove(existing);
    }

    private async Task SyncCashMovementAsync(string reference, DateOnly date, decimal amount, int? accountId, LedgerTransactionKind kind, string category, string? notes, CancellationToken cancellationToken)
    {
        var existing = await _context.LedgerTransactions.FirstOrDefaultAsync(t => t.Reference == reference, cancellationToken);
        if (accountId is null or <= 0 || amount <= 0)
        {
            if (existing is not null) _context.LedgerTransactions.Remove(existing);
            return;
        }

        var account = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Selected Cash/Bank account was not found. Add it under Bank & Cash.");

        if (kind is LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment)
        {
            var balance = await AccountBalanceAsync(account.Id, cancellationToken);
            var already = existing is not null && existing.AccountId == account.Id && existing.Kind == kind ? existing.Amount : 0;
            if (amount > balance + already)
                throw new InvalidOperationException($"Insufficient balance in {account.Name}. Available: {balance + already:N0}.");
        }

        if (existing is null)
        {
            existing = new LedgerTransaction { Reference = reference, CreatedDate = DateTime.UtcNow };
            _context.LedgerTransactions.Add(existing);
        }
        existing.Date = date;
        existing.Kind = kind;
        existing.AccountId = account.Id;
        existing.Amount = amount;
        existing.Category = category;
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? category : notes;
        existing.UpdatedDate = DateTime.UtcNow;
        if (!account.OpeningBalanceLocked)
        {
            account.OpeningBalanceLocked = true;
            account.UpdatedDate = DateTime.UtcNow;
        }
    }

    private async Task<decimal> AccountBalanceAsync(int accountId, CancellationToken cancellationToken)
    {
        var opening = await _context.MoneyAccounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => a.OpeningBalance).FirstAsync(cancellationToken);
        var tx = await _context.LedgerTransactions.AsNoTracking()
            .Where(t => t.AccountId == accountId || t.CounterAccountId == accountId)
            .ToListAsync(cancellationToken);
        var balance = opening;
        foreach (var t in tx)
        {
            if (t.Kind == LedgerTransactionKind.Transfer)
            {
                if (t.AccountId == accountId) balance -= t.Amount;
                if (t.CounterAccountId == accountId) balance += t.Amount;
            }
            else if (t.AccountId == accountId)
            {
                balance += t.Kind is LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment ? t.Amount : -t.Amount;
            }
        }
        return balance;
    }

    private async Task EnrichAccountingAsync(FinancePageViewModel page, DateOnly monthStart, DateOnly monthEnd, bool includeTabRegisters, CancellationToken cancellationToken)
    {
        var trendStart = monthStart.AddMonths(-5);
        var incomeRows = await _context.Incomes.AsNoTracking().Where(i => i.Date >= trendStart && i.Date < monthEnd).Select(i => new { i.Date, i.Amount, i.Type }).ToListAsync(cancellationToken);
        var milkRows = await _context.MilkSales.AsNoTracking().Where(s => s.Date >= trendStart && s.Date < monthEnd).Select(s => new { s.Date, s.Amount, s.Comment }).ToListAsync(cancellationToken);
        var productRows = await _context.ProductSales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.Date >= trendStart && s.Date < monthEnd)
            .Select(s => new { s.Id, s.Date, s.Amount, s.ProductName, s.IsCredit, Customer = s.Customer != null ? s.Customer.Name : null, s.Notes, s.CustomerId })
            .ToListAsync(cancellationToken);
        var invoiceRows = await _context.CustomerInvoices.AsNoTracking().Include(i => i.Customer)
            .OrderByDescending(i => i.Date).Take(400).ToListAsync(cancellationToken);
        var expenseRows = await _context.Expenses.AsNoTracking().Where(e => e.Date >= trendStart && e.Date < monthEnd).Select(e => new { e.Date, e.Amount, e.Type }).ToListAsync(cancellationToken);
        var feedRows = await _context.FeedPurchases.AsNoTracking().Where(f => f.Date >= trendStart && f.Date < monthEnd).Select(f => new { f.Date, f.Amount }).ToListAsync(cancellationToken);
        var vaccRows = await _context.VaccinePurchases.AsNoTracking().Where(v => v.Date >= trendStart && v.Date < monthEnd).Select(v => new { v.Date, v.Amount }).ToListAsync(cancellationToken);
        var goatSales = await _context.GoatSales.AsNoTracking().OrderByDescending(g => g.Date).Take(200)
            .Select(g => new { g.Id, g.Date, g.Price, g.Tag, g.Buyer, g.IsCredit, g.CustomerInvoiceId, g.WeightKg }).ToListAsync(cancellationToken);
        var salary = GetStaffSalaryMonthlyTotal();
        var recurring = GetRecurringMonthlyTotal();
        var medicine = _feedService.CalculateFarmMedicineMonthly();

        bool InMonth(DateOnly date) => date >= monthStart && date < monthEnd;
        bool ManualInvoice(CustomerInvoice invoice) =>
            invoice.Status != InvoiceStatus.Void
            && !invoice.Description.StartsWith("Product sale:")
            && !invoice.Description.StartsWith("Live goat sale");

        var monthInvoices = invoiceRows.Where(i => InMonth(i.Date) && ManualInvoice(i)).ToList();
        var monthProducts = productRows.Where(p => InMonth(p.Date)).ToList();
        page.DisplayIncome = page.TotalIncome + monthProducts.Sum(p => p.Amount) + monthInvoices.Sum(i => i.Amount);
        page.DisplayExpense = page.TotalExpense;
        page.Profit = page.DisplayIncome - page.DisplayExpense;
        page.IsLoss = page.Profit < 0;
        page.ProfitNote = page.Profit >= 0 ? "you made money this month" : "costs were higher than income this month";

        var incomeLines = new List<FinanceLineViewModel>();
        if (page.MilkIncome > 0) incomeLines.Add(new FinanceLineViewModel { Label = "Milk sales", Amount = page.MilkIncome });
        foreach (var group in monthProducts.GroupBy(p => string.IsNullOrWhiteSpace(p.ProductName) ? "Product sales" : p.ProductName))
            incomeLines.Add(new FinanceLineViewModel { Label = group.Key, Amount = group.Sum(x => x.Amount) });
        if (monthInvoices.Count > 0)
            incomeLines.Add(new FinanceLineViewModel { Label = "Other farm sale", Amount = monthInvoices.Sum(i => i.Amount) });
        foreach (var group in page.Incomes.GroupBy(i => i.Type))
            incomeLines.Add(new FinanceLineViewModel { Label = group.Key, Amount = group.Sum(x => x.Amount) });
        page.IncomeLines = incomeLines.Where(x => x.Amount > 0).OrderByDescending(x => x.Amount).ToList();

        var expenseLines = new List<FinanceLineViewModel>();
        if (page.FeedBoughtMonthly > 0) expenseLines.Add(new FinanceLineViewModel { Label = "Feed bought", Amount = page.FeedBoughtMonthly });
        if (page.MedicineMonthly > 0) expenseLines.Add(new FinanceLineViewModel { Label = "Medicine (plan)", Amount = page.MedicineMonthly });
        if (page.VaccineBoughtMonthly > 0) expenseLines.Add(new FinanceLineViewModel { Label = "Vaccines bought", Amount = page.VaccineBoughtMonthly });
        if (page.StaffSalaryMonthly > 0) expenseLines.Add(new FinanceLineViewModel { Label = "Salaries", Amount = page.StaffSalaryMonthly });
        foreach (var cost in page.RecurringCosts.Where(r => r.MonthlyAmount > 0))
            expenseLines.Add(new FinanceLineViewModel { Label = cost.Name, Amount = cost.MonthlyAmount });
        foreach (var group in page.Expenses.Where(e => !FinanceExpenseRules.IsDuplicateVaccinePurchase(e.Type)).GroupBy(e => e.Type))
            expenseLines.Add(new FinanceLineViewModel { Label = group.Key, Amount = group.Sum(x => x.Amount) });
        page.ExpenseLines = expenseLines.OrderByDescending(x => x.Amount).ToList();

        var trend = new List<FinanceTrendMonthViewModel>();
        for (var i = 5; i >= 0; i--)
        {
            var start = monthStart.AddMonths(-i);
            var end = start.AddMonths(1);
            var key = $"{start.Year:0000}-{start.Month:00}";
            bool In(DateOnly date) => date >= start && date < end;
            var inc = incomeRows.Where(x => In(x.Date)).Sum(x => x.Amount)
                + milkRows.Where(x => In(x.Date)).Sum(x => x.Amount)
                + productRows.Where(x => In(x.Date)).Sum(x => x.Amount)
                + invoiceRows.Where(x => In(x.Date) && ManualInvoice(x)).Sum(x => x.Amount);
            var exp = expenseRows.Where(x => In(x.Date) && !FinanceExpenseRules.IsDuplicateVaccinePurchase(x.Type)).Sum(x => x.Amount)
                + feedRows.Where(x => In(x.Date)).Sum(x => x.Amount)
                + vaccRows.Where(x => In(x.Date)).Sum(x => x.Amount)
                + salary + recurring + medicine;
            trend.Add(new FinanceTrendMonthViewModel { Month = key, Income = inc, Expense = exp });
        }
        page.Trend = trend;

        if (!includeTabRegisters)
            return;

        var matchedInvoiceIds = new HashSet<int>();
        var sales = new List<SaleRegisterRowViewModel>();
        foreach (var milkSale in milkRows.Where(x => InMonth(x.Date)))
        {
            sales.Add(new SaleRegisterRowViewModel
            {
                Source = "milk",
                DateDisplay = milkSale.Date.ToString("yyyy-MM-dd"),
                Kind = "Direct milk",
                Customer = string.IsNullOrWhiteSpace(milkSale.Comment) ? "Cash / direct" : milkSale.Comment,
                Status = "Paid",
                Amount = milkSale.Amount,
                Received = milkSale.Amount
            });
        }
        foreach (var sale in productRows)
        {
            var match = invoiceRows.FirstOrDefault(i =>
                i.CustomerId == sale.CustomerId && i.Date == sale.Date && i.Amount == sale.Amount && i.Description.StartsWith("Product sale:"));
            if (match is not null) matchedInvoiceIds.Add(match.Id);
            var received = match?.PaidAmount ?? (sale.IsCredit ? 0 : sale.Amount);
            var due = Math.Max(0, sale.Amount - received);
            sales.Add(new SaleRegisterRowViewModel
            {
                Source = "product",
                Id = sale.Id,
                DateDisplay = sale.Date.ToString("yyyy-MM-dd"),
                Kind = sale.ProductName,
                Customer = sale.Customer ?? "—",
                Status = due <= 0 ? "Paid" : received > 0 ? "Part paid" : "Credit",
                Amount = sale.Amount,
                Received = received,
                Due = due,
                DueDate = match?.DueDate?.ToString("yyyy-MM-dd"),
                Note = sale.Notes
            });
        }
        foreach (var sale in goatSales)
        {
            var match = sale.CustomerInvoiceId is int invoiceId ? invoiceRows.FirstOrDefault(i => i.Id == invoiceId) : null;
            if (match is not null) matchedInvoiceIds.Add(match.Id);
            var received = match?.PaidAmount ?? (sale.IsCredit ? 0 : sale.Price);
            var due = Math.Max(0, sale.Price - received);
            sales.Add(new SaleRegisterRowViewModel
            {
                Source = "goat",
                Id = sale.Id,
                DateDisplay = sale.Date.ToString("yyyy-MM-dd"),
                Kind = "Live goat · " + sale.Tag,
                Customer = string.IsNullOrWhiteSpace(sale.Buyer) ? "Cash / market" : sale.Buyer,
                Status = due <= 0 ? "Paid" : received > 0 ? "Part paid" : "Credit",
                Amount = sale.Price,
                Received = received,
                Due = due,
                DueDate = match?.DueDate?.ToString("yyyy-MM-dd"),
                Note = sale.WeightKg is > 0 ? $"{sale.WeightKg:N1} kg" : null
            });
        }
        foreach (var invoice in invoiceRows.Where(i => !matchedInvoiceIds.Contains(i.Id) && i.Status != InvoiceStatus.Void))
        {
            var due = Math.Max(0, invoice.Amount - invoice.PaidAmount);
            sales.Add(new SaleRegisterRowViewModel
            {
                Source = "invoice",
                Id = invoice.Id,
                DateDisplay = invoice.Date.ToString("yyyy-MM-dd"),
                Kind = invoice.Description,
                Customer = invoice.Customer.Name,
                Status = due <= 0 ? "Paid" : invoice.PaidAmount > 0 ? "Part paid" : "Credit",
                Amount = invoice.Amount,
                Received = invoice.PaidAmount,
                Due = due,
                DueDate = invoice.DueDate?.ToString("yyyy-MM-dd")
            });
        }
        page.Sales = sales.OrderByDescending(s => s.DateDisplay).ToList();

        var capex = await _context.ProjectExpenditures.AsNoTracking().Include(p => p.Project)
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .OrderByDescending(p => p.Date)
            .Select(p => new CapexRowViewModel
            {
                DateDisplay = p.Date.ToString("yyyy-MM-dd"),
                Name = p.Project.Name,
                Type = p.CostType ?? p.Description,
                Supplier = p.Vendor,
                Amount = p.Amount
            }).ToListAsync(cancellationToken);
        var assetCapex = await _context.FixedAssetRegisters.AsNoTracking()
            .Where(a => a.PurchaseDate >= monthStart && a.PurchaseDate < monthEnd)
            .Select(a => new CapexRowViewModel
            {
                DateDisplay = a.PurchaseDate!.Value.ToString("yyyy-MM-dd"),
                Name = a.Name,
                Type = a.Category.ToString(),
                Supplier = a.Notes,
                Amount = a.Cost
            }).ToListAsync(cancellationToken);
        page.Capex = capex.Concat(assetCapex).OrderByDescending(x => x.DateDisplay).ToList();
        page.ProjectSpend = await _context.ProjectExpenditures.AsNoTracking().SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;
        page.FixedAssetRegisterValue = await _context.FixedAssetRegisters.AsNoTracking().SumAsync(a => (decimal?)a.Cost, cancellationToken) ?? 0;
        page.FeedStockValue = await _context.FeedPrices.AsNoTracking().SumAsync(f => (decimal?)(f.StockKg * f.PricePerKg), cancellationToken) ?? 0;
        page.GoatsBoughtMonth = await _context.Goats.AsNoTracking()
            .Where(g => g.Source != GoatSource.Born && g.EventDate >= monthStart && g.EventDate < monthEnd)
            .SumAsync(g => (decimal?)g.PurchasePrice, cancellationToken) ?? 0;
        page.OpeningCash = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).SumAsync(a => (decimal?)a.OpeningBalance, cancellationToken) ?? 0;
        page.FundingHistory = await _context.OwnerInvestments.AsNoTracking().OrderByDescending(o => o.Date)
            .Select(o => new OwnerInvestmentViewModel { Id = o.Id, Note = o.Note, Amount = o.Amount, Date = o.Date })
            .ToListAsync(cancellationToken);

        var links = await _context.LedgerTransactions.AsNoTracking().Include(t => t.Account)
            .Where(t => t.Reference != null && (t.Reference.StartsWith("fin-income:") || t.Reference.StartsWith("fin-expense:") || t.Reference.StartsWith("fin-funding:")))
            .ToListAsync(cancellationToken);
        void ApplyLink<T>(IEnumerable<T> rows, string prefix, Action<T, int?, string?> set) where T : class
        {
            foreach (var row in rows)
            {
                var id = row switch
                {
                    IncomeViewModel i => i.Id,
                    ExpenseViewModel e => e.Id,
                    OwnerInvestmentViewModel o => o.Id,
                    _ => 0
                };
                var link = links.FirstOrDefault(t => t.Reference == prefix + id);
                if (link is not null) set(row, link.AccountId, link.Account?.Name);
            }
        }
        ApplyLink(page.Incomes, "fin-income:", (row, id, name) => { row.AccountId = id; row.AccountName = name; });
        ApplyLink(page.Expenses, "fin-expense:", (row, id, name) => { row.AccountId = id; row.AccountName = name; });
        ApplyLink(page.OwnerInvestments, "fin-funding:", (row, id, name) => { row.AccountId = id; row.AccountName = name; });
    }
}
