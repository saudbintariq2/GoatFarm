using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyAccountOpeningBalanceLocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OpeningBalanceLocked",
                table: "MoneyAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "MoneyAccounts" SET "OpeningBalanceLocked" = TRUE WHERE "OpeningBalance" <> 0;
                UPDATE "MoneyAccounts" m SET "OpeningBalanceLocked" = TRUE
                WHERE EXISTS (
                    SELECT 1 FROM "LedgerTransactions" t
                    WHERE t."AccountId" = m."Id" OR t."CounterAccountId" = m."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningBalanceLocked",
                table: "MoneyAccounts");
        }
    }
}
