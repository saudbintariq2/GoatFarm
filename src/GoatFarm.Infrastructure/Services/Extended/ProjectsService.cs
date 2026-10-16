using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Projects;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class ProjectsService : IProjectsService
{
    private readonly GoatFarmDbContext _context;

    public ProjectsService(GoatFarmDbContext context) => _context = context;

    public async Task<ProjectsPageViewModel> GetPageAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _context.FarmProjects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var exp = await _context.ProjectExpenditures.AsNoTracking().Include(e => e.Project)
            .OrderByDescending(e => e.Date).Take(100).ToListAsync(cancellationToken);
        var assets = await _context.FixedAssetRegisters.AsNoTracking().Include(a => a.Project)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var solar = await _context.SolarProductionLogs.AsNoTracking().OrderByDescending(s => s.Date).Take(60).ToListAsync(cancellationToken);

        var spentByProject = exp.GroupBy(e => e.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return new ProjectsPageViewModel
        {
            TotalCapex = assets.Sum(a => a.Cost),
            ActiveBudget = projects.Where(p => p.Status == Domain.Enums.ProjectStatus.Active).Sum(p => p.BudgetAmount),
            Projects = projects.Select(p => new ProjectRowViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status.ToString(),
                BudgetAmount = p.BudgetAmount,
                Spent = spentByProject.GetValueOrDefault(p.Id, 0),
                StartDate = p.StartDate?.ToString("yyyy-MM-dd")
            }).ToList(),
            Expenditures = exp.Select(e => new ProjectExpenditureRowViewModel
            {
                Id = e.Id,
                ProjectId = e.ProjectId,
                ProjectName = e.Project.Name,
                DateDisplay = e.Date.ToString("yyyy-MM-dd"),
                Description = e.Description,
                Amount = e.Amount
            }).ToList(),
            FixedAssets = assets.Select(a => new FixedAssetRowViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Category = a.Category.ToString(),
                Cost = a.Cost,
                PurchaseDate = a.PurchaseDate?.ToString("yyyy-MM-dd"),
                ProjectName = a.Project?.Name
            }).ToList(),
            SolarLogs = solar.Select(s => new SolarLogRowViewModel
            {
                Id = s.Id,
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                KwhGenerated = s.KwhGenerated,
                SavingsEstimate = s.SavingsEstimate
            }).ToList()
        };
    }

    public async Task<object> SaveProjectAsync(SaveProjectViewModel model, CancellationToken cancellationToken = default)
    {
        FarmProject row;
        if (model.Id is > 0)
            row = await _context.FarmProjects.FirstAsync(p => p.Id == model.Id, cancellationToken);
        else { row = new FarmProject(); _context.FarmProjects.Add(row); }
        row.Name = model.Name.Trim();
        row.Description = model.Description;
        row.Status = model.Status;
        row.StartDate = model.StartDate;
        row.TargetEndDate = model.TargetEndDate;
        row.BudgetAmount = model.BudgetAmount;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteProjectAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.FarmProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null) return false;
        _context.FarmProjects.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveExpenditureAsync(SaveProjectExpenditureViewModel model, CancellationToken cancellationToken = default)
    {
        ProjectExpenditure row;
        if (model.Id is > 0)
            row = await _context.ProjectExpenditures.FirstAsync(e => e.Id == model.Id, cancellationToken);
        else { row = new ProjectExpenditure(); _context.ProjectExpenditures.Add(row); }
        row.ProjectId = model.ProjectId;
        row.Date = model.Date;
        row.Description = model.Description.Trim();
        row.Amount = model.Amount;
        row.Vendor = model.Vendor;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveFixedAssetAsync(SaveFixedAssetViewModel model, CancellationToken cancellationToken = default)
    {
        FixedAssetRegister row;
        if (model.Id is > 0)
            row = await _context.FixedAssetRegisters.FirstAsync(a => a.Id == model.Id, cancellationToken);
        else { row = new FixedAssetRegister(); _context.FixedAssetRegisters.Add(row); }
        row.Name = model.Name.Trim();
        row.Category = model.Category;
        row.Cost = model.Cost;
        row.PurchaseDate = model.PurchaseDate;
        row.ProjectId = model.ProjectId;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveSolarLogAsync(SaveSolarLogViewModel model, CancellationToken cancellationToken = default)
    {
        SolarProductionLog row;
        if (model.Id is > 0)
            row = await _context.SolarProductionLogs.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new SolarProductionLog(); _context.SolarProductionLogs.Add(row); }
        row.Date = model.Date;
        row.KwhGenerated = model.KwhGenerated;
        row.SavingsEstimate = model.SavingsEstimate;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }
}
