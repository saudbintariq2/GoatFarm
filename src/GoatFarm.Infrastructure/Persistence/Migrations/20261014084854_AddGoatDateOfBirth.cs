using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoatDateOfBirth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Goats",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DateOfBirthIsEstimated",
                table: "Goats",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "DateOfBirthIsEstimated",
                table: "Goats");
        }
    }
}
