using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebLinkTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite solo permite DEFAULT constante al agregar columnas (ALTER TABLE).
            var seedUtc = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "WebLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: seedUtc);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaActualizacion",
                table: "WebLinks",
                type: "TEXT",
                nullable: false,
                defaultValue: seedUtc);

            migrationBuilder.Sql(
                """
                UPDATE WebLinks
                SET FechaCreacion = strftime('%Y-%m-%d %H:%M:%f', 'now'),
                    FechaUltimaActualizacion = strftime('%Y-%m-%d %H:%M:%f', 'now');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WebLinks_FechaUltimaActualizacion",
                table: "WebLinks",
                column: "FechaUltimaActualizacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebLinks_FechaUltimaActualizacion",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "FechaUltimaActualizacion",
                table: "WebLinks");
        }
    }
}
