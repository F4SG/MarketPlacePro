using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketPlacePro.Migrations
{
    /// <inheritdoc />
    public partial class ControlDescuentoStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StockDescontado",
                table: "Pagos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockDescontado",
                table: "Pagos");
        }
    }
}
