using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class Refactordb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "InsiderTrades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InsiderTrades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishingDate",
                table: "InsiderTrades",
                type: "date",
                nullable: false,
                defaultValueSql: "CURRENT_DATE");

            migrationBuilder.AddColumn<DateTime>(
                name: "TransactionDate",
                table: "InsiderTrades",
                type: "date",
                nullable: false,
                defaultValueSql: "CURRENT_DATE");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
               name: "Currency",
               table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "PublishingDate",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "TransactionDate",
                table: "InsiderTrades");
        }
    }
}
