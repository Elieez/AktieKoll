using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class AddShortPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShortIssuers",
                columns: table => new
                {
                    Lei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimarySymbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GleifCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortIssuers", x => x.Lei);
                });

            migrationBuilder.CreateTable(
                name: "ShortIssuerSymbols",
                columns: table => new
                {
                    Lei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortIssuerSymbols", x => new { x.Lei, x.Symbol });
                });

            migrationBuilder.CreateTable(
                name: "ShortPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Lei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HolderName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PositionPercent = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    BelowThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    PositionDate = table.Column<DateTime>(type: "date", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShortSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Lei = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "date", nullable: false),
                    PositionPercent = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    LatestPositionDate = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShortSnapshots_ShortIssuers_Lei",
                        column: x => x.Lei,
                        principalTable: "ShortIssuers",
                        principalColumn: "Lei",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShortIssuerSymbols_Symbol",
                table: "ShortIssuerSymbols",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_ShortPositions_HolderName_Isin_PositionDate",
                table: "ShortPositions",
                columns: new[] { "HolderName", "Isin", "PositionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortPositions_Lei",
                table: "ShortPositions",
                column: "Lei");

            migrationBuilder.CreateIndex(
                name: "IX_ShortSnapshots_Lei_SnapshotDate",
                table: "ShortSnapshots",
                columns: new[] { "Lei", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortSnapshots_SnapshotDate",
                table: "ShortSnapshots",
                column: "SnapshotDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShortIssuerSymbols");

            migrationBuilder.DropTable(
                name: "ShortPositions");

            migrationBuilder.DropTable(
                name: "ShortSnapshots");

            migrationBuilder.DropTable(
                name: "ShortIssuers");
        }
    }
}
