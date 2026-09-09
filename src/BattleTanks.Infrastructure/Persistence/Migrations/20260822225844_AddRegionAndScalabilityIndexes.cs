using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleTanks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionAndScalabilityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "GameSessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "sa-east-1");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Region",
                table: "GameSessions",
                column: "Region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Region",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "GameSessions");
        }
    }
}
