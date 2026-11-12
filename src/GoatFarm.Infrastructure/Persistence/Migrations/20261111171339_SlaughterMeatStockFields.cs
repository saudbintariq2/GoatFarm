using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaughterMeatStockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarcassForm",
                table: "SlaughterRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HalfPieces",
                table: "SlaughterRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingKg",
                table: "SlaughterRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostPerKg",
                table: "SlaughterRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "SlaughterRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "SlaughterRecords"
                SET "RemainingKg" = "CarcassKg",
                    "CarcassForm" = 'whole',
                    "HalfPieces" = 1
                WHERE "CarcassKg" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarcassForm",
                table: "SlaughterRecords");

            migrationBuilder.DropColumn(
                name: "HalfPieces",
                table: "SlaughterRecords");

            migrationBuilder.DropColumn(
                name: "RemainingKg",
                table: "SlaughterRecords");

            migrationBuilder.DropColumn(
                name: "UnitCostPerKg",
                table: "SlaughterRecords");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "SlaughterRecords");
        }
    }
}
