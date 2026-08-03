using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_CategoryId",
                table: "MediaFiles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoCategories_Name",
                table: "VideoCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoCategories_SortOrder",
                table: "VideoCategories",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_VideoCategories_CategoryId",
                table: "MediaFiles",
                column: "CategoryId",
                principalTable: "VideoCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_VideoCategories_CategoryId",
                table: "MediaFiles");

            migrationBuilder.DropTable(
                name: "VideoCategories");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_CategoryId",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "MediaFiles");
        }
    }
}
