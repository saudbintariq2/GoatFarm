using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendProjectsAssetsParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SavingsEstimate",
                table: "SolarProductionLogs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "SolarProductionLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "KwhGenerated",
                table: "SolarProductionLogs",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "InstalledCapacityKw",
                table: "SolarProductionLogs",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintenanceAmount",
                table: "SolarProductionLogs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "SolarProductionLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Vendor",
                table: "ProjectExpenditures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ProjectExpenditures",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ProjectExpenditures",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ProjectExpenditures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostType",
                table: "ProjectExpenditures",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "ProjectExpenditures",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingSource",
                table: "ProjectExpenditures",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Item",
                table: "ProjectExpenditures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ProjectExpenditures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ProjectExpenditures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "ProjectExpenditures",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "ProjectExpenditures",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Paid");

            migrationBuilder.AddColumn<decimal>(
                name: "Qty",
                table: "ProjectExpenditures",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Spec",
                table: "ProjectExpenditures",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ProjectExpenditures",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyUntil",
                table: "ProjectExpenditures",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "FixedAssetRegisters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "FixedAssetRegisters",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "FixedAssetRegisters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "FixedAssetRegisters",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ProjectCapitalized",
                table: "FixedAssetRegisters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                table: "FixedAssetRegisters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyUntil",
                table: "FixedAssetRegisters",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FarmProjects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                table: "FarmProjects",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "CapitalizedAssetId",
                table: "FarmProjects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CapitalizedAt",
                table: "FarmProjects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "FarmProjects",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Funding",
                table: "FarmProjects",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ProjectExpenditures"
                SET "PaidAmount" = "Amount",
                    "PaymentStatus" = 'Paid',
                    "CostType" = COALESCE("CostType", "Description")
                WHERE "PaidAmount" = 0;

                UPDATE "FixedAssetRegisters"
                SET "ProjectCapitalized" = TRUE
                WHERE "ProjectId" IS NOT NULL AND "ProjectCapitalized" = FALSE;

                UPDATE "FixedAssetRegisters"
                SET "CategoryName" = CASE "Category"
                    WHEN 0 THEN 'Machinery'
                    WHEN 1 THEN 'Buildings / Sheds'
                    WHEN 2 THEN 'Land'
                    WHEN 3 THEN 'Solar / Power'
                    ELSE 'Other Asset' END
                WHERE "CategoryName" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SolarProductionLogs_Date",
                table: "SolarProductionLogs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SolarProductionLogs_ProjectId",
                table: "SolarProductionLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenditures_AccountId",
                table: "ProjectExpenditures",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenditures_Date",
                table: "ProjectExpenditures",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetRegisters_AccountId",
                table: "FixedAssetRegisters",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssetRegisters_MoneyAccounts_AccountId",
                table: "FixedAssetRegisters",
                column: "AccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectExpenditures_MoneyAccounts_AccountId",
                table: "ProjectExpenditures",
                column: "AccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SolarProductionLogs_FarmProjects_ProjectId",
                table: "SolarProductionLogs",
                column: "ProjectId",
                principalTable: "FarmProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssetRegisters_MoneyAccounts_AccountId",
                table: "FixedAssetRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectExpenditures_MoneyAccounts_AccountId",
                table: "ProjectExpenditures");

            migrationBuilder.DropForeignKey(
                name: "FK_SolarProductionLogs_FarmProjects_ProjectId",
                table: "SolarProductionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SolarProductionLogs_Date",
                table: "SolarProductionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SolarProductionLogs_ProjectId",
                table: "SolarProductionLogs");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenditures_AccountId",
                table: "ProjectExpenditures");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenditures_Date",
                table: "ProjectExpenditures");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetRegisters_AccountId",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "InstalledCapacityKw",
                table: "SolarProductionLogs");

            migrationBuilder.DropColumn(
                name: "MaintenanceAmount",
                table: "SolarProductionLogs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "SolarProductionLogs");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "CostType",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "FundingSource",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Item",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Qty",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Spec",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "WarrantyUntil",
                table: "ProjectExpenditures");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "ProjectCapitalized",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "Supplier",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "WarrantyUntil",
                table: "FixedAssetRegisters");

            migrationBuilder.DropColumn(
                name: "CapitalizedAssetId",
                table: "FarmProjects");

            migrationBuilder.DropColumn(
                name: "CapitalizedAt",
                table: "FarmProjects");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "FarmProjects");

            migrationBuilder.DropColumn(
                name: "Funding",
                table: "FarmProjects");

            migrationBuilder.AlterColumn<decimal>(
                name: "SavingsEstimate",
                table: "SolarProductionLogs",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "SolarProductionLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "KwhGenerated",
                table: "SolarProductionLogs",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Vendor",
                table: "ProjectExpenditures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ProjectExpenditures",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ProjectExpenditures",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FarmProjects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                table: "FarmProjects",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }
    }
}
