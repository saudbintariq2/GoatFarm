using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class BackupService : Application.Interfaces.IBackupService
{
    private readonly GoatFarmDbContext _context;

    public BackupService(GoatFarmDbContext context) => _context = context;

    public static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<object> ExportAsync(CancellationToken cancellationToken = default)
    {
        var remindDaysSetting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "RemindDays", cancellationToken);
        var userEmails = await _context.Users.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id, cancellationToken);

        return new
        {
            formatVersion = BackupFormat.CurrentVersion,
            app = BackupFormat.AppMarker,
            goats = await _context.Goats.AsNoTracking().OrderBy(g => g.Id).Select(g => new
            {
                id = g.Id, tag = g.Tag, name = g.Name, comment = g.Comment, breed = g.Breed,
                gender = g.Gender.ToString(), status = g.Status.ToString(), source = g.Source.ToString(),
                purchasePrice = g.PurchasePrice, eventDate = g.EventDate,
                dateOfBirth = g.DateOfBirth, dateOfBirthIsEstimated = g.DateOfBirthIsEstimated,
                group = g.Group == null ? null : g.Group.Name,
                prepCrossDate = g.PrepCrossDate, matedDate = g.MatedDate, buckTag = g.BuckTag,
                kidsCount = g.KidsCount, ultrasoundDate = g.UltrasoundDate,
                isArchived = g.IsArchived, archivedReason = g.ArchivedReason, archivedDate = g.ArchivedDate,
                owner = g.Owner, healthState = g.HealthState.ToString(), breedingStage = g.BreedingStage.ToString(),
                sireId = g.SireId, damId = g.DamId, sireTagText = g.SireTagText, damTagText = g.DamTagText,
                isTemporaryTag = g.IsTemporaryTag, kiddingRecordId = g.KiddingRecordId
            }).ToListAsync(cancellationToken),
            breedingEvents = await _context.BreedingEvents.AsNoTracking().OrderBy(e => e.Id).Select(e => new
            {
                id = e.Id, doeId = e.DoeId, buckId = e.BuckId, buckTagText = e.BuckTagText, matedDate = e.MatedDate,
                scanDate = e.ScanDate, scanKids = e.ScanKids, outcome = e.Outcome.ToString(),
                outcomeDate = e.OutcomeDate, notes = e.Notes
            }).ToListAsync(cancellationToken),
            kiddingRecords = await _context.KiddingRecords.AsNoTracking().OrderBy(k => k.Id).Select(k => new
            {
                id = k.Id, doeId = k.DoeId, breedingEventId = k.BreedingEventId, kiddingDate = k.KiddingDate,
                liveKids = k.LiveKids, stillborn = k.Stillborn, ease = k.Ease.ToString(), notes = k.Notes,
                clientRequestId = k.ClientRequestId
            }).ToListAsync(cancellationToken),
            farmTasks = (await _context.FarmTasks.AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => new
            {
                id = t.Id, title = t.Title, notes = t.Notes, type = t.Type.ToString(), priority = t.Priority.ToString(),
                status = t.Status.ToString(), dueDate = t.DueDate,
                assigneeEmail = t.AssigneeUserId is null ? null : userEmails.GetValueOrDefault(t.AssigneeUserId),
                createdByEmail = t.CreatedByUserId is null ? null : userEmails.GetValueOrDefault(t.CreatedByUserId),
                goatId = t.GoatId, startedAtUtc = t.StartedAtUtc, completedAtUtc = t.CompletedAtUtc
            }).ToList(),
            security = await ExportSecurityAsync(cancellationToken),
            groups = await _context.GoatGroups.AsNoTracking().Select(g => g.Name).ToListAsync(cancellationToken),
            feedPrices = await _context.FeedPrices.AsNoTracking().ToListAsync(cancellationToken),
            prices = await _context.FeedPrices.AsNoTracking().ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken),
            feedStock = await _context.FeedPrices.AsNoTracking().ToDictionaryAsync(p => p.FeedType, p => p.StockKg, cancellationToken),
            feedPlans = await _context.FeedPlans.AsNoTracking().OrderBy(p => p.Id).Select(p => new
            {
                id = p.Id, statusKey = p.StatusKey.ToString(), mixKgPerDay = p.MixKgPerDay, fodderKgPerDay = p.FodderKgPerDay,
                fodderDryKgPerDay = p.FodderDryKgPerDay, medicineCostPerGoatPerMonth = p.MedicineCostPerGoatPerMonth,
                items = p.Items.OrderBy(i => i.Id).Select(i => new { feedType = i.FeedType, gramsPerDay = i.GramsPerDay }).ToList()
            }).ToListAsync(cancellationToken),
            feedBuys = await _context.FeedPurchases.AsNoTracking().ToListAsync(cancellationToken),
            recurringCosts = await _context.RecurringCosts.AsNoTracking().ToListAsync(cancellationToken),
            vaccineBuys = await _context.VaccinePurchases.AsNoTracking().ToListAsync(cancellationToken),
            assets = await _context.Assets.AsNoTracking().ToListAsync(cancellationToken),
            incomes = await _context.Incomes.AsNoTracking().ToListAsync(cancellationToken),
            expenses = await _context.Expenses.AsNoTracking().ToListAsync(cancellationToken),
            ownerInv = await _context.OwnerInvestments.AsNoTracking().ToListAsync(cancellationToken),
            vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken),
            vaccLog = await _context.VaccinationHistories.AsNoTracking()
                .Select(v => new { id = v.Id, goatId = v.GoatId, vaccineId = v.VaccineId, date = v.VaccinationDate })
                .ToListAsync(cancellationToken),
            reminders = await _context.Reminders.AsNoTracking().ToListAsync(cancellationToken),
            remindDays = remindDaysSetting is not null && int.TryParse(remindDaysSetting.Value, out var days) ? days : 30,
            milkProd = await _context.MilkProductions.AsNoTracking().ToListAsync(cancellationToken),
            milkSales = await _context.MilkSales.AsNoTracking().ToListAsync(cancellationToken),
            milkWastes = await _context.MilkWastes.AsNoTracking().ToListAsync(cancellationToken),
            employees = await _context.Employees.AsNoTracking().ToListAsync(cancellationToken),
            breedingEmptyLogs = await _context.BreedingEmptyLogs.AsNoTracking()
                .Select(l => new { id = l.Id, goatId = l.GoatId, scanDate = l.ScanDate, matedDate = l.MatedDate, buckTag = l.BuckTag })
                .ToListAsync(cancellationToken),
            mixRecipe = await GetMixRecipeExportAsync(cancellationToken),
            mixRecipes = await GetAppSettingJsonAsync(Domain.Constants.AppSettingKeys.MixRecipes, cancellationToken),
            fodderPool = await GetAppSettingJsonAsync(Domain.Constants.AppSettingKeys.FodderPool, cancellationToken),
            feedSettings = await GetAppSettingJsonAsync(Domain.Constants.AppSettingKeys.FeedSettings, cancellationToken),
            feedFull = await _context.FeedPrices.AsNoTracking()
                .Where(p => p.StockKgFull > 0)
                .ToDictionaryAsync(p => p.FeedType, p => p.StockKgFull, cancellationToken),
            feedUsage = await _context.FeedUsageRecords.AsNoTracking().ToListAsync(cancellationToken),
            deaths = await _context.DeathRecords.AsNoTracking().ToListAsync(cancellationToken),
            goatWeights = await _context.GoatWeightRecords.AsNoTracking()
                .Select(w => new { id = w.Id, goatId = w.GoatId, date = w.Date, kg = w.Kg })
                .ToListAsync(cancellationToken),
            lookupSettings = await _context.AppSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("Lookup."))
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken),
            v241Extended = await BackupV241Helper.ExportAsync(_context, cancellationToken),
            _savedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Who had which access when the backup was taken. Never includes password hashes, security stamps or tokens.
    /// Restore does not apply this section; accounts and permissions stay as they are in the target database.
    /// </summary>
    private async Task<object> ExportSecurityAsync(CancellationToken cancellationToken)
    {
        var roleNames = await (from ur in _context.UserRoles
                               join r in _context.Roles on ur.RoleId equals r.Id
                               select new { ur.UserId, r.Name }).AsNoTracking().ToListAsync(cancellationToken);
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, u.FullName, u.UsesRolePermissions, u.PermissionsJson })
            .ToListAsync(cancellationToken);
        return new
        {
            restoredOnImport = false,
            users = users.Select(u => new
            {
                email = u.Email,
                fullName = u.FullName,
                roles = roleNames.Where(r => r.UserId == u.Id).Select(r => r.Name).Order().ToList(),
                usesRolePermissions = u.UsesRolePermissions,
                permissions = ParseJsonOrNull(u.PermissionsJson)
            }).ToList(),
            rolePermissions = await GetAppSettingJsonAsync("RolePermissions", cancellationToken)
        };
    }

    private static object? ParseJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch (JsonException) { return null; }
    }

    private async Task<object?> GetMixRecipeExportAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == Domain.Constants.AppSettingKeys.MixRecipe, cancellationToken);
        return setting?.Value;
    }

    private async Task<object?> GetAppSettingJsonAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting?.Value is null) return null;
        try { return JsonSerializer.Deserialize<object>(setting.Value); }
        catch { return setting.Value; }
    }

    /// <summary>
    /// Replaces all farm data with the backup contents. The file is validated first; the wipe and the import run in
    /// one database transaction, so a failure leaves the existing farm untouched.
    /// </summary>
    public async Task ImportAsync(string json, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var errors = BackupValidator.Validate(root);
        if (errors.Count > 0)
            throw new BackupValidationException(errors);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await BackupImportHelper.ClearFarmDataAsync(_context, ct);
            if (root.TryGetProperty("farmTasks", out _))
            {
                _context.FarmTasks.RemoveRange(await _context.FarmTasks.ToListAsync(ct));
                await _context.SaveChangesAsync(ct);
            }
            await BackupImportHelper.ImportAsync(_context, root, ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
        _context.ChangeTracker.Clear();
    }
}
