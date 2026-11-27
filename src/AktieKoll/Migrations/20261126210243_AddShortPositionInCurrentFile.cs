using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class AddShortPositionInCurrentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InCurrentFile",
                table: "ShortPositions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InCurrentFile",
                table: "ShortPositions");
        }
    }
}
