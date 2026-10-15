using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHerdQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Goats_IsArchived_EventDate",
                table: "Goats",
                columns: new[] { "IsArchived", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Goats_IsArchived_HealthState",
                table: "Goats",
                columns: new[] { "IsArchived", "HealthState" });

            migrationBuilder.CreateIndex(
                name: "IX_Goats_IsArchived_MatedDate",
                table: "Goats",
                columns: new[] { "IsArchived", "MatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Goats_IsArchived_Status",
                table: "Goats",
                columns: new[] { "IsArchived", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Goats_IsArchived_EventDate",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_IsArchived_HealthState",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_IsArchived_MatedDate",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_IsArchived_Status",
                table: "Goats");
        }
    }
}
