using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoDownloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoDownloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    SegmentsTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    SegmentsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoDownloads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoDownloads_CreatedAt",
                table: "VideoDownloads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VideoDownloads_Status",
                table: "VideoDownloads",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoDownloads");
        }
    }
}
