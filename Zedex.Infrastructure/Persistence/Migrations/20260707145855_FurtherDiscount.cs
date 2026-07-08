using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FurtherDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FurtherDiscount",
                table: "Invoices",
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
                name: "FurtherDiscount",
                table: "Invoices");
        }
    }
}
