using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaFileLastOpenedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastOpenedAt",
                table: "MediaFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOpenedAt",
                table: "MediaFiles");
        }
    }
}
