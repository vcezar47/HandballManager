using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class AddSupercupV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AwayTeamLogo",
                table: "MatchRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CupRound",
                table: "MatchRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeTeamLogo",
                table: "MatchRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCupMatch",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CupGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupName = table.Column<string>(type: "TEXT", nullable: false),
                    Season = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CupGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CupWinnerRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CupWinnerRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupercupFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPlayed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Round = table.Column<string>(type: "TEXT", nullable: false),
                    VenueName = table.Column<string>(type: "TEXT", nullable: true),
                    MatchRecordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupercupFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupercupFixtures_MatchRecords_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "MatchRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupercupFixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupercupFixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupercupWinnerRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupercupWinnerRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CupFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CupGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPlayed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Round = table.Column<string>(type: "TEXT", nullable: false),
                    VenueName = table.Column<string>(type: "TEXT", nullable: true),
                    MatchRecordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CupFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CupFixtures_CupGroups_CupGroupId",
                        column: x => x.CupGroupId,
                        principalTable: "CupGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CupFixtures_MatchRecords_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "MatchRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CupFixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CupFixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CupGroupEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CupGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Played = table.Column<int>(type: "INTEGER", nullable: false),
                    Won = table.Column<int>(type: "INTEGER", nullable: false),
                    Drawn = table.Column<int>(type: "INTEGER", nullable: false),
                    Lost = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CupGroupEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CupGroupEntries_CupGroups_CupGroupId",
                        column: x => x.CupGroupId,
                        principalTable: "CupGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CupGroupEntries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CupFixtures_AwayTeamId",
                table: "CupFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CupFixtures_CupGroupId",
                table: "CupFixtures",
                column: "CupGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CupFixtures_HomeTeamId",
                table: "CupFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CupFixtures_MatchRecordId",
                table: "CupFixtures",
                column: "MatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CupGroupEntries_CupGroupId",
                table: "CupGroupEntries",
                column: "CupGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CupGroupEntries_TeamId",
                table: "CupGroupEntries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SupercupFixtures_AwayTeamId",
                table: "SupercupFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SupercupFixtures_HomeTeamId",
                table: "SupercupFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SupercupFixtures_MatchRecordId",
                table: "SupercupFixtures",
                column: "MatchRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CupFixtures");

            migrationBuilder.DropTable(
                name: "CupGroupEntries");

            migrationBuilder.DropTable(
                name: "CupWinnerRecords");

            migrationBuilder.DropTable(
                name: "SupercupFixtures");

            migrationBuilder.DropTable(
                name: "SupercupWinnerRecords");

            migrationBuilder.DropTable(
                name: "CupGroups");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AwayTeamLogo",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "CupRound",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "HomeTeamLogo",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "IsCupMatch",
                table: "MatchRecords");
        }
    }
}
