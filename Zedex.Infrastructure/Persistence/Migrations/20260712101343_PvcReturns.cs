using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PvcReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "InvoiceItemId",
                table: "SaleReturnItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PvcInvoiceItemId",
                table: "SaleReturnItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_PvcInvoiceItemId",
                table: "SaleReturnItems",
                column: "PvcInvoiceItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleReturnItems_PvcInvoiceItems_PvcInvoiceItemId",
                table: "SaleReturnItems",
                column: "PvcInvoiceItemId",
                principalTable: "PvcInvoiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleReturnItems_PvcInvoiceItems_PvcInvoiceItemId",
                table: "SaleReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleReturnItems_PvcInvoiceItemId",
                table: "SaleReturnItems");

            migrationBuilder.DropColumn(
                name: "PvcInvoiceItemId",
                table: "SaleReturnItems");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceItemId",
                table: "SaleReturnItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
