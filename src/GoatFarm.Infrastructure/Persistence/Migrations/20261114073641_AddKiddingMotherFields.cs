using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKiddingMotherFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BodyCondition",
                table: "KiddingRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DamHealth",
                table: "KiddingRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DamHealthIssues",
                table: "KiddingRecords",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KeepDecision",
                table: "KiddingRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KeepReason",
                table: "KiddingRecords",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MilkProduction",
                table: "KiddingRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mothering",
                table: "KiddingRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ColostrumGiven",
                table: "Goats",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyCondition",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "DamHealth",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "DamHealthIssues",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "KeepDecision",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "KeepReason",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "MilkProduction",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "Mothering",
                table: "KiddingRecords");

            migrationBuilder.DropColumn(
                name: "ColostrumGiven",
                table: "Goats");
        }
    }
}
