using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DownloadRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DownloadRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArtistId = table.Column<string>(type: "TEXT", nullable: false),
                    ArtistName = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseGroupId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    FirstReleaseDate = table.Column<string>(type: "TEXT", nullable: true),
                    EditionReleaseId = table.Column<string>(type: "TEXT", nullable: true),
                    TrackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Failure = table.Column<string>(type: "TEXT", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Directory = table.Column<string>(type: "TEXT", nullable: true),
                    TracksDone = table.Column<int>(type: "INTEGER", nullable: false),
                    BytesTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    BytesTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    PlaceInQueue = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadRequests_ReleaseGroupId_TrackCount_Quality",
                table: "DownloadRequests",
                columns: new[] { "ReleaseGroupId", "TrackCount", "Quality" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadRequests");
        }
    }
}
