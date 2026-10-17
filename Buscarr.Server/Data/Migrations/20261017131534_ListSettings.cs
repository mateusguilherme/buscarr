using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ListSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "ListedArtists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReleaseTypes",
                table: "ListedArtists",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Edition",
                table: "ListedArtists");

            migrationBuilder.DropColumn(
                name: "ReleaseTypes",
                table: "ListedArtists");
        }
    }
}
