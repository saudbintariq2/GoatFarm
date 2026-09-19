using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddV41Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ArchivedDate",
                table: "Goats",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedReason",
                table: "Goats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Goats",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StockKgFull",
                table: "FeedPrices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FodderDryKgPerDay",
                table: "FeedPlans",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DeathRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoatId = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgeDays = table.Column<int>(type: "integer", nullable: true),
                    ValueLost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedUsageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    FeedType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    IsStockCheck = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoatWeightRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoatId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoatWeightRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoatWeightRecords_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeathRecords_Date",
                table: "DeathRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_FeedUsageRecords_Date",
                table: "FeedUsageRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_GoatWeightRecords_GoatId_Date",
                table: "GoatWeightRecords",
                columns: new[] { "GoatId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeathRecords");

            migrationBuilder.DropTable(
                name: "FeedUsageRecords");

            migrationBuilder.DropTable(
                name: "GoatWeightRecords");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "ArchivedReason",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "StockKgFull",
                table: "FeedPrices");

            migrationBuilder.DropColumn(
                name: "FodderDryKgPerDay",
                table: "FeedPlans");
        }
    }
}
