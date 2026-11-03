using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Projects;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class ProjectsService : IProjectsService
{
    private readonly GoatFarmDbContext _context;
    private readonly ILookupService _lookups;

    public ProjectsService(GoatFarmDbContext context, ILookupService lookups)
    {
        _context = context;
        _lookups = lookups;
    }

    public async Task<ProjectsPageViewModel> GetPageAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _context.FarmProjects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var exp = await _context.ProjectExpenditures.AsNoTracking()
            .Include(e => e.Project).Include(e => e.Account)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
        var assets = await _context.FixedAssetRegisters.AsNoTracking().Include(a => a.Project)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var solar = await _context.SolarProductionLogs.AsNoTracking().Include(s => s.Project)
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id).Take(200).ToListAsync(cancellationToken);
        var accounts = await _context.MoneyAccounts.AsNoTracking()
            .Where(a => a.IsActive).OrderBy(a => a.Name)
            .Select(a => new ProjectAccountOptionViewModel { Id = a.Id, Name = a.Name })
            .ToListAsync(cancellationToken);

        var spentByProject = exp.GroupBy(e => e.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var capitalizedByProject = assets
            .Where(a => a.ProjectId is > 0 && a.ProjectCapitalized)
            .GroupBy(a => a.ProjectId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost));

        decimal CipFor(int projectId)
        {
            var spent = spentByProject.GetValueOrDefault(projectId);
            var cap = capitalizedByProject.GetValueOrDefault(projectId);
            return Math.Max(0, spent - cap);
        }

        var projectRows = projects.Select(p =>
        {
            var spent = spentByProject.GetValueOrDefault(p.Id);
            var cip = CipFor(p.Id);
            return new ProjectRowViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Note = p.Description,
                Category = p.Category ?? "",
                Status = ProjectParity.StatusLabel(p.Status),
                Funding = p.Funding ?? "",
                BudgetAmount = p.BudgetAmount,
                Spent = spent,
                Cip = cip,
                Remaining = p.BudgetAmount - spent,
                StartDate = p.StartDate?.ToString("yyyy-MM-dd")
            };
        }).ToList();

        return new ProjectsPageViewModel
        {
            TotalBudget = projects.Sum(p => p.BudgetAmount),
            TotalSpent = exp.Sum(e => e.Amount),
            TotalCip = projectRows.Sum(p => p.Cip),
            TotalCapex = assets.Sum(a => a.Cost),
            ActiveBudget = projects.Where(p => p.Status == ProjectStatus.Active).Sum(p => p.BudgetAmount),
            Projects = projectRows,
            Expenditures = exp.Select(e =>
            {
                var paid = e.PaidAmount;
                var due = Math.Max(0, e.Amount - paid);
                return new ProjectExpenditureRowViewModel
                {
                    Id = e.Id,
                    ProjectId = e.ProjectId,
                    ProjectName = e.Project.Name,
                    DateDisplay = e.Date.ToString("yyyy-MM-dd"),
                    CostType = e.CostType ?? e.Description,
                    Description = e.Description,
                    Item = e.Item,
                    Model = e.Model,
                    Qty = e.Qty,
                    Unit = e.Unit,
                    Spec = e.Spec,
                    Warranty = e.WarrantyUntil?.ToString("yyyy-MM-dd"),
                    Supplier = e.Vendor,
                    FundingSource = e.FundingSource ?? e.Project.Funding,
                    PaymentStatus = string.IsNullOrWhiteSpace(e.PaymentStatus)
                        ? (due <= 0 ? "Paid" : paid > 0 ? "Part Paid" : "Credit")
                        : e.PaymentStatus,
                    AccountName = e.Account?.Name,
                    AccountId = e.AccountId,
                    DueDate = e.DueDate?.ToString("yyyy-MM-dd"),
                    Note = e.Note,
                    Amount = e.Amount,
                    PaidAmount = paid,
                    Payable = due
                };
            }).ToList(),
            FixedAssets = assets.Select(a => new FixedAssetRowViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Category = ProjectParity.AssetCategoryLabel(a.Category, a.CategoryName),
                Cost = a.Cost,
                PurchaseDate = a.PurchaseDate?.ToString("yyyy-MM-dd"),
                ProjectId = a.ProjectId,
                ProjectName = a.Project?.Name,
                Location = a.Location,
                Supplier = a.Supplier,
                ProjectCapitalized = a.ProjectCapitalized,
                PaidAmount = a.PaidAmount,
                Due = a.ProjectCapitalized ? 0 : Math.Max(0, a.Cost - a.PaidAmount)
            }).ToList(),
            SolarLogs = solar.Select(s => new SolarLogRowViewModel
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                ProjectName = s.Project?.Name ?? "—",
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                KwhGenerated = s.KwhGenerated,
                SavingsEstimate = s.SavingsEstimate,
                MaintenanceAmount = s.MaintenanceAmount,
                InstalledCapacityKw = s.InstalledCapacityKw,
                Notes = s.Notes
            }).ToList(),
            Accounts = accounts,
            ProjectCategories = await _lookups.GetListAsync(LookupSettingKeys.ProjectCategories, cancellationToken),
            CostTypes = await _lookups.GetListAsync(LookupSettingKeys.CapexCostTypes, cancellationToken),
            AssetCategories = await _lookups.GetListAsync(LookupSettingKeys.ProjectAssetCategories, cancellationToken),
            FundingSources = await _lookups.GetListAsync(LookupSettingKeys.ProjectFundingSources, cancellationToken)
        };
    }

    public async Task<object> SaveProjectAsync(SaveProjectViewModel model, CancellationToken cancellationToken = default)
    {
        var name = model.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a project name.");

        FarmProject row;
        if (model.Id is > 0)
            row = await _context.FarmProjects.FirstAsync(p => p.Id == model.Id, cancellationToken);
        else { row = new FarmProject(); _context.FarmProjects.Add(row); }
        row.Name = name;
        row.Description = TrimOrNull(model.Description);
        row.Category = TrimOrNull(model.Category);
        row.Funding = TrimOrNull(model.Funding);
        row.Status = ProjectParity.ParseStatus(model.Status);
        row.StartDate = model.StartDate;
        row.TargetEndDate = model.TargetEndDate;
        row.BudgetAmount = Math.Max(0, model.BudgetAmount);
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteProjectAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.FarmProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null) return false;
        var expIds = await _context.ProjectExpenditures.Where(e => e.ProjectId == id).Select(e => e.Id).ToListAsync(cancellationToken);
        foreach (var expId in expIds)
            await ReverseMoneyAsync("capex", expId, cancellationToken);
        _context.FarmProjects.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveExpenditureAsync(SaveProjectExpenditureViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.ProjectId <= 0)
            throw new InvalidOperationException("Select a project.");
        if (model.Amount <= 0)
            throw new InvalidOperationException("Enter the amount.");

        var project = await _context.FarmProjects.FirstOrDefaultAsync(p => p.Id == model.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("That project was not found.");

        var status = NormalizePaymentStatus(model.PaymentStatus);
        var paid = ClampPaid(model.Amount, model.PaidAmount, status);
        if (status == "Part Paid" && (paid <= 0 || paid >= model.Amount))
            throw new InvalidOperationException("For Part Paid, enter an amount greater than 0 and less than the total.");
        if (paid > 0 && model.AccountId is not > 0)
            throw new InvalidOperationException("Select the Cash / Bank account used for this payment.");
        if (status != "Paid" && model.DueDate is null)
            throw new InvalidOperationException("Enter the due date for the outstanding amount.");

        ProjectExpenditure row;
        if (model.Id is > 0)
            row = await _context.ProjectExpenditures.FirstAsync(e => e.Id == model.Id, cancellationToken);
        else { row = new ProjectExpenditure(); _context.ProjectExpenditures.Add(row); }

        var costType = TrimOrNull(model.CostType) ?? "Other Capital Cost";
        var item = TrimOrNull(model.Item);
        row.ProjectId = model.ProjectId;
        row.Date = model.Date == default ? DateOnly.FromDateTime(DateTime.Today) : model.Date;
        row.CostType = costType;
        row.Item = item;
        row.Qty = Math.Max(0, model.Qty);
        row.Unit = TrimOrNull(model.Unit);
        row.Model = TrimOrNull(model.Model);
        row.Spec = TrimOrNull(model.Spec);
        row.WarrantyUntil = model.WarrantyUntil;
        row.Description = TrimOrNull(model.Description) ?? item ?? costType;
        row.Amount = model.Amount;
        row.PaidAmount = paid;
        row.PaymentStatus = status;
        row.AccountId = paid > 0 ? model.AccountId : null;
        row.FundingSource = TrimOrNull(model.FundingSource) ?? project.Funding;
        row.DueDate = status == "Paid" ? null : model.DueDate;
        row.Vendor = TrimOrNull(model.Vendor);
        row.Note = TrimOrNull(model.Note);
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await ReverseMoneyAsync("capex", row.Id, cancellationToken);
        await PostCapexMoneyAsync(row, project.Name, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteExpenditureAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.ProjectExpenditures.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (row is null) return false;
        await ReverseMoneyAsync("capex", id, cancellationToken);
        _context.ProjectExpenditures.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> PayExpenditureAsync(PayProjectExpenditureViewModel model, CancellationToken cancellationToken = default)
    {
        var row = await _context.ProjectExpenditures.Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == model.Id, cancellationToken)
            ?? throw new InvalidOperationException("That capital cost was not found.");
        var due = Math.Max(0, row.Amount - row.PaidAmount);
        if (due <= 0) throw new InvalidOperationException("This capital cost is already paid.");
        var amt = Math.Max(0, Math.Min(due, model.Amount));
        if (amt <= 0) throw new InvalidOperationException("Enter a payment amount.");
        if (model.AccountId <= 0) throw new InvalidOperationException("Choose the Cash/Bank account.");

        row.PaidAmount += amt;
        row.AccountId = model.AccountId;
        row.PaymentStatus = row.PaidAmount >= row.Amount ? "Paid" : "Part Paid";
        if (row.PaymentStatus == "Paid") row.DueDate = null;
        row.UpdatedDate = DateTime.UtcNow;

        await EnsureAccountCanPayAsync(model.AccountId, amt, cancellationToken);
        _context.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Kind = LedgerTransactionKind.Expense,
            AccountId = model.AccountId,
            Amount = amt,
            Category = "Capital expenditure",
            Reference = "capex:" + row.Id + ":p" + DateTime.UtcNow.Ticks,
            Notes = $"CAPEX payment · {row.Project.Name}"
        });
        var sid = row.Id.ToString();
        var payable = await _context.SupplierPayables
            .FirstOrDefaultAsync(p => p.SourceType == "capital_expenditure" && p.SourceId == sid, cancellationToken);
        if (payable is not null)
        {
            payable.PaidAmount = row.PaidAmount;
            payable.Status = row.PaidAmount >= row.Amount ? PayableStatus.Paid : PayableStatus.Partial;
            if (payable.Status == PayableStatus.Paid) payable.DueDate = null;
            payable.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id, paid = row.PaidAmount };
    }

    public async Task<object> SaveFixedAssetAsync(SaveFixedAssetViewModel model, CancellationToken cancellationToken = default)
    {
        var name = model.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || model.Cost <= 0)
            throw new InvalidOperationException("Enter asset name and value.");

        var projectLinked = model.ProjectId is > 0;
        if (projectLinked && model.PaidAmount > 0)
            throw new InvalidOperationException("Project-linked assets should be capitalized from CIP. Do not pay them again here.");

        var paid = projectLinked ? 0 : Math.Max(0, Math.Min(model.Cost, model.PaidAmount));
        if (!projectLinked && paid > 0 && model.AccountId is not > 0)
            throw new InvalidOperationException("Select the Cash / Bank account used for this payment.");

        FixedAssetRegister row;
        if (model.Id is > 0)
            row = await _context.FixedAssetRegisters.FirstAsync(a => a.Id == model.Id, cancellationToken);
        else { row = new FixedAssetRegister(); _context.FixedAssetRegisters.Add(row); }

        var categoryName = TrimOrNull(model.Category) ?? "Other Asset";
        row.Name = name;
        row.CategoryName = categoryName;
        row.Category = ProjectParity.ParseAssetCategory(categoryName);
        row.Cost = model.Cost;
        row.PurchaseDate = model.PurchaseDate;
        row.ProjectId = projectLinked ? model.ProjectId : null;
        row.ProjectCapitalized = projectLinked;
        row.Location = TrimOrNull(model.Location);
        row.Supplier = TrimOrNull(model.Supplier);
        row.PaidAmount = paid;
        row.AccountId = projectLinked ? null : (paid > 0 ? model.AccountId : null);
        row.WarrantyUntil = model.WarrantyUntil;
        row.Notes = TrimOrNull(model.Notes);
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await ReverseMoneyAsync("fixed_asset", row.Id, cancellationToken);
        if (!projectLinked)
            await PostAssetMoneyAsync(row, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteFixedAssetAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.FixedAssetRegisters.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (row is null) return false;
        await ReverseMoneyAsync("fixed_asset", id, cancellationToken);
        var linked = await _context.FarmProjects.Where(p => p.CapitalizedAssetId == id).ToListAsync(cancellationToken);
        foreach (var p in linked)
        {
            p.CapitalizedAssetId = null;
            p.CapitalizedAt = null;
            if (p.Status == ProjectStatus.Completed) p.Status = ProjectStatus.Active;
            p.UpdatedDate = DateTime.UtcNow;
        }
        _context.FixedAssetRegisters.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> CapitalizeProjectAsync(CapitalizeProjectViewModel model, CancellationToken cancellationToken = default)
    {
        var name = model.AssetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a fixed asset name.");

        var project = await _context.FarmProjects.FirstOrDefaultAsync(p => p.Id == model.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("That project was not found.");
        var spent = await _context.ProjectExpenditures.Where(e => e.ProjectId == project.Id).SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
        var capitalized = await _context.FixedAssetRegisters
            .Where(a => a.ProjectId == project.Id && a.ProjectCapitalized)
            .SumAsync(a => (decimal?)a.Cost, cancellationToken) ?? 0;
        var cip = Math.Max(0, spent - capitalized);
        if (cip <= 0)
            throw new InvalidOperationException("There is no uncapitalized project cost to convert.");

        var inService = model.InServiceDate ?? DateOnly.FromDateTime(DateTime.Today);
        var asset = new FixedAssetRegister
        {
            Name = name,
            CategoryName = string.IsNullOrWhiteSpace(project.Category) ? "Buildings / Sheds" : project.Category,
            Category = ProjectParity.ParseAssetCategory(project.Category),
            Cost = cip,
            PurchaseDate = inService,
            ProjectId = project.Id,
            ProjectCapitalized = true,
            PaidAmount = 0,
            Notes = "Capitalized from completed project: " + project.Name,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.FixedAssetRegisters.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);

        project.Status = ProjectStatus.Completed;
        project.CapitalizedAt = inService;
        project.CapitalizedAssetId = asset.Id;
        project.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = asset.Id, projectId = project.Id };
    }

    public async Task<object> SaveSolarLogAsync(SaveSolarLogViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.ProjectId is not > 0)
            throw new InvalidOperationException("Select a solar project.");

        SolarProductionLog row;
        if (model.Id is > 0)
            row = await _context.SolarProductionLogs.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new SolarProductionLog(); _context.SolarProductionLogs.Add(row); }
        row.ProjectId = model.ProjectId;
        row.Date = model.Date == default ? DateOnly.FromDateTime(DateTime.Today) : model.Date;
        row.KwhGenerated = Math.Max(0, model.KwhGenerated);
        row.SavingsEstimate = model.SavingsEstimate;
        row.MaintenanceAmount = Math.Max(0, model.MaintenanceAmount);
        row.InstalledCapacityKw = model.InstalledCapacityKw;
        row.Notes = TrimOrNull(model.Notes);
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    private async Task PostCapexMoneyAsync(ProjectExpenditure row, string projectName, CancellationToken cancellationToken)
    {
        if (row.PaidAmount > 0 && row.AccountId is > 0)
        {
            await EnsureAccountCanPayAsync(row.AccountId.Value, row.PaidAmount, cancellationToken);
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Date = row.Date,
                Kind = LedgerTransactionKind.Expense,
                AccountId = row.AccountId,
                Amount = row.PaidAmount,
                Category = "Capital expenditure",
                Reference = "capex:" + row.Id,
                Notes = $"CAPEX · {projectName} · {row.CostType ?? row.Description}"
            });
        }
        if (row.PaidAmount < row.Amount)
        {
            _context.SupplierPayables.Add(new SupplierPayable
            {
                SupplierName = string.IsNullOrWhiteSpace(row.Vendor) ? "CAPEX supplier" : row.Vendor,
                Date = row.Date,
                DueDate = row.DueDate,
                Description = $"CAPEX · {projectName} · {row.CostType ?? row.Description}",
                Amount = row.Amount,
                PaidAmount = row.PaidAmount,
                Status = row.PaidAmount > 0 ? PayableStatus.Partial : PayableStatus.Open,
                SourceType = "capital_expenditure",
                SourceId = row.Id.ToString()
            });
        }
        await Task.CompletedTask;
    }

    private async Task PostAssetMoneyAsync(FixedAssetRegister row, CancellationToken cancellationToken)
    {
        if (row.PaidAmount <= 0 || row.AccountId is not > 0) return;
        await EnsureAccountCanPayAsync(row.AccountId.Value, row.PaidAmount, cancellationToken);
        _context.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = row.PurchaseDate ?? DateOnly.FromDateTime(DateTime.Today),
            Kind = LedgerTransactionKind.Expense,
            AccountId = row.AccountId,
            Amount = row.PaidAmount,
            Category = "Fixed asset",
            Reference = "fixed_asset:" + row.Id,
            Notes = $"Asset · {row.Name}"
        });
        if (row.PaidAmount < row.Cost)
        {
            _context.SupplierPayables.Add(new SupplierPayable
            {
                SupplierName = string.IsNullOrWhiteSpace(row.Supplier) ? "Asset supplier" : row.Supplier,
                Date = row.PurchaseDate ?? DateOnly.FromDateTime(DateTime.Today),
                DueDate = null,
                Description = $"Asset · {row.Name}",
                Amount = row.Cost,
                PaidAmount = row.PaidAmount,
                Status = row.PaidAmount > 0 ? PayableStatus.Partial : PayableStatus.Open,
                SourceType = "fixed_asset",
                SourceId = row.Id.ToString()
            });
        }
    }

    private async Task ReverseMoneyAsync(string source, int id, CancellationToken cancellationToken)
    {
        var key = source + ":" + id;
        var txs = await _context.LedgerTransactions
            .Where(t => t.Reference == key || (t.Reference != null && t.Reference.StartsWith(key + ":")))
            .ToListAsync(cancellationToken);
        _context.LedgerTransactions.RemoveRange(txs);
        var sid = id.ToString();
        var sourceType = source == "capex" ? "capital_expenditure" : source;
        var pays = await _context.SupplierPayables.Where(p => p.SourceType == sourceType && p.SourceId == sid).ToListAsync(cancellationToken);
        _context.SupplierPayables.RemoveRange(pays);
    }

    private async Task EnsureAccountCanPayAsync(int accountId, decimal amount, CancellationToken cancellationToken)
    {
        var account = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Selected Cash / Bank account was not found. Add it under Accounting → Bank & Cash.");
        var balance = await AccountBalanceAsync(account.Id, cancellationToken);
        if (amount > balance)
            throw new InvalidOperationException($"Insufficient balance in {account.Name}. Available: {balance:N0}.");
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

    private static string NormalizePaymentStatus(string? status) => (status ?? "Paid").Trim() switch
    {
        "Credit" => "Credit",
        "Part Paid" or "Part paid" or "Partial" => "Part Paid",
        _ => "Paid"
    };

    private static decimal ClampPaid(decimal amount, decimal paid, string status)
    {
        if (status == "Paid") return amount;
        if (status == "Credit") return 0;
        return Math.Max(0, Math.Min(amount, paid));
    }

    private static string? TrimOrNull(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }
}
