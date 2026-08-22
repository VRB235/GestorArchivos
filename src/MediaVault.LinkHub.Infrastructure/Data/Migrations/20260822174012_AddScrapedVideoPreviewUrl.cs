using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapedVideoPreviewUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewUrl",
                table: "ScrapedVideos",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewUrl",
                table: "ScrapedVideos");
        }
    }
}
