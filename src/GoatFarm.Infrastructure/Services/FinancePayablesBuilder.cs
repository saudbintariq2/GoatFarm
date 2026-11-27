using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

/// <summary>One payables list (supplier bills incl. feed credit, salary due) used by the card, balance sheet, closing and dashboard.</summary>
internal static class FinancePayablesBuilder
{
    private const string PayrollPrefix = "payroll:";

    public static async Task<IReadOnlyList<(int EmployeeId, string Name, decimal Balance)>> PayrollOutstandingAsync(
        GoatFarmDbContext db, string month, CancellationToken cancellationToken = default)
    {
        month = string.IsNullOrWhiteSpace(month) ? MonthHelper.CurrentMonthKey() : month.Trim();
        var employees = await db.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync(cancellationToken);
        var payrollTx = await db.LedgerTransactions.AsNoTracking()
            .Where(t => t.Reference != null && t.Reference.StartsWith(PayrollPrefix))
            .Select(t => new { t.Reference, t.Amount })
            .ToListAsync(cancellationToken);

        decimal SumFor(int employeeId, string kind) => payrollTx
            .Where(t => TryParsePayrollReference(t.Reference!, out var empId, out var monthKey, out var txKind)
                        && empId == employeeId && monthKey == month && txKind == kind)
            .Sum(t => t.Amount);

        var monthStart = DateOnly.Parse(month + "-01");
        var rows = new List<(int, string, decimal)>();
        foreach (var e in employees)
        {
            var due = StaffSalaryAccrual.ForMonth([e], monthStart);
            if (due <= 0) continue;
            var balance = Math.Max(0, due - SumFor(e.Id, "advance") - SumFor(e.Id, "salary"));
            if (balance > 0.01m)
                rows.Add((e.Id, e.Name, balance));
        }

        return rows;
    }

    private static bool TryParsePayrollReference(string reference, out int employeeId, out string monthKey, out string kind)
    {
        employeeId = 0;
        monthKey = "";
        kind = "";
        if (!reference.StartsWith(PayrollPrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var parts = reference[PayrollPrefix.Length..].Split(':');
        if (parts.Length < 4)
            return false;
        kind = parts[0];
        if (!int.TryParse(parts[1], out employeeId))
            return false;
        monthKey = parts[2];
        return true;
    }

    public static async Task<(IReadOnlyList<SupplierPayableRowViewModel> Rows, decimal TotalOpen, decimal LoansOpen)> BuildAsync(
        GoatFarmDbContext db,
        IReadOnlyList<(int EmployeeId, string Name, decimal Balance)> payrollOutstanding,
        CancellationToken cancellationToken = default)
    {
        // Credit feed purchases already own a SupplierPayable (SourceType "feed_purchase"), so bills are the only source.
        var billRows = await db.SupplierPayables.AsNoTracking()
            .Where(p => p.Status != PayableStatus.Void && p.Amount > p.PaidAmount + 0.01m)
            .OrderByDescending(p => p.Date)
            .ToListAsync(cancellationToken);

        var rows = new List<SupplierPayableRowViewModel>();

        foreach (var p in billRows)
        {
            rows.Add(new SupplierPayableRowViewModel
            {
                Id = p.Id,
                SupplierName = p.SupplierName,
                DateDisplay = p.Date.ToString("yyyy-MM-dd"),
                Description = p.Description,
                Amount = p.Amount,
                PaidAmount = p.PaidAmount,
                Status = p.Status.ToString(),
                DueDateDisplay = p.DueDate?.ToString("yyyy-MM-dd"),
                CanPayFromUi = true
            });
        }

        var month = MonthHelper.CurrentMonthKey();
        foreach (var s in payrollOutstanding.Where(x => x.Balance > 0.01m))
        {
            rows.Add(new SupplierPayableRowViewModel
            {
                Id = 0,
                SupplierName = s.Name,
                DateDisplay = month + "-01",
                Description = $"Salary {month}",
                Amount = s.Balance,
                PaidAmount = 0,
                Status = "Open",
                DueDateDisplay = null,
                CanPayFromUi = false
            });
        }

        var ordered = rows.OrderBy(r => r.DueDateDisplay ?? "9999-99-99").ThenByDescending(r => r.DateDisplay).ToList();
        decimal LoansOpen(IEnumerable<SupplierPayableRowViewModel> r) => r
            .Where(p => p.Description.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Max(0, p.Amount - p.PaidAmount));
        var loans = LoansOpen(ordered);
        var total = ordered
            .Where(p => !p.Description.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Max(0, p.Amount - p.PaidAmount));
        return (ordered, total, loans);
    }
}