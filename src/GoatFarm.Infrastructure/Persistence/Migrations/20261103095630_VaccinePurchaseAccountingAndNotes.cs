using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VaccinePurchaseAccountingAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Vaccines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "VaccinePurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "VaccinePurchases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "VaccinePurchases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "VaccinePurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "paid");

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                table: "VaccinePurchases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "VaccinePurchases"
                SET "PaidAmount" = "Amount"
                WHERE "PaymentStatus" = 'paid';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "VaccinePurchases");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "VaccinePurchases");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "VaccinePurchases");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "VaccinePurchases");

            migrationBuilder.DropColumn(
                name: "Supplier",
                table: "VaccinePurchases");
        }
    }
}
