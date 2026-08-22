using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActressLinksAndScrapedVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActressLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActressId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ScrapeHintsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ScraperKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScrapedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActressLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActressLinks_Actresses_ActressId",
                        column: x => x.ActressId,
                        principalTable: "Actresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrapedVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActressLinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActressId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DurationText = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScrapedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExtraJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapedVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapedVideos_ActressLinks_ActressLinkId",
                        column: x => x.ActressLinkId,
                        principalTable: "ActressLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrapedVideos_Actresses_ActressId",
                        column: x => x.ActressId,
                        principalTable: "Actresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActressLinks_ActressId",
                table: "ActressLinks",
                column: "ActressId");

            migrationBuilder.CreateIndex(
                name: "IX_ActressLinks_ActressId_SortOrder",
                table: "ActressLinks",
                columns: new[] { "ActressId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ActressId",
                table: "ScrapedVideos",
                column: "ActressId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ActressLinkId",
                table: "ScrapedVideos",
                column: "ActressLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ActressLinkId_SourceUrl",
                table: "ScrapedVideos",
                columns: new[] { "ActressLinkId", "SourceUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_SourceUrl",
                table: "ScrapedVideos",
                column: "SourceUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapedVideos");

            migrationBuilder.DropTable(
                name: "ActressLinks");
        }
    }
}
