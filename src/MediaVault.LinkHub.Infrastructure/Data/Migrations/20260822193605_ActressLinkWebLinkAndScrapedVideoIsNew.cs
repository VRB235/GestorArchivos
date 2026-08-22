using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActressLinkWebLinkAndScrapedVideoIsNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "ScrapedVideos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WebLinkId",
                table: "ActressLinks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActressLinks_WebLinkId",
                table: "ActressLinks",
                column: "WebLinkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActressLinks_WebLinks_WebLinkId",
                table: "ActressLinks",
                column: "WebLinkId",
                principalTable: "WebLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActressLinks_WebLinks_WebLinkId",
                table: "ActressLinks");

            migrationBuilder.DropIndex(
                name: "IX_ActressLinks_WebLinkId",
                table: "ActressLinks");

            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "ScrapedVideos");

            migrationBuilder.DropColumn(
                name: "WebLinkId",
                table: "ActressLinks");
        }
    }
}
