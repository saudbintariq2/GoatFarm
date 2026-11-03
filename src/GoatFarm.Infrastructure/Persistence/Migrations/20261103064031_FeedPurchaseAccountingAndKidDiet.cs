using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FeedPurchaseAccountingAndKidDiet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                table: "SupplierPayables",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "SupplierPayables",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "FeedPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocationMonths",
                table: "FeedPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "FeedPurchases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "FeedPurchases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "FeedPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "paid");

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                table: "FeedPurchases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UseAs",
                table: "FeedPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "recipe");

            migrationBuilder.AddColumn<string>(
                name: "UseType",
                table: "FeedPrices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "recipe");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CustomFeedingGroups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<decimal>(
                name: "FodderDryKgPerDay",
                table: "CustomFeedingGroups",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RecipeFrom",
                table: "CustomFeedingGroups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "milking");

            migrationBuilder.Sql("""
                UPDATE "FeedPurchases"
                SET "PaidAmount" = "Amount"
                WHERE "PaymentStatus" = 'paid';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "SupplierPayables");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "SupplierPayables");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "AllocationMonths",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "Supplier",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "UseAs",
                table: "FeedPurchases");

            migrationBuilder.DropColumn(
                name: "UseType",
                table: "FeedPrices");

            migrationBuilder.DropColumn(
                name: "FodderDryKgPerDay",
                table: "CustomFeedingGroups");

            migrationBuilder.DropColumn(
                name: "RecipeFrom",
                table: "CustomFeedingGroups");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CustomFeedingGroups",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
