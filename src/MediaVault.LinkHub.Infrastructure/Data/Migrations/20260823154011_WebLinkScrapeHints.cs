using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WebLinkScrapeHints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScrapeHintsJson",
                table: "WebLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScraperKey",
                table: "WebLinks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            // Copia hints existentes desde ActressLinks hacia el WebLink (sitio/productora).
            migrationBuilder.Sql(
                """
                UPDATE WebLinks
                SET
                    ScrapeHintsJson = (
                        SELECT al.ScrapeHintsJson
                        FROM ActressLinks AS al
                        WHERE al.WebLinkId = WebLinks.Id
                          AND al.ScrapeHintsJson IS NOT NULL
                          AND length(trim(al.ScrapeHintsJson)) > 0
                        ORDER BY COALESCE(al.LastScrapedAt, al.CreatedAt) DESC, al.Id DESC
                        LIMIT 1
                    ),
                    ScraperKey = (
                        SELECT al.ScraperKey
                        FROM ActressLinks AS al
                        WHERE al.WebLinkId = WebLinks.Id
                          AND al.ScraperKey IS NOT NULL
                          AND length(trim(al.ScraperKey)) > 0
                        ORDER BY COALESCE(al.LastScrapedAt, al.CreatedAt) DESC, al.Id DESC
                        LIMIT 1
                    )
                WHERE EXISTS (
                    SELECT 1
                    FROM ActressLinks AS al
                    WHERE al.WebLinkId = WebLinks.Id
                      AND al.ScrapeHintsJson IS NOT NULL
                      AND length(trim(al.ScrapeHintsJson)) > 0
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScrapeHintsJson",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "ScraperKey",
                table: "WebLinks");
        }
    }
}
