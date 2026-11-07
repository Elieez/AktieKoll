using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class TradeDetailsAndValueSek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstrumentType",
                table: "InsiderTrades",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCloselyAssociated",
                table: "InsiderTrades",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShareProgram",
                table: "InsiderTrades",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lei",
                table: "InsiderTrades",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradingVenue",
                table: "InsiderTrades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValueSek",
                table: "InsiderTrades",
                type: "numeric(20,2)",
                nullable: true);

            // Existing SEK trades get their value straight away; other currencies are
            // filled in by the FetchTrades backfill mode (needs historical FX rates).
            migrationBuilder.Sql(
                @"UPDATE ""InsiderTrades"" SET ""ValueSek"" = ROUND(""Price"" * ""Shares"", 2) WHERE UPPER(""Currency"") = 'SEK';");

            migrationBuilder.CreateIndex(
                name: "IX_InsiderTrades_PublishingDate",
                table: "InsiderTrades",
                column: "PublishingDate");

            migrationBuilder.CreateIndex(
                name: "IX_InsiderTrades_Symbol_PublishingDate",
                table: "InsiderTrades",
                columns: new[] { "Symbol", "PublishingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InsiderTrades_PublishingDate",
                table: "InsiderTrades");

            migrationBuilder.DropIndex(
                name: "IX_InsiderTrades_Symbol_PublishingDate",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "InstrumentType",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "IsCloselyAssociated",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "IsShareProgram",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "Lei",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "TradingVenue",
                table: "InsiderTrades");

            migrationBuilder.DropColumn(
                name: "ValueSek",
                table: "InsiderTrades");
        }
    }
}
