using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SlskdUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SlskdApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadsPath = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryPath = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedPath = table.Column<string>(type: "TEXT", nullable: true),
                    SubsonicUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SubsonicUser = table.Column<string>(type: "TEXT", nullable: true),
                    SubsonicPassword = table.Column<string>(type: "TEXT", nullable: true),
                    NtfyUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FanartTvApiKey = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerSettings");
        }
    }
}
