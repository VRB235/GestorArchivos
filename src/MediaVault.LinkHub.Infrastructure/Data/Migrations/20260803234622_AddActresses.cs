using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFileActresses",
                columns: table => new
                {
                    ActressesId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaFilesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFileActresses", x => new { x.ActressesId, x.MediaFilesId });
                    table.ForeignKey(
                        name: "FK_MediaFileActresses_Actresses_ActressesId",
                        column: x => x.ActressesId,
                        principalTable: "Actresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaFileActresses_MediaFiles_MediaFilesId",
                        column: x => x.MediaFilesId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actresses_Name",
                table: "Actresses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actresses_SortOrder",
                table: "Actresses",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFileActresses_MediaFilesId",
                table: "MediaFileActresses",
                column: "MediaFilesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFileActresses");

            migrationBuilder.DropTable(
                name: "Actresses");
        }
    }
}
