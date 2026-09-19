using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.PersistenceMigrations
{
    /// <inheritdoc />
    public partial class AddLedgerPaymentSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "LedgerEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentSource",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Type_EntryDate",
                table: "LedgerEntries",
                columns: new[] { "Type", "EntryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_Type_EntryDate",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "PaymentSource",
                table: "LedgerEntries");
        }
    }
}
