using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProducerLinksAndScrapedVideoProducer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ActressLinkId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ActressId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "ProducerId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProducerLinkId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProducerLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProducerId = table.Column<int>(type: "INTEGER", nullable: false),
                    WebLinkId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScrapedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProducerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProducerLinks_Producers_ProducerId",
                        column: x => x.ProducerId,
                        principalTable: "Producers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProducerLinks_WebLinks_WebLinkId",
                        column: x => x.WebLinkId,
                        principalTable: "WebLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ProducerId",
                table: "ScrapedVideos",
                column: "ProducerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ProducerLinkId",
                table: "ScrapedVideos",
                column: "ProducerLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedVideos_ProducerLinkId_SourceUrl",
                table: "ScrapedVideos",
                columns: new[] { "ProducerLinkId", "SourceUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_ProducerLinks_ProducerId",
                table: "ProducerLinks",
                column: "ProducerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProducerLinks_ProducerId_SortOrder",
                table: "ProducerLinks",
                columns: new[] { "ProducerId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProducerLinks_WebLinkId",
                table: "ProducerLinks",
                column: "WebLinkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapedVideos_ProducerLinks_ProducerLinkId",
                table: "ScrapedVideos",
                column: "ProducerLinkId",
                principalTable: "ProducerLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapedVideos_Producers_ProducerId",
                table: "ScrapedVideos",
                column: "ProducerId",
                principalTable: "Producers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrapedVideos_ProducerLinks_ProducerLinkId",
                table: "ScrapedVideos");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrapedVideos_Producers_ProducerId",
                table: "ScrapedVideos");

            migrationBuilder.DropTable(
                name: "ProducerLinks");

            migrationBuilder.DropIndex(
                name: "IX_ScrapedVideos_ProducerId",
                table: "ScrapedVideos");

            migrationBuilder.DropIndex(
                name: "IX_ScrapedVideos_ProducerLinkId",
                table: "ScrapedVideos");

            migrationBuilder.DropIndex(
                name: "IX_ScrapedVideos_ProducerLinkId_SourceUrl",
                table: "ScrapedVideos");

            migrationBuilder.DropColumn(
                name: "ProducerId",
                table: "ScrapedVideos");

            migrationBuilder.DropColumn(
                name: "ProducerLinkId",
                table: "ScrapedVideos");

            migrationBuilder.AlterColumn<int>(
                name: "ActressLinkId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActressId",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
