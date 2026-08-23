using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(Zedex.Infrastructure.Persistence.AppDbContext))]
    [Migration("20260816120000_AddPvcGasKitPriceIncludedInRate")]
    /// <inheritdoc />
    public partial class AddPvcGasKitPriceIncludedInRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // When true the product's gas kit charge is already bundled into its
            // rate, so PVC billing must not add a separate gas kit amount for it.
            migrationBuilder.AddColumn<bool>(
                name: "GasKitPriceIncludedInRate",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GasKitPriceIncludedInRate",
                table: "Products");
        }
    }
}