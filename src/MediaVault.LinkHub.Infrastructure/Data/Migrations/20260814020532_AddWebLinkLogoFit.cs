using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebLinkLogoFit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LogoOffsetX",
                table: "WebLinks",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LogoOffsetY",
                table: "WebLinks",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LogoZoom",
                table: "WebLinks",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoOffsetX",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "LogoOffsetY",
                table: "WebLinks");

            migrationBuilder.DropColumn(
                name: "LogoZoom",
                table: "WebLinks");
        }
    }
}
