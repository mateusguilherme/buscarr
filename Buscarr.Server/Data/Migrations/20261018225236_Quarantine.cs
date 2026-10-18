using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Quarantine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Directory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailedSources_DownloadRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "DownloadRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuarantinedAlbums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArtistName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuarantinedAlbums_DownloadRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "DownloadRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedSources_RequestId_Username_Directory",
                table: "FailedSources",
                columns: new[] { "RequestId", "Username", "Directory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedAlbums_RequestId",
                table: "QuarantinedAlbums",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedSources");

            migrationBuilder.DropTable(
                name: "QuarantinedAlbums");
        }
    }
}
