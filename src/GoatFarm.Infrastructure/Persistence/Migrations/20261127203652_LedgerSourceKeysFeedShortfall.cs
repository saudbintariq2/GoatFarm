using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LedgerSourceKeysFeedShortfall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ProductSales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonCash",
                table: "OwnerInvestments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                table: "LedgerTransactions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "LedgerTransactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "Incomes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Incomes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortfallKg",
                table: "FeedUsageRecords",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Expenses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_SourceType_SourceId",
                table: "LedgerTransactions",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_SourceType_SourceId",
                table: "Incomes",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SourceType_SourceId",
                table: "Expenses",
                columns: new[] { "SourceType", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_SourceType_SourceId",
                table: "LedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_SourceType_SourceId",
                table: "Incomes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_SourceType_SourceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ProductSales");

            migrationBuilder.DropColumn(
                name: "IsNonCash",
                table: "OwnerInvestments");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "ShortfallKg",
                table: "FeedUsageRecords");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Expenses");
        }
    }
}
