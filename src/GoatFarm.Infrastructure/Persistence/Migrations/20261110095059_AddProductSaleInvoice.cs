using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSaleInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNo",
                table: "ProductSales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "ProductSales",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSales_WarehouseId",
                table: "ProductSales",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSales_Warehouses_WarehouseId",
                table: "ProductSales",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductSales_Warehouses_WarehouseId",
                table: "ProductSales");

            migrationBuilder.DropIndex(
                name: "IX_ProductSales_WarehouseId",
                table: "ProductSales");

            migrationBuilder.DropColumn(
                name: "InvoiceNo",
                table: "ProductSales");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ProductSales");
        }
    }
}
