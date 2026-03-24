using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AktieKoll.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPublishingDateToTimestamptz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""InsiderTrades"" ALTER COLUMN ""PublishingDate"" TYPE timestamp with time zone USING ""PublishingDate"" AT TIME ZONE 'UTC'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""InsiderTrades"" ALTER COLUMN ""PublishingDate"" TYPE timestamp without time zone USING ""PublishingDate"" AT TIME ZONE 'UTC'");
        }
    }
}
