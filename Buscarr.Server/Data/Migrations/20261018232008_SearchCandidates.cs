using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarr.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SearchCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinnedDirectory",
                table: "DownloadRequests",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinnedUsername",
                table: "DownloadRequests",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SearchCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Directory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BitRate = table.Column<int>(type: "INTEGER", nullable: true),
                    HasFreeUploadSlot = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueueLength = table.Column<int>(type: "INTEGER", nullable: false),
                    Rejection = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchCandidates_DownloadRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "DownloadRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchCandidates_RequestId",
                table: "SearchCandidates",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchCandidates");

            migrationBuilder.DropColumn(
                name: "PinnedDirectory",
                table: "DownloadRequests");

            migrationBuilder.DropColumn(
                name: "PinnedUsername",
                table: "DownloadRequests");
        }
    }
}
