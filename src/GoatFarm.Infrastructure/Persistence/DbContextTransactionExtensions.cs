using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class DbContextTransactionExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> as one atomic unit inside the provider's execution strategy.
    /// Npgsql's retrying strategy rejects a bare BeginTransaction, so every multi-step save must go through here.
    /// On retry the change tracker is cleared so the work starts from a clean state.
    /// </summary>
    public static async Task<T> ExecuteInTransactionAsync<T>(this DbContext context, Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational() || context.Database.CurrentTransaction is not null)
            return await work();

        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0) context.ChangeTracker.Clear();
            await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await work();
            await tx.CommitAsync(cancellationToken);
            return result;
        });
    }

    public static Task ExecuteInTransactionAsync(this DbContext context, Func<Task> work,
        CancellationToken cancellationToken = default) =>
        context.ExecuteInTransactionAsync(async () => { await work(); return true; }, cancellationToken);
}
