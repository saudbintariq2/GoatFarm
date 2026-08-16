using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class DbDataCleaner
{
    public static async Task ClearAllFarmDataAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        context.VaccinationHistories.RemoveRange(await context.VaccinationHistories.ToListAsync(cancellationToken));
        context.Goats.RemoveRange(await context.Goats.ToListAsync(cancellationToken));
        context.MilkProductions.RemoveRange(await context.MilkProductions.ToListAsync(cancellationToken));
        context.MilkSales.RemoveRange(await context.MilkSales.ToListAsync(cancellationToken));
        context.MilkWastes.RemoveRange(await context.MilkWastes.ToListAsync(cancellationToken));
        context.FeedPurchases.RemoveRange(await context.FeedPurchases.ToListAsync(cancellationToken));
        context.VaccinePurchases.RemoveRange(await context.VaccinePurchases.ToListAsync(cancellationToken));
        context.Incomes.RemoveRange(await context.Incomes.ToListAsync(cancellationToken));
        context.Expenses.RemoveRange(await context.Expenses.ToListAsync(cancellationToken));
        context.OwnerInvestments.RemoveRange(await context.OwnerInvestments.ToListAsync(cancellationToken));
        context.Assets.RemoveRange(await context.Assets.ToListAsync(cancellationToken));
        context.RecurringCosts.RemoveRange(await context.RecurringCosts.ToListAsync(cancellationToken));
        context.Reminders.RemoveRange(await context.Reminders.ToListAsync(cancellationToken));
        context.Vaccines.RemoveRange(await context.Vaccines.ToListAsync(cancellationToken));
        context.GoatGroups.RemoveRange(await context.GoatGroups.ToListAsync(cancellationToken));
        context.FeedPlans.RemoveRange(await context.FeedPlans.ToListAsync(cancellationToken));
        context.FeedPrices.RemoveRange(await context.FeedPrices.ToListAsync(cancellationToken));

        await context.SaveChangesAsync(cancellationToken);
    }
}
