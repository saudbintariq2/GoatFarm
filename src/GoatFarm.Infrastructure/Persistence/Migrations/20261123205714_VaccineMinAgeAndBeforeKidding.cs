using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VaccineMinAgeAndBeforeKidding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinAgeDays",
                table: "Vaccines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpeningRecord",
                table: "VaccinationHistories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Vaccines" SET "MinAgeDays" = 120 WHERE "Name" IN ('PPR', 'CCPP') AND "MinAgeDays" IS NULL;
                UPDATE "Vaccines" SET "MinAgeDays" = 90 WHERE "Name" = 'FMD' AND "MinAgeDays" IS NULL;
                UPDATE "Vaccines" SET "MinAgeDays" = 60 WHERE "Name" = 'Deworming' AND "MinAgeDays" IS NULL;
                UPDATE "Vaccines"
                SET "RuleType" = 2, "Days" = COALESCE("Days", 30), "Months" = NULL
                WHERE "Name" ILIKE 'ET booster%' AND "RuleType" <> 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinAgeDays",
                table: "Vaccines");

            migrationBuilder.DropColumn(
                name: "IsOpeningRecord",
                table: "VaccinationHistories");
        }
    }
}
