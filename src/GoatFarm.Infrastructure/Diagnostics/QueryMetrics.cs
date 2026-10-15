using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoatFarm.Infrastructure.Diagnostics;

/// <summary>Database commands and time spent in them for one measured operation (usually one HTTP request).</summary>
public sealed class QueryMetrics
{
    private long _commands;
    private long _dbTicks;

    public QueryMetrics(bool captureCommandText = false) =>
        CommandTexts = captureCommandText ? new ConcurrentDictionary<string, int>(StringComparer.Ordinal) : null;

    public int Commands => (int)Interlocked.Read(ref _commands);
    public TimeSpan DatabaseTime => TimeSpan.FromTicks(Interlocked.Read(ref _dbTicks));

    /// <summary>Command text → number of executions; only collected when requested.</summary>
    public ConcurrentDictionary<string, int>? CommandTexts { get; }

    internal void Record(DbCommand command, TimeSpan duration)
    {
        Interlocked.Increment(ref _commands);
        Interlocked.Add(ref _dbTicks, duration.Ticks);
        CommandTexts?.AddOrUpdate(command.CommandText, 1, (_, n) => n + 1);
    }

    private static readonly AsyncLocal<QueryMetrics?> CurrentScope = new();

    public static QueryMetrics? Current => CurrentScope.Value;

    /// <summary>Starts measuring for the current async flow; dispose to stop.</summary>
    public static IDisposable Begin(QueryMetrics metrics)
    {
        var previous = CurrentScope.Value;
        CurrentScope.Value = metrics;
        return new Restore(previous);
    }

    private sealed class Restore(QueryMetrics? previous) : IDisposable
    {
        public void Dispose() => CurrentScope.Value = previous;
    }
}

/// <summary>Adds every executed command to <see cref="QueryMetrics.Current"/>; does nothing when no measurement is active.</summary>
public sealed class QueryMetricsInterceptor : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        QueryMetrics.Current?.Record(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }
}
