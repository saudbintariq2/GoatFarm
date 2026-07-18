using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddV19Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuckTag",
                table: "Goats",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KidsCount",
                table: "Goats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MatedDate",
                table: "Goats",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrepCrossDate",
                table: "Goats",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UltrasoundDate",
                table: "Goats",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockKg",
                table: "FeedPrices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuckTag",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "KidsCount",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "MatedDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "PrepCrossDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "UltrasoundDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "StockKg",
                table: "FeedPrices");
        }
    }
}
