using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public class GoatFarmDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public GoatFarmDbContext(DbContextOptions<GoatFarmDbContext> options) : base(options) { }

    public DbSet<Goat> Goats => Set<Goat>();
    public DbSet<GoatGroup> GoatGroups => Set<GoatGroup>();
    public DbSet<FeedPrice> FeedPrices => Set<FeedPrice>();
    public DbSet<FeedPlan> FeedPlans => Set<FeedPlan>();
    public DbSet<FeedPlanItem> FeedPlanItems => Set<FeedPlanItem>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MilkProduction> MilkProductions => Set<MilkProduction>();
    public DbSet<MilkSale> MilkSales => Set<MilkSale>();
    public DbSet<MilkWaste> MilkWastes => Set<MilkWaste>();
    public DbSet<Vaccine> Vaccines => Set<Vaccine>();
    public DbSet<VaccinationHistory> VaccinationHistories => Set<VaccinationHistory>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(GoatFarmDbContext).Assembly);
    }
}
