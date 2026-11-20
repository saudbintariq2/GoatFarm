using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public partial class FinanceService
{
    public const string PayrollReferencePrefix = "payroll:";

    public async Task<PayrollMonthViewModel> BuildPayrollMonthAsync(
        string month,
        IReadOnlyList<EmployeeViewModel> employees,
        CancellationToken cancellationToken = default)
    {
        month = string.IsNullOrWhiteSpace(month) ? MonthHelper.CurrentMonthKey() : month.Trim();
        var payrollTx = await _context.LedgerTransactions.AsNoTracking()
            .Where(t => t.Reference != null && t.Reference.StartsWith(PayrollReferencePrefix))
            .Select(t => new { t.Reference, t.Amount })
            .ToListAsync(cancellationToken);

        decimal SumFor(int employeeId, string kind) => payrollTx
            .Where(t => TryParsePayrollReference(t.Reference!, out var empId, out var monthKey, out var txKind)
                        && empId == employeeId && monthKey == month && txKind == kind)
            .Sum(t => t.Amount);

        var rows = new List<PayrollEmployeeRowViewModel>();
        foreach (var e in employees)
        {
            var due = e.MonthlySalary;
            var advance = SumFor(e.Id, "advance");
            var paid = SumFor(e.Id, "salary");
            var balance = Math.Max(0, due - advance - paid);
            rows.Add(new PayrollEmployeeRowViewModel
            {
                EmployeeId = e.Id,
                Name = e.Name,
                Role = e.Role,
                Due = due,
                Advance = advance,
                Paid = paid,
                Balance = balance
            });
        }

        return new PayrollMonthViewModel
        {
            Month = month,
            TotalDue = rows.Sum(r => r.Due),
            TotalAdvance = rows.Sum(r => r.Advance),
            TotalPaid = rows.Sum(r => r.Paid),
            TotalOutstanding = rows.Sum(r => r.Balance),
            Rows = rows
        };
    }

    public async Task<PayrollPaymentResultViewModel> RecordPayrollPaymentAsync(
        RecordPayrollPaymentViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Amount <= 0)
            return new PayrollPaymentResultViewModel { Success = false, Message = "Enter a positive amount." };

        var kind = (model.Kind ?? "").Trim().ToLowerInvariant();
        if (kind is not ("salary" or "advance"))
            return new PayrollPaymentResultViewModel { Success = false, Message = "Payment kind must be salary or advance." };

        var month = string.IsNullOrWhiteSpace(model.Month) ? MonthHelper.CurrentMonthKey() : model.Month.Trim();
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == model.EmployeeId, cancellationToken);
        if (employee is null)
            return new PayrollPaymentResultViewModel { Success = false, Message = "Employee not found." };

        var account = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == model.AccountId && a.IsActive, cancellationToken);
        if (account is null)
            return new PayrollPaymentResultViewModel { Success = false, Message = "Choose an active Cash/Bank account." };

        var tx = new LedgerTransaction
        {
            Date = model.Date,
            Kind = LedgerTransactionKind.Expense,
            AccountId = model.AccountId,
            Amount = model.Amount,
            Category = kind == "advance" ? "Salary advance" : "Staff salary",
            Notes = string.IsNullOrWhiteSpace(model.Note)
                ? $"{employee.Name} · {month}"
                : $"{employee.Name} · {month} · {model.Note.Trim()}",
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.LedgerTransactions.Add(tx);
        await _context.SaveChangesAsync(cancellationToken);

        tx.Reference = $"{PayrollReferencePrefix}{kind}:{employee.Id}:{month}:{tx.Id}";
        await _context.SaveChangesAsync(cancellationToken);

        return new PayrollPaymentResultViewModel
        {
            Success = true,
            Message = kind == "advance"
                ? $"Advance recorded for {employee.Name}."
                : $"Salary paid to {employee.Name}."
        };
    }

    private static bool TryParsePayrollReference(string reference, out int employeeId, out string monthKey, out string kind)
    {
        employeeId = 0;
        monthKey = "";
        kind = "";
        if (!reference.StartsWith(PayrollReferencePrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var parts = reference[PayrollReferencePrefix.Length..].Split(':');
        if (parts.Length < 4)
            return false;
        kind = parts[0];
        if (!int.TryParse(parts[1], out employeeId))
            return false;
        monthKey = parts[2];
        return true;
    }
}