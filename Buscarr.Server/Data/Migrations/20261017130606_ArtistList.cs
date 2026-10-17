using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArtistList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListedArtists",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtistId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Disambiguation = table.Column<string>(type: "TEXT", nullable: true),
                    Area = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListedArtists", x => new { x.UserId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_ListedArtists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListedArtists");
        }
    }
}
