using GoatFarm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Helpers;

public static class DuplicateMoneyGate
{
    public static async Task<IActionResult?> ConflictIfDuplicateAsync(
        DuplicateMoneyCheck check,
        bool confirmPossibleDuplicate,
        DateOnly date,
        decimal amount,
        string? party,
        string? ignoreTab,
        int? ignoreId,
        CancellationToken cancellationToken)
    {
        if (confirmPossibleDuplicate || amount <= 0)
            return null;
        var dup = await check.FindDuplicateAsync(date, amount, party, ignoreTab, ignoreId, cancellationToken);
        if (dup is null)
            return null;
        return new ConflictObjectResult(new
        {
            error = DuplicateMoneyCheck.ConflictMessage(dup),
            severity = "warn",
            requiresConfirmation = true
        });
    }
}