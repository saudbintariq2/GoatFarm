using GoatFarm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoatFarm.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.Property(x => x.Module).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(64);
        builder.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UserDisplay).HasMaxLength(200);
        builder.HasIndex(x => x.CreatedDate);
        builder.HasIndex(x => new { x.Module, x.CreatedDate });
    }
}

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests");
        builder.Property(x => x.Module).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActionKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.Status);
    }
}

public class TradeItemConfiguration : IEntityTypeConfiguration<TradeItem>
{
    public void Configure(EntityTypeBuilder<TradeItem> builder)
    {
        builder.ToTable("TradeItems");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DefaultPrice).HasPrecision(18, 2);
    }
}

public class TradeEntryConfiguration : IEntityTypeConfiguration<TradeEntry>
{
    public void Configure(EntityTypeBuilder<TradeEntry> builder)
    {
        builder.ToTable("TradeEntries");
        builder.Property(x => x.Qty).HasPrecision(18, 3);
        builder.Property(x => x.Rate).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.Date);
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ToStore).WithMany().HasForeignKey(x => x.ToStoreId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MoneyAccountConfiguration : IEntityTypeConfiguration<MoneyAccount>
{
    public void Configure(EntityTypeBuilder<MoneyAccount> builder)
    {
        builder.ToTable("MoneyAccounts");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.OpeningBalance).HasPrecision(18, 2);
    }
}

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Category).HasMaxLength(120);
        builder.Property(x => x.Reference).HasMaxLength(120);
        builder.HasIndex(x => x.Date);
    }
}

public class GoatTreatmentConfiguration : IEntityTypeConfiguration<GoatTreatment>
{
    public void Configure(EntityTypeBuilder<GoatTreatment> builder)
    {
        builder.ToTable("GoatTreatments");
        builder.Property(x => x.Diagnosis).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Medicine).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.HasOne(x => x.Goat).WithMany().HasForeignKey(x => x.GoatId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.GoatId);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.Property(x => x.Qty).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class FixedAssetRegisterConfiguration : IEntityTypeConfiguration<FixedAssetRegister>
{
    public void Configure(EntityTypeBuilder<FixedAssetRegister> builder)
    {
        builder.ToTable("FixedAssetRegisters");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CustomerInvoiceConfiguration : IEntityTypeConfiguration<CustomerInvoice>
{
    public void Configure(EntityTypeBuilder<CustomerInvoice> builder)
    {
        builder.ToTable("CustomerInvoices");
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierPayableConfiguration : IEntityTypeConfiguration<SupplierPayable>
{
    public void Configure(EntityTypeBuilder<SupplierPayable> builder)
    {
        builder.ToTable("SupplierPayables");
        builder.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
    }
}
