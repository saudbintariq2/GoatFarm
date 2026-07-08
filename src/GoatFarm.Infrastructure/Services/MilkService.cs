using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Milk;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class MilkService : IMilkService
{
    private const int DefaultPageSize = 10;
    private readonly GoatFarmDbContext _context;

    public MilkService(GoatFarmDbContext context) => _context = context;

    public async Task<MilkPageViewModel> GetMilkPageAsync(
        int prodPage = 1,
        int salePage = 1,
        int wastePage = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        var month = MonthHelper.CurrentMonthKey();
        var prodL = GetMilkLitersProduced(month);
        var soldL = GetMilkLitersSold(month);
        var wastedL = GetMilkLitersWasted(month);
        var inc = GetMilkIncomeMonth(month);
        var dayN = DateTime.Today.Day;

        var (productions, productionPagination) = await GetPagedProductionsAsync(prodPage, pageSize, cancellationToken);
        var (sales, salePagination) = await GetPagedSalesAsync(salePage, pageSize, cancellationToken);
        var (wastes, wastePagination) = await GetPagedWastesAsync(wastePage, pageSize, cancellationToken);

        return new MilkPageViewModel
        {
            MilkIncome = inc,
            LitersSold = soldL,
            LitersProduced = prodL,
            LitersWasted = wastedL,
            LitersLeft = prodL - soldL - wastedL,
            LitersPerDayAvg = dayN > 0 ? Math.Round(prodL / dayN) : 0,
            Productions = productions,
            Sales = sales,
            Wastes = wastes,
            ProductionPagination = productionPagination,
            SalePagination = salePagination,
            WastePagination = wastePagination,
            ProdPage = productionPagination.Page,
            SalePage = salePagination.Page,
            WastePage = wastePagination.Page,
            NewProduction = new CreateMilkProductionViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            NewSale = new CreateMilkSaleViewModel { Date = DateOnly.FromDateTime(DateTime.Today) },
            NewWaste = new CreateMilkWasteViewModel { Date = DateOnly.FromDateTime(DateTime.Today) }
        };
    }

    public async Task<MilkProductionViewModel> AddProductionAsync(CreateMilkProductionViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new MilkProduction
        {
            Date = model.Date,
            Breed = model.Breed,
            Liters = model.Liters
        };
        _context.MilkProductions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkProductionViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Breed = entity.Breed,
            Liters = entity.Liters
        };
    }

    public async Task<MilkSaleViewModel> AddSaleAsync(CreateMilkSaleViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new MilkSale
        {
            Date = model.Date,
            Liters = model.Liters,
            Rate = model.Rate,
            Amount = Math.Round(model.Liters * model.Rate)
        };
        _context.MilkSales.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkSaleViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Liters = entity.Liters,
            Rate = entity.Rate,
            Amount = entity.Amount
        };
    }

    public async Task<MilkWasteViewModel> AddWasteAsync(CreateMilkWasteViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new MilkWaste
        {
            Date = model.Date,
            Liters = model.Liters,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
        };
        _context.MilkWastes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkWasteViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Liters = entity.Liters,
            Notes = entity.Notes
        };
    }

    public async Task<MilkProductionViewModel?> UpdateProductionAsync(int id, CreateMilkProductionViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkProductions.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Date = model.Date;
        entity.Breed = model.Breed;
        entity.Liters = model.Liters;
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkProductionViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Breed = entity.Breed,
            Liters = entity.Liters
        };
    }

    public async Task<MilkSaleViewModel?> UpdateSaleAsync(int id, CreateMilkSaleViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkSales.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Date = model.Date;
        entity.Liters = model.Liters;
        entity.Rate = model.Rate;
        entity.Amount = Math.Round(model.Liters * model.Rate);
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkSaleViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Liters = entity.Liters,
            Rate = entity.Rate,
            Amount = entity.Amount
        };
    }

    public async Task<MilkWasteViewModel?> UpdateWasteAsync(int id, CreateMilkWasteViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkWastes.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Date = model.Date;
        entity.Liters = model.Liters;
        entity.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return new MilkWasteViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            Liters = entity.Liters,
            Notes = entity.Notes
        };
    }

    public async Task<bool> DeleteProductionAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkProductions.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.MilkProductions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSaleAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkSales.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.MilkSales.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteWasteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MilkWastes.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.MilkWastes.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal GetMilkLitersProduced(string month)
    {
        var (start, end) = MonthHelper.GetMonthRange(month);
        return _context.MilkProductions.AsNoTracking()
            .Where(p => p.Date >= start && p.Date < end)
            .Sum(p => p.Liters);
    }

    public decimal GetMilkLitersSold(string month)
    {
        var (start, end) = MonthHelper.GetMonthRange(month);
        return _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .Sum(s => s.Liters);
    }

    public decimal GetMilkLitersWasted(string month)
    {
        var (start, end) = MonthHelper.GetMonthRange(month);
        return _context.MilkWastes.AsNoTracking()
            .Where(w => w.Date >= start && w.Date < end)
            .Sum(w => w.Liters);
    }

    public decimal GetMilkIncomeMonth(string month)
    {
        var (start, end) = MonthHelper.GetMonthRange(month);
        return _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .Sum(s => s.Amount);
    }

    private async Task<(List<MilkProductionViewModel> Items, PaginationViewModel Pagination)> GetPagedProductionsAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.MilkProductions.AsNoTracking()
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id);
        var totalItems = await query.CountAsync(cancellationToken);
        var pagination = BuildPagination(page, pageSize, totalItems);
        var items = await query
            .Skip((pagination.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new MilkProductionViewModel
            {
                Id = p.Id,
                Date = p.Date,
                Breed = p.Breed,
                Liters = p.Liters
            }).ToListAsync(cancellationToken);
        return (items, pagination);
    }

    private async Task<(List<MilkSaleViewModel> Items, PaginationViewModel Pagination)> GetPagedSalesAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.MilkSales.AsNoTracking()
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id);
        var totalItems = await query.CountAsync(cancellationToken);
        var pagination = BuildPagination(page, pageSize, totalItems);
        var items = await query
            .Skip((pagination.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new MilkSaleViewModel
            {
                Id = s.Id,
                Date = s.Date,
                Liters = s.Liters,
                Rate = s.Rate,
                Amount = s.Amount
            }).ToListAsync(cancellationToken);
        return (items, pagination);
    }

    private async Task<(List<MilkWasteViewModel> Items, PaginationViewModel Pagination)> GetPagedWastesAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.MilkWastes.AsNoTracking()
            .OrderByDescending(w => w.Date).ThenByDescending(w => w.Id);
        var totalItems = await query.CountAsync(cancellationToken);
        var pagination = BuildPagination(page, pageSize, totalItems);
        var items = await query
            .Skip((pagination.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new MilkWasteViewModel
            {
                Id = w.Id,
                Date = w.Date,
                Liters = w.Liters,
                Notes = w.Notes
            }).ToListAsync(cancellationToken);
        return (items, pagination);
    }

    private static PaginationViewModel BuildPagination(int page, int pageSize, int totalItems)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        return new PaginationViewModel
        {
            Page = Math.Clamp(page, 1, totalPages),
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }
}
