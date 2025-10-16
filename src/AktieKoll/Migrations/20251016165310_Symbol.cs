using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class Symbol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "InsiderTrades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "InsiderTrades",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Isin",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "InsiderTrades");
        }
    }
}
