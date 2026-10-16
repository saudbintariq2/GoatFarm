using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

/// <summary>Development-only sample rows so each module has something to click through. Skips sections that already have data.</summary>
public static class DemoDataSeedHelper
{
    public const string MarkerKey = "DemoData:V1";

    public static async Task SeedAsync(GoatFarmDbContext context, bool forceReload, CancellationToken cancellationToken = default)
    {
        if (forceReload)
        {
            var existingMarker = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == MarkerKey, cancellationToken);
            if (existingMarker is not null)
            {
                context.AppSettings.Remove(existingMarker);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        await SeedHerdAndBreedingAsync(context, today, cancellationToken);
        await SeedMilkAsync(context, today, monthStart, cancellationToken);
        await SeedFeedAndHealthAsync(context, today, monthStart, cancellationToken);
        await SeedFinanceAsync(context, today, monthStart, cancellationToken);
        await SeedTasksAsync(context, today, cancellationToken);
        await SeedTradingAsync(context, today, monthStart, cancellationToken);
        await SeedProductsAsync(context, today, monthStart, cancellationToken);
        await SeedProjectsAsync(context, today, cancellationToken);
        await SeedLedgerAsync(context, today, monthStart, cancellationToken);
        await SeedTreatmentsAsync(context, today, cancellationToken);
        await SeedAuditAndApprovalAsync(context, cancellationToken);

        var marker = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == MarkerKey, cancellationToken);
        if (marker is null)
            context.AppSettings.Add(new AppSetting { Key = MarkerKey, Value = DateTime.UtcNow.ToString("O") });
        else
            marker.Value = DateTime.UtcNow.ToString("O");

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedHerdAndBreedingAsync(GoatFarmDbContext context, DateOnly today, CancellationToken cancellationToken)
    {
        if (await context.Goats.AnyAsync(cancellationToken)) return;

        var group = new GoatGroup { Name = "Main herd", Description = "Demo group" };
        context.GoatGroups.Add(group);
        await context.SaveChangesAsync(cancellationToken);

        var buck = new Goat
        {
            Tag = "B-001", Name = "Raja", Breed = "Beetal", Gender = GoatGender.Male, Status = GoatStatus.Buck,
            Source = GoatSource.Born, EventDate = today.AddMonths(-24), GroupId = group.Id, HealthState = GoatHealthState.Healthy
        };
        var doe1 = new Goat
        {
            Tag = "D-101", Name = "Gulabo", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Milking,
            Source = GoatSource.Born, EventDate = today.AddMonths(-18), GroupId = group.Id, HealthState = GoatHealthState.Healthy
        };
        var doe2 = new Goat
        {
            Tag = "D-102", Name = "Motia", Breed = "Kamori", Gender = GoatGender.Female, Status = GoatStatus.Pregnant,
            Source = GoatSource.Bought, PurchasePrice = 45000, EventDate = today.AddMonths(-12), GroupId = group.Id,
            MatedDate = today.AddDays(-45), BuckTag = "B-001", BreedingStage = BreedingStage.ScanPending,
            HealthState = GoatHealthState.Healthy
        };
        var kid = new Goat
        {
            Tag = "K-201", Name = "Chotu", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Kid,
            Source = GoatSource.Born, EventDate = today.AddMonths(-2), DateOfBirth = today.AddMonths(-2), GroupId = group.Id,
            HealthState = GoatHealthState.Healthy
        };
        context.Goats.AddRange(buck, doe1, doe2, kid);
        await context.SaveChangesAsync(cancellationToken);

        if (!await context.BreedingEvents.AnyAsync(cancellationToken))
        {
            context.BreedingEvents.Add(new BreedingEvent
            {
                DoeId = doe2.Id,
                BuckId = buck.Id,
                MatedDate = today.AddDays(-45),
                Outcome = BreedingOutcome.Open,
                Notes = "Demo cross"
            });
        }
    }

    private static async Task SeedMilkAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (await context.MilkProductions.AnyAsync(cancellationToken)) return;

        for (var i = 0; i < 7; i++)
        {
            var d = today.AddDays(-i);
            context.MilkProductions.Add(new MilkProduction { Date = d, Breed = "Beetal", Liters = 38 + i, Comment = "Demo" });
        }
        context.MilkSales.Add(new MilkSale { Date = today.AddDays(-1), Liters = 30, Rate = 220, Amount = 6600, Comment = "Shop sale" });
        context.MilkWastes.Add(new MilkWaste { Date = today.AddDays(-2), Liters = 2, Notes = "Spoiled sample" });
    }

