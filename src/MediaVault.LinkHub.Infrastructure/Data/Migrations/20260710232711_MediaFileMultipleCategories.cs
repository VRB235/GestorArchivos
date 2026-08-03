using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaFileMultipleCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaFileCategories",
                columns: table => new
                {
                    CategoriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaFilesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFileCategories", x => new { x.CategoriesId, x.MediaFilesId });
                    table.ForeignKey(
                        name: "FK_MediaFileCategories_MediaFiles_MediaFilesId",
                        column: x => x.MediaFilesId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaFileCategories_VideoCategories_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "VideoCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO MediaFileCategories (CategoriesId, MediaFilesId)
                SELECT CategoryId, Id
                FROM MediaFiles
                WHERE CategoryId IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFileCategories_MediaFilesId",
                table: "MediaFileCategories",
                column: "MediaFilesId");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_VideoCategories_CategoryId",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_CategoryId",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "MediaFiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFileCategories");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_CategoryId",
                table: "MediaFiles",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_VideoCategories_CategoryId",
                table: "MediaFiles",
                column: "CategoryId",
                principalTable: "VideoCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
