using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QualityPreference",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualityPreference",
                table: "Users");
        }
    }
}
