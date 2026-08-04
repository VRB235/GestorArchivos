using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaVault.LinkHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebLinkProducers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebLinkProducers",
                columns: table => new
                {
                    ProducersId = table.Column<int>(type: "INTEGER", nullable: false),
                    WebLinksId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebLinkProducers", x => new { x.ProducersId, x.WebLinksId });
                    table.ForeignKey(
                        name: "FK_WebLinkProducers_Producers_ProducersId",
                        column: x => x.ProducersId,
                        principalTable: "Producers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebLinkProducers_WebLinks_WebLinksId",
                        column: x => x.WebLinksId,
                        principalTable: "WebLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebLinkProducers_WebLinksId",
                table: "WebLinkProducers",
                column: "WebLinksId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebLinkProducers");
        }
    }
}
