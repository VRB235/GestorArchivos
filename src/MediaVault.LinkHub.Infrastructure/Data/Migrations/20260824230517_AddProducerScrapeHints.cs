using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProducerScrapeHints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProducerScrapeHintsJson",
                table: "WebLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProducerScraperKey",
                table: "WebLinks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProducerScrapeHintsJson",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "ProducerScraperKey",
                table: "WebLinks");
        }
    }
}
