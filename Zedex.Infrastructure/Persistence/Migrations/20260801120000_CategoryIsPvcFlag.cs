using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryIsPvcFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPvc",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Carry forward the old hardcoded "PVC" category name as the flagged category,
            // so existing installs keep working without an admin having to re-check it.
            migrationBuilder.Sql(
                "UPDATE \"Categories\" SET \"IsPvc\" = TRUE WHERE \"Name\" = 'PVC';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPvc",
                table: "Categories");
        }
    }
}
