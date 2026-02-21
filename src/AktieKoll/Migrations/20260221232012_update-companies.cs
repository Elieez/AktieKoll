using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class updatecompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Company",
                table: "Company");

            migrationBuilder.RenameTable(
                name: "Company",
                newName: "Companies");

            migrationBuilder.RenameIndex(
                name: "IX_Company_Name",
                table: "Companies",
                newName: "IX_Companies_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Company_Isin",
                table: "Companies",
                newName: "IX_Companies_Isin");

            migrationBuilder.RenameIndex(
                name: "IX_Company_Code",
                table: "Companies",
                newName: "IX_Companies_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Companies",
                table: "Companies",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Companies",
                table: "Companies");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "Company");

            migrationBuilder.RenameIndex(
                name: "IX_Companies_Name",
                table: "Company",
                newName: "IX_Company_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Companies_Isin",
                table: "Company",
                newName: "IX_Company_Isin");

            migrationBuilder.RenameIndex(
                name: "IX_Companies_Code",
                table: "Company",
                newName: "IX_Company_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Company",
                table: "Company",
                column: "Id");
        }
    }
}
