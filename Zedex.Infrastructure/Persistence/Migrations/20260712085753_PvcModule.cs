using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PvcModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GasKitType",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleType",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerLength",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceType",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PvcInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    LengthFt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaleType = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WeightPerLength = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalWeight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalFeet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LengthsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GasKitType = table.Column<int>(type: "integer", nullable: false),
                    GasKitRatePerFt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GasKitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PvcInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PvcInvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PvcInvoiceItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId",
                table: "Products",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceType",
                table: "Invoices",
                column: "InvoiceType");

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PvcInvoiceItems_InvoiceId",
                table: "PvcInvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PvcInvoiceItems_ProductId",
                table: "PvcInvoiceItems",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "PvcInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GasKitType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WeightPerLength",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "InvoiceType",
                table: "Invoices");
        }
    }
}
