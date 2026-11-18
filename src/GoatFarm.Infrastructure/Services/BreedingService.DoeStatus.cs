using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public partial class BreedingService
{
    public async Task<DoeStatusContextViewModel?> GetDoeStatusContextAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goatId, cancellationToken);
        if (goat is null || goat.Gender != GoatGender.Female) return null;

        var flow = BreedingDoeFlowHelper.FlowStage(goat, Today) ?? "getting_ready";
        var season = await LoadSeasonSettingsAsync(cancellationToken);
        var batches = season.Batches.Select(b => new BreedingBirthBatchOptionViewModel
        {
            Id = b.Id,
            Label = $"{b.Icon} {b.Name}"
        }).ToList();
        var batch = season.Batches.FirstOrDefault(b =>
            string.Equals(b.Name, goat.BirthBatchName, StringComparison.OrdinalIgnoreCase));

        var bucks = await _context.Goats.AsNoTracking()
            .Where(g => g.Gender == GoatGender.Male && !g.IsArchived && g.Status == GoatStatus.Buck)
            .OrderBy(g => g.Tag)
            .Select(g => g.Tag)
            .ToListAsync(cancellationToken);

        var open = await BreedingCycleWriter.OpenEventsAsync(_context, goat.Id, cancellationToken);
        var kiddedFromOpen = goat.MatedDate.HasValue && await _context.KiddingRecords.AsNoTracking()
            .AnyAsync(k => k.DoeId == goat.Id && k.KiddingDate >= goat.MatedDate.Value, cancellationToken);

        return new DoeStatusContextViewModel
        {
            GoatId = goat.Id,
            Tag = goat.Tag,
            FlowStageKey = flow,
            FlowStageLabel = BreedingDoeFlowHelper.FlowStageLabel(flow),
            Breed = goat.Breed,
            BirthBatchId = batch?.Id,
            BirthBatchName = goat.BirthBatchName,
            MatedDate = goat.MatedDate?.ToString("yyyy-MM-dd"),
            BuckTag = goat.BuckTag,
            KidsCount = goat.KidsCount,
            CanUndoMistake = open.Count > 0 && !kiddedFromOpen,
            BirthBatches = batches,
            BuckTags = bucks
        };
    }

    public async Task<ChangeDoeStatusResultViewModel> ChangeDoeStatusAsync(
        ChangeDoeStatusViewModel model,
        CancellationToken cancellationToken = default)
    {
        var goat = await _context.Goats.FindAsync([model.GoatId], cancellationToken)
            ?? throw new InvalidOperationException("Doe not found.");
        if (goat.Gender != GoatGender.Female)
            throw new InvalidOperationException($"{goat.Tag} is not a doe.");

        var flow = BreedingDoeFlowHelper.FlowStage(goat, Today) ?? "getting_ready";
        var target = model.TargetStatus.Trim().ToLowerInvariant();
        var date = model.Date ?? Today;

        if (BreedingDoeStatusCatalog.IsOddMove(flow, target) && !model.ConfirmWarnings)
            throw new InvalidOperationException("Confirm this unusual status change.");

        if (target == "kidded")
        {
            return new ChangeDoeStatusResultViewModel
            {
                Message = $"{goat.Tag} → open kidding form",
                OpenKiddingForm = true,
                GoatId = goat.Id,
                Tag = goat.Tag
            };
        }

        var label = BreedingDoeStatusCatalog.LabelFor(target);
        switch (target)
        {
            case "getting_ready":
                await RemoveDoeFromAllBreedingGroupsAsync(goat.Id, cancellationToken);
                goat.PrepCrossDate = date;
                goat.NextCrossDate = null;
                goat.BreedingStage = BreedingStage.Ready;
                if (!string.IsNullOrWhiteSpace(model.BirthBatchId))
                {
                    var season = await LoadSeasonSettingsAsync(cancellationToken);
                    var batch = season.Batches.FirstOrDefault(b => b.Id == model.BirthBatchId);
                    if (batch is not null) goat.BirthBatchName = batch.Name;
                }
                goat.Comment = AppendLine(goat.Comment, $"Getting ready · {date:yyyy-MM-dd}");
                break;

            case "in_heat":
                if (model.CrossNotTaken && goat.MatedDate.HasValue && goat.KidsCount is null or 0)
                {
                    await RecordReturnedToHeatAsync(new RecordReturnedToHeatViewModel { Tag = goat.Tag, Date = date }, cancellationToken);
                    goat = await _context.Goats.FindAsync([goat.Id], cancellationToken) ?? goat;
                }
                else
                {
                    _context.GoatHeatEvents.Add(new GoatHeatEvent { GoatId = goat.Id, Date = date, Note = "heat seen" });
                    goat.Comment = AppendLine(goat.Comment, $"In heat {date:yyyy-MM-dd}");
                }
                if (model.CrossNowFromHeat && !string.IsNullOrWhiteSpace(model.BuckTag))
                    await CrossAsync(goat, date, model.BuckTag, model.ConfirmWarnings, cancellationToken);
                break;

            case "crossed":
                await CrossAsync(goat, date, model.BuckTag, model.ConfirmWarnings, cancellationToken);
                break;

            case "waiting_scan":
                goat.Comment = AppendLine(goat.Comment, $"Buck out {date:yyyy-MM-dd} · waiting scan");
                if (goat.BreedingStage == BreedingStage.None && goat.MatedDate.HasValue)
                    goat.BreedingStage = BreedingStage.ScanPending;
                break;

            case "pregnant":
                if (!model.KidsCount.HasValue || model.KidsCount < 1)
                    throw new InvalidOperationException("Pick kids seen on scan (1–4+).");
                await RecordUltrasoundAsync(new RecordUltrasoundViewModel
                {
                    Tag = goat.Tag,
                    KidsCount = model.KidsCount.Value,
                    Date = date,
                    DaysPregnant = model.DaysPregnant
                }, cancellationToken);
                return new ChangeDoeStatusResultViewModel
                {
                    Message = $"{goat.Tag} → {label}",
                    GoatId = goat.Id,
                    Tag = goat.Tag
                };

            case "empty":
                if (string.IsNullOrWhiteSpace(model.EmptyNextStep))
                    throw new InvalidOperationException("Choose what happens next after an empty scan.");
                await RecordUltrasoundAsync(new RecordUltrasoundViewModel
                {
                    Tag = goat.Tag,
                    KidsCount = 0,
                    Date = date,
                    EmptyNextStep = model.EmptyNextStep,
                    EmptyBirthBatchId = model.BirthBatchId
                }, cancellationToken);
                return new ChangeDoeStatusResultViewModel
                {
                    Message = $"{goat.Tag} → Empty",
                    GoatId = goat.Id,
                    Tag = goat.Tag
                };

            case "dry":
                if (goat.Status == GoatStatus.Milking)
                    goat.Status = GoatStatus.Dry;
                goat.Comment = AppendLine(goat.Comment, $"Dried off {date:yyyy-MM-dd}");
                break;

            case "lost_kids":
                await RecordAbortionAsync(new RecordAbortionViewModel
                {
                    Tag = goat.Tag,
                    Date = date,
                    Notes = "Lost kids"
                }, cancellationToken);
                goat = await _context.Goats.FindAsync([goat.Id], cancellationToken) ?? goat;
                await RemoveDoeFromAllBreedingGroupsAsync(goat.Id, cancellationToken);
                goat.PrepCrossDate = date;
                goat.BreedingStage = BreedingStage.Ready;
                if (!string.IsNullOrWhiteSpace(model.BirthBatchId))
                {
                    var season = await LoadSeasonSettingsAsync(cancellationToken);
                    var batch = season.Batches.FirstOrDefault(b => b.Id == model.BirthBatchId);
                    if (batch is not null) goat.BirthBatchName = batch.Name;
                }
                break;

            case "cull_list":
                await RemoveDoeFromAllBreedingGroupsAsync(goat.Id, cancellationToken);
                var reason = string.IsNullOrWhiteSpace(model.CullReason) ? "Other" : model.CullReason.Trim();
                goat.Comment = AppendLine(goat.Comment, $"Cull list — {reason}");
                _context.FarmTasks.Add(new FarmTask
                {
                    Title = $"Cull review — {goat.Tag}",
                    Notes = reason,
                    Type = FarmTaskType.Reminder,
                    Priority = FarmTaskPriority.High,
                    Status = FarmTaskStatus.ToDo,
                    DueDate = date,
                    GoatId = goat.Id
                });
                break;

            case "mistake":
                var kidded = goat.MatedDate.HasValue && await _context.KiddingRecords.AsNoTracking()
                    .AnyAsync(k => k.DoeId == goat.Id && k.KiddingDate >= goat.MatedDate.Value, cancellationToken);
                if (kidded)
                    throw new InvalidOperationException("Cannot undo — she already kidded from this cross.");
                await ClearBreedingMistakeAsync(goat.Id, cancellationToken);
                return new ChangeDoeStatusResultViewModel
                {
                    Message = $"{goat.Tag} → cross removed",
                    GoatId = goat.Id,
                    Tag = goat.Tag
                };

            default:
                throw new InvalidOperationException("Unknown status.");
        }

        goat.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new ChangeDoeStatusResultViewModel
        {
            Message = $"{goat.Tag} → {label} · Main herd updated",
            GoatId = goat.Id,
            Tag = goat.Tag
        };
    }

    public async Task<ChangeDoeStatusBulkResultViewModel> ChangeDoeStatusBulkAsync(
        ChangeDoeStatusBulkViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.GoatIds.Count == 0)
            throw new InvalidOperationException("Select at least one doe.");
        var errors = new List<string>();
        var saved = 0;
        foreach (var id in model.GoatIds.Distinct())
        {
            try
            {
                await ChangeDoeStatusAsync(new ChangeDoeStatusViewModel
                {
                    GoatId = id,
                    TargetStatus = model.TargetStatus,
                    Date = model.Date,
                    ConfirmWarnings = model.ConfirmWarnings
                }, cancellationToken);
                saved++;
            }
            catch (InvalidOperationException ex)
            {
                var tag = await _context.Goats.AsNoTracking().Where(g => g.Id == id).Select(g => g.Tag).FirstOrDefaultAsync(cancellationToken);
                errors.Add($"{tag ?? id.ToString()}: {ex.Message}");
            }
        }

        return new ChangeDoeStatusBulkResultViewModel
        {
            Saved = saved,
            Errors = errors,
            Message = saved > 0 ? $"Updated {saved} doe(s)" : "No changes saved"
        };
    }

    private async Task RemoveDoeFromAllBreedingGroupsAsync(int goatId, CancellationToken cancellationToken)
    {
        var groups = await LoadBreedingGroupsAsync(cancellationToken);
        var changed = false;
        foreach (var g in groups)
        {
            var before = g.Members.Count;
            g.Members.RemoveAll(m => m.GoatId == goatId);
            if (g.Members.Count != before) changed = true;
        }
        if (changed)
            await SaveBreedingGroupsAsync(groups, cancellationToken);
    }
}