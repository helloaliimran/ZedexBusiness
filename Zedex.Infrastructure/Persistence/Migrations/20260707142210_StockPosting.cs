using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zedex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPosted",
                table: "StockHeaders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PostedBy",
                table: "StockHeaders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedDate",
                table: "StockHeaders",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPosted",
                table: "StockHeaders");

            migrationBuilder.DropColumn(
                name: "PostedBy",
                table: "StockHeaders");

            migrationBuilder.DropColumn(
                name: "PostedDate",
                table: "StockHeaders");
        }
    }
}
