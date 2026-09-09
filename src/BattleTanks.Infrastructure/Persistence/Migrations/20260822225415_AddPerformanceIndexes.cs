using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_PlayerId",
                table: "Scores");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_PlayerId_Date",
                table: "Scores",
                columns: new[] { "PlayerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_Points",
                table: "Scores",
                column: "Points");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_TotalScore",
                table: "Players",
                column: "TotalScore");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_RoomCode",
                table: "GameSessions",
                column: "RoomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_PlayerId_Date",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Scores_Points",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Players_Email",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_TotalScore",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_RoomCode",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_PlayerId",
                table: "Scores",
                column: "PlayerId");
        }
    }
}
