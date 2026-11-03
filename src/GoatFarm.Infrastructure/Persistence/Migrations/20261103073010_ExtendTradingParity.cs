using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendTradingParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Place",
                table: "TradeStores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TradeItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillNo",
                table: "TradeEntries",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "TradeEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "TradeEntries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Freight",
                table: "TradeEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Labour",
                table: "TradeEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LossReason",
                table: "TradeEntries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "TradeEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "TradeEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedAmount",
                table: "TradeEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ToAccountId",
                table: "TradeEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TradeAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeEntries_ToAccountId",
                table: "TradeEntries",
                column: "ToAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeEntries_TradeAccounts_ToAccountId",
                table: "TradeEntries",
                column: "ToAccountId",
                principalTable: "TradeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeEntries_TradeAccounts_ToAccountId",
                table: "TradeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TradeEntries_ToAccountId",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "Place",
                table: "TradeStores");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TradeItems");

            migrationBuilder.DropColumn(
                name: "BillNo",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "Freight",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "Labour",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "LossReason",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "ReceivedAmount",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "TradeEntries");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TradeAccounts");
        }
    }
}
