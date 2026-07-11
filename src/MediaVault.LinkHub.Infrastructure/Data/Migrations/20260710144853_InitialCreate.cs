using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    VecesAbierto = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RankingCalidad = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    RankingContenido = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    RankingGusto = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuickNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Contenido = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_Extension",
                table: "MediaFiles",
                column: "Extension");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_Path",
                table: "MediaFiles",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_VecesAbierto",
                table: "MediaFiles",
                column: "VecesAbierto");

            migrationBuilder.CreateIndex(
                name: "IX_QuickNotes_FechaCreacion",
                table: "QuickNotes",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_WebLinks_Categoria",
                table: "WebLinks",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_WebLinks_Url",
                table: "WebLinks",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "QuickNotes");

            migrationBuilder.DropTable(
                name: "WebLinks");
        }
    }
}
