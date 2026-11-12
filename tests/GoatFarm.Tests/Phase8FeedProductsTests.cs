using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Products;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GoatFarm.Tests;

public class Phase8FeedProductsTests
{
  private static (FeedService Feed, GoatFarmDbContext Db) CreateFeed()
  {
    var db = TestDb.Create();
    var goats = new GoatService(db);
    return (new FeedService(db, goats, new MilkService(db)), db);
  }

  private static IConfiguration DeviceConfig() =>
  new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
  {
    ["ApplicationSettings:DeviceCode"] = "K7Q"
  }).Build();

  [Fact]
  public void FeedSeasonHelper_OffSeasonZerosDirectUse()
  {
    var months = new Dictionary<string, int[]> { [FeedTypes.Binola] = [11, 12, 1, 2] };
    Assert.False(FeedSeasonHelper.IsActive(FeedTypes.Binola, new DateOnly(2026, 7, 15), months));
    Assert.True(FeedSeasonHelper.IsActive(FeedTypes.Binola, new DateOnly(2026, 1, 10), months));
    Assert.Equal("Nov–Feb", FeedSeasonHelper.SeasonLabel(months[FeedTypes.Binola]));
  }

  [Fact]
  public async Task UpdateFeedActiveMonths_PersistsInSettings()
  {
    var (feed, db) = CreateFeed();
    db.FeedPrices.Add(new FeedPrice
    {
      FeedType = FeedTypes.Binola,
      DisplayName = "Binola",
      UseType = FeedTypes.UseDirect,
      PricePerKg = 80
    });
    await db.SaveChangesAsync();
    await feed.UpdateFeedActiveMonthsAsync(new UpdateFeedActiveMonthsViewModel
    {
      FeedType = FeedTypes.Binola,
      Months = [5, 6, 7]
    });
    var page = await feed.GetFeedPageAsync(null, null, null);
    var direct = page.DirectFeeds.Single(f => f.FeedType == FeedTypes.Binola);
    Assert.Equal("May–Jul", direct.SeasonLabel);
  }

  [Fact]
  public void FarmRecordNumberFormat_SaleInvoice_MatchesV242()
  {
    var n = FarmRecordNumberFormat.FormatSaleInvoice("k7q", new DateOnly(2026, 10, 7), 7);
    Assert.Equal("SF-202610-K7Q-0007", n);
  }

  [Fact]
  public async Task SaveProductSale_AssignsInvoiceAndIssuesStock()
  {
    var db = TestDb.Create();
    var wh = new Warehouse { Name = "Raw", Location = "raw_tank", IsActive = true };
    var prod = new ProductCatalogItem { Sku = "MILK-RAW", Name = "Raw milk", Unit = "L" };
    db.Warehouses.Add(wh);
    db.ProductCatalogItems.Add(prod);
    db.StockMovements.Add(new StockMovement
    {
      Date = TestDb.Today,
      Type = StockMovementType.Receive,
      ProductId = 0,
      WarehouseId = 0,
      Qty = 50
    });
    await db.SaveChangesAsync();
    db.StockMovements.RemoveRange(db.StockMovements);
    db.StockMovements.Add(new StockMovement
    {
      Date = TestDb.Today,
      Type = StockMovementType.Receive,
      ProductId = prod.Id,
      WarehouseId = wh.Id,
      Qty = 50
    });
    await db.SaveChangesAsync();

    var products = new ProductsService(db, new LedgerService(db), DeviceConfig());
    await products.SaveProductSaleAsync(new SaveProductSaleViewModel
    {
      Date = TestDb.Today,
      ProductId = prod.Id,
      WarehouseId = wh.Id,
      ProductName = "Raw milk",
      Qty = 10,
      Rate = 120
    });
    var sale = await db.ProductSales.SingleAsync();
    Assert.StartsWith("SF-202", sale.InvoiceNo);
    Assert.Contains("-K7Q-", sale.InvoiceNo);
    var move = db.StockMovements.Single(m => m.Type == StockMovementType.Issue);
    Assert.Equal(10, move.Qty);
  }

  [Fact]
  public async Task SaveMeatSplit_ConvertsWholeToHalf()
  {
    var db = TestDb.Create();
    db.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MEAT", Name = "Carcass", Unit = "kg", Category = "meat" });
    var wh = new Warehouse { Name = "Chiller", Location = "meat_store", IsActive = true };
    db.Warehouses.Add(wh);
    await db.SaveChangesAsync();
    var slaughter = new SlaughterRecord
    {
      Date = TestDb.Today,
      TagText = "T-1",
      CarcassKg = 20,
      RemainingKg = 20,
      CarcassForm = MeatCarcassForms.Whole,
      HalfPieces = 1,
      WarehouseId = wh.Id
    };
    db.SlaughterRecords.Add(slaughter);
    await db.SaveChangesAsync();
    var products = new ProductsService(db, new LedgerService(db), DeviceConfig());
    await products.SaveMeatSplitAsync(new SaveMeatSplitViewModel
    {
      SlaughterId = slaughter.Id,
      Date = TestDb.Today,
      HalfAKg = 10
    });
    var row = await db.SlaughterRecords.SingleAsync();
    Assert.Equal(MeatCarcassForms.Half, row.CarcassForm);
    Assert.Equal(2, row.HalfPieces);
    Assert.Equal(20, row.RemainingKg);
  }

  [Fact]
  public async Task SaveMeatCut_IssuesCutStockAndReducesCarcass()
  {
    var db = TestDb.Create();
    var meat = new ProductCatalogItem { Sku = "MEAT", Name = "Carcass", Unit = "kg", Category = "meat" };
    var leg = new ProductCatalogItem { Sku = "LEG", Name = "Leg", Unit = "kg", Category = "meat", MeatType = "cuts" };
    var wh = new Warehouse { Name = "Chiller", Location = "meat_store", IsActive = true };
    db.ProductCatalogItems.AddRange(meat, leg);
    db.Warehouses.Add(wh);
    await db.SaveChangesAsync();
    var slaughter = new SlaughterRecord
    {
      Date = TestDb.Today,
      TagText = "T-2",
      CarcassKg = 18,
      RemainingKg = 18,
      UnitCostPerKg = 100,
      CarcassForm = MeatCarcassForms.Whole,
      HalfPieces = 1,
      WarehouseId = wh.Id
    };
    db.SlaughterRecords.Add(slaughter);
    await db.SaveChangesAsync();
    var products = new ProductsService(db, new LedgerService(db), DeviceConfig());
    await products.SaveMeatCutAsync(new SaveMeatCutViewModel
    {
      SlaughterId = slaughter.Id,
      Date = TestDb.Today,
      CutWarehouseId = wh.Id,
      InputKg = 10,
      CostMethod = "weight",
      Cuts = [new MeatCutLineViewModel { ProductId = leg.Id, Kg = 8 }]
    });
    var row = await db.SlaughterRecords.SingleAsync();
    Assert.Equal(8, row.RemainingKg);
    Assert.Single(await db.StockMovements.Where(m => m.Type == StockMovementType.Issue).ToListAsync());
    Assert.Contains(await db.StockMovements.Where(m => m.Type == StockMovementType.Receive).ToListAsync(),
      m => m.ProductId == leg.Id && m.Qty == 8);
  }

  [Fact]
  public async Task SaveProductSale_RejectsOversell()
  {
    var db = TestDb.Create();
    var wh = new Warehouse { Name = "Store", Location = "dairy_store", IsActive = true };
    var prod = new ProductCatalogItem { Sku = "X", Name = "Yogurt", Unit = "units" };
    db.Warehouses.Add(wh);
    db.ProductCatalogItems.Add(prod);
    await db.SaveChangesAsync();
    db.StockMovements.Add(new StockMovement
    {
      Date = TestDb.Today,
      Type = StockMovementType.Receive,
      ProductId = prod.Id,
      WarehouseId = wh.Id,
      Qty = 5
    });
    await db.SaveChangesAsync();
    var products = new ProductsService(db, new LedgerService(db), DeviceConfig());
    await Assert.ThrowsAsync<InvalidOperationException>(() => products.SaveProductSaleAsync(new SaveProductSaleViewModel
    {
      Date = TestDb.Today,
      ProductId = prod.Id,
      WarehouseId = wh.Id,
      ProductName = "Yogurt",
      Qty = 20,
      Rate = 50
    }));
  }
}
