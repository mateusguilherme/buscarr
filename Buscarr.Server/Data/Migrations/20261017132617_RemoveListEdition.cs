using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveListEdition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Edition",
                table: "ListedArtists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "ListedArtists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