    private static async Task SeedFeedAndHealthAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (!await context.FeedPurchases.AnyAsync(cancellationToken))
        {
            context.FeedPurchases.Add(new FeedPurchase
            {
                Date = today.AddDays(-3), FeedType = "wanda", Kg = 500, RatePerKg = 85, Amount = 42500, Comment = "Demo buy"
            });
        }

        if (!await context.VaccinePurchases.AnyAsync(cancellationToken))
        {
            context.VaccinePurchases.Add(new VaccinePurchase
            {
                Date = monthStart, Name = "PPR", Qty = 20, Unit = "doses", Amount = 8000, Comment = "Demo stock"
            });
        }

        if (!await context.Reminders.AnyAsync(cancellationToken))
        {
            context.Reminders.Add(new Reminder
            {
                Title = "Deworming due — demo",
                Scope = VaccineScope.All,
                ReminderDate = today.AddDays(5)
            });
        }
    }

    private static async Task SeedFinanceAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (await context.Incomes.AnyAsync(cancellationToken)) return;

        context.Incomes.Add(new Income { Type = "Milk sales", Amount = 6600, Date = today.AddDays(-1), Comment = "Demo" });
        context.Expenses.Add(new Expense { Type = "Feed", Amount = 42500, Date = today.AddDays(-3), Comment = "Demo" });
        context.OwnerInvestments.Add(new OwnerInvestment { Note = "Owner top-up (demo)", Amount = 100000, Date = monthStart });
        context.Assets.Add(new Asset { Name = "Milking cans", Type = "Equipment", Cost = 15000, PurchaseDate = monthStart.AddMonths(-2) });
        context.Employees.Add(new Employee { Name = "Ahmed (demo)", Role = "Herdsman", MonthlySalary = 25000 });
        context.RecurringCosts.Add(new RecurringCost { Name = "Electricity — shed", Amount = 3500, Period = RecurringCostPeriod.Month });
    }

    private static async Task SeedTasksAsync(GoatFarmDbContext context, DateOnly today, CancellationToken cancellationToken)
    {
        if (await context.FarmTasks.AnyAsync(cancellationToken)) return;

        context.FarmTasks.Add(new FarmTask
        {
            Title = "Clean milking area",
            Notes = "Demo task",
            Type = FarmTaskType.Task,
            Priority = FarmTaskPriority.Normal,
            Status = FarmTaskStatus.ToDo,
            DueDate = today.AddDays(1)
        });
        context.FarmTasks.Add(new FarmTask
        {
            Title = "Order vaccine stock",
            Type = FarmTaskType.Reminder,
            Priority = FarmTaskPriority.High,
            Status = FarmTaskStatus.InProgress,
            DueDate = today
        });
    }

    private static async Task SeedTradingAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (await context.TradeEntries.AnyAsync(cancellationToken)) return;

        var item = new TradeItem { Name = "Moongphali bhoosa", Unit = "kg", DefaultPrice = 42, UnitNote = "40 kg = 1 maund" };
        var store = new TradeStore { Name = "Trading Godown" };
        var cash = new TradeAccount { Name = "Trading Cash", Type = TradeAccountType.Cash, OpeningBalance = 5000 };
        var bank = new TradeAccount { Name = "Trading Bank", Type = TradeAccountType.Bank, OpeningBalance = 20000 };
        var party = new TradeParty { Name = "Ali Traders", Role = TradePartyRole.Supplier, Phone = "0300-0000000" };
        var cat = new TradeExpenseCategory { Name = "Transport / kiraya" };
        context.TradeItems.Add(item);
        context.TradeStores.Add(store);
        context.TradeAccounts.AddRange(cash, bank);
        context.TradeParties.Add(party);
        context.TradeExpenseCategories.Add(cat);
        await context.SaveChangesAsync(cancellationToken);

        context.TradeEntries.Add(new TradeEntry
        {
            Type = TradeEntryType.Buy, Date = monthStart.AddDays(2), ItemId = item.Id, StoreId = store.Id,
            AccountId = bank.Id, PartyId = party.Id, Qty = 2000, Rate = 40, Amount = 80000, Notes = "Demo purchase"
        });
        context.TradeEntries.Add(new TradeEntry
        {
            Type = TradeEntryType.Sell, Date = today.AddDays(-4), ItemId = item.Id, StoreId = store.Id,
            AccountId = cash.Id, Qty = 400, Rate = 45, Amount = 18000, Notes = "Demo sale"
        });
        context.TradeEntries.Add(new TradeEntry
        {
            Type = TradeEntryType.Expense, Date = today.AddDays(-2), CategoryId = cat.Id, AccountId = cash.Id,
            Amount = 2500, Notes = "Demo transport"
        });
    }

    private static async Task SeedProductsAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (await context.StockMovements.AnyAsync(cancellationToken)) return;

        var milk = new ProductCatalogItem { Sku = "MILK-RAW", Name = "Raw milk", Unit = "L", DefaultPrice = 220 };
        var ghee = new ProductCatalogItem { Sku = "GHEE", Name = "Ghee", Unit = "kg", DefaultPrice = 2800 };
        var wh = new Warehouse { Name = "Main cold store", Location = "Shed A" };
        context.ProductCatalogItems.AddRange(milk, ghee);
        context.Warehouses.Add(wh);
        await context.SaveChangesAsync(cancellationToken);

        context.MilkingSessions.Add(new MilkingSession
        {
            Date = today, Session = "AM", Breed = "Beetal", Liters = 22, RejectedLiters = 0.5m, Notes = "Demo session"
        });
        context.StockMovements.Add(new StockMovement
        {
            Date = today.AddDays(-1), Type = StockMovementType.Receive, ProductId = milk.Id, WarehouseId = wh.Id,
            Qty = 100, UnitCost = 180, Notes = "Demo receive"
        });
        context.ProductSales.Add(new ProductSale
        {
            Date = today.AddDays(-1), ProductId = ghee.Id, ProductName = "Ghee", Qty = 2, Rate = 2800, Amount = 5600,
            IsCredit = false, Notes = "Demo retail"
        });

        if (!await context.SlaughterRecords.AnyAsync(cancellationToken))
        {
            var meatGoat = await context.Goats.FirstOrDefaultAsync(g => g.Status == GoatStatus.Sale, cancellationToken);
            context.SlaughterRecords.Add(new SlaughterRecord
            {
                Date = monthStart.AddDays(10),
                TagText = meatGoat?.Tag ?? "S-099",
                LiveWeightKg = 35,
                CarcassKg = 18,
                SaleAmount = 42000,
                Notes = "Demo Eid cull"
            });
        }
    }

    private static async Task SeedProjectsAsync(GoatFarmDbContext context, DateOnly today, CancellationToken cancellationToken)
    {
        if (await context.FarmProjects.AnyAsync(cancellationToken)) return;

        var solar = new FarmProject
        {
            Name = "Solar for milk chiller",
            Status = ProjectStatus.Active,
            StartDate = today.AddMonths(-3),
            BudgetAmount = 350000,
            Description = "Demo CAPEX project"
        };
        context.FarmProjects.Add(solar);
        await context.SaveChangesAsync(cancellationToken);

        context.ProjectExpenditures.Add(new ProjectExpenditure
        {
            ProjectId = solar.Id, Date = today.AddMonths(-2), Description = "Panels + inverter", Amount = 280000, Vendor = "Solar Co."
        });
        context.FixedAssetRegisters.Add(new FixedAssetRegister
        {
            Name = "5kW Solar array", Category = FixedAssetCategory.Solar, Cost = 280000,
            PurchaseDate = today.AddMonths(-2), ProjectId = solar.Id
        });
        context.SolarProductionLogs.Add(new SolarProductionLog
        {
            Date = today.AddDays(-1), KwhGenerated = 18.5m, SavingsEstimate = 1200, Notes = "Demo log"
        });
    }

    private static async Task SeedLedgerAsync(GoatFarmDbContext context, DateOnly today, DateOnly monthStart, CancellationToken cancellationToken)
    {
        if (await context.LedgerTransactions.AnyAsync(cancellationToken)) return;

        var cash = new MoneyAccount { Name = "Farm Cash", Kind = MoneyAccountKind.Cash, OpeningBalance = 25000 };
        var bank = new MoneyAccount { Name = "Farm Bank", Kind = MoneyAccountKind.Bank, OpeningBalance = 150000 };
        var customer = new BusinessCustomer { Name = "City Dairy Shop", Phone = "042-1111111", Place = "Lahore" };
        context.MoneyAccounts.AddRange(cash, bank);
        context.BusinessCustomers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);

        context.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = today.AddDays(-5), Kind = LedgerTransactionKind.Income, AccountId = bank.Id,
            Amount = 6600, Category = "Milk", Notes = "Demo deposit"
        });
        context.CustomerInvoices.Add(new CustomerInvoice
        {
            CustomerId = customer.Id, Date = today.AddDays(-7), DueDate = today.AddDays(7),
            Description = "Ghee order — demo", Amount = 12000, PaidAmount = 4000, Status = InvoiceStatus.Partial
        });
        context.SupplierPayables.Add(new SupplierPayable
        {
            SupplierName = "Feed merchant", Date = today.AddDays(-3), DueDate = today.AddDays(14),
            Description = "Wanda load", Amount = 42500, PaidAmount = 20000, Status = PayableStatus.Partial
        });
        context.BudgetLines.Add(new BudgetLine { Year = today.Year, Month = today.Month, Category = "Feed", PlannedAmount = 50000 });
        context.ProfitSharePartners.Add(new ProfitSharePartner { Name = "Partner A (demo)", SharePercent = 30 });
    }

    private static async Task SeedTreatmentsAsync(GoatFarmDbContext context, DateOnly today, CancellationToken cancellationToken)
    {
        if (await context.GoatTreatments.AnyAsync(cancellationToken)) return;

        var sick = await context.Goats.FirstOrDefaultAsync(g => g.Tag == "D-101", cancellationToken);
        if (sick is null) return;

        context.GoatTreatments.Add(new GoatTreatment
        {
            GoatId = sick.Id,
            StartDate = today.AddDays(-3),
            Diagnosis = "Mastitis (demo)",
            Medicine = "Antibiotic course",
            Cost = 3500,
            WithdrawalDays = 7,
            Notes = "Sample treatment row"
        });
        sick.HealthState = GoatHealthState.UnderTreatment;
    }

    private static async Task SeedAuditAndApprovalAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.AuditLogEntries.AnyAsync(cancellationToken))
        {
            context.AuditLogEntries.AddRange(
                new AuditLogEntry
                {
                    Module = "trading", Action = AuditAction.Create, Summary = "Demo: recorded straw purchase",
                    UserDisplay = "System seed", ShowInNotices = true
                },
                new AuditLogEntry
                {
                    Module = "finance", Action = AuditAction.Update, Summary = "Demo: sample invoice added",
                    UserDisplay = "System seed", ShowInNotices = true
                });
        }

        if (!await context.ApprovalRequests.AnyAsync(cancellationToken))
        {
            context.ApprovalRequests.Add(new ApprovalRequest
            {
                Module = "herd",
                ActionKey = "Delete",
                PayloadJson = """{"entity":"Goat","action":"Delete","id":0}""",
                RequestedByUserId = "demo-seed",
                RequestedByDisplay = "Demo Staff",
                Status = ApprovalStatus.Pending
            });
        }
    }
}
