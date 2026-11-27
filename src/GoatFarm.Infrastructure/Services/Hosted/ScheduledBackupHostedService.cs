using System.Globalization;
using GoatFarm.Application.Interfaces;
using GoatFarm.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoatFarm.Infrastructure.Services.Hosted;

/// <summary>
/// Daily backup at <see cref="BackupOptions.DailyAt"/> farm time (02:00 PKT by default). Each run uses a new scope, so it
/// reads the live database through a fresh DbContext. After a restart it catches up if the last good backup is over a day old.
/// </summary>
public sealed class ScheduledBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledBackupHostedService> _logger;
    private readonly BackupOptions _options;

    public ScheduledBackupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledBackupHostedService> logger,
        IOptions<BackupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutomaticEnabled)
        {
            _logger.LogWarning("Automatic backups are disabled (Backup:AutomaticEnabled = false).");
            return;
        }

        var zone = BackupService.ResolveTimeZone(_options.TimeZone);
        var at = TimeSpan.TryParseExact(_options.DailyAt, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : TimeSpan.FromHours(2);

        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        if (await IsOverdueAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextRunUtc(DateTime.UtcNow, zone, at) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);
            await RunOnceAsync(stoppingToken);
        }
    }

    public static DateTime NextRunUtc(DateTime nowUtc, TimeZoneInfo zone, TimeSpan at)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var nextLocal = nowLocal.Date.Add(at);
        if (nextLocal <= nowLocal) nextLocal = nextLocal.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), zone);
    }

    private async Task<bool> IsOverdueAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var status = await scope.ServiceProvider.GetRequiredService<IBackupService>().GetStatusAsync(stoppingToken);
            return status.LastSuccessUtc is not { } last || DateTime.UtcNow - last > TimeSpan.FromHours(24);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not read backup status; running a backup now.");
            return true;
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IBackupService>().RunScheduledBackupAsync(stoppingToken);
            if (result.Success)
                _logger.LogInformation("{Message}", result.Message);
            else
                _logger.LogError("Daily backup FAILED: {Message}", result.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Daily backup FAILED");
        }
    }
}
