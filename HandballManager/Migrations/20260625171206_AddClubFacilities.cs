using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class AddClubFacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubReputation",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StadiumCapacity",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StadiumImage",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TrainingFacilityLevel",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrainingFacilityUpgradeCompleteDate",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YouthFacilityLevel",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "YouthFacilityUpgradeCompleteDate",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "SupercupWinnerRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "SupercupWinnerRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayPenaltyGoals",
                table: "SupercupFixtures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "SupercupFixtures",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HomePenaltyGoals",
                table: "SupercupFixtures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MatchEnergy",
                table: "Players",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Attendance",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayPenaltyGoals",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomePenaltyGoals",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnplayedPlaceholder",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LeagueSubtitle",
                table: "MatchRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "MatchRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "WasDecidedByOvertime",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasDecidedByShootout",
                table: "MatchRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MatchEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Second",
                table: "MatchEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "LeagueEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "CupWinnerRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "CupWinnerRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "CupGroups",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AwayPenaltyGoals",
                table: "CupFixtures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "CupFixtures",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HomePenaltyGoals",
                table: "CupFixtures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompetitionName",
                table: "ChampionRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RunnerUpTeamName",
                table: "ChampionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "ChampionRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPlaceTeamName",
                table: "ChampionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeagueFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPlayed = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompetitionName = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    PlayoffSeriesId = table.Column<string>(type: "TEXT", nullable: true),
                    PlayoffLeg = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueFixtures_MatchRecords_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "MatchRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueFixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueFixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Managers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Birthdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "TEXT", nullable: false),
                    Nationality = table.Column<string>(type: "TEXT", nullable: false),
                    License = table.Column<int>(type: "INTEGER", nullable: false),
                    Reputation = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivation = table.Column<int>(type: "INTEGER", nullable: false),
                    YouthDevelopment = table.Column<int>(type: "INTEGER", nullable: false),
                    Discipline = table.Column<int>(type: "INTEGER", nullable: false),
                    Adaptability = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeoutTalks = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWon = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesDrawn = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesLost = table.Column<int>(type: "INTEGER", nullable: false),
                    TrophiesWon = table.Column<int>(type: "INTEGER", nullable: false),
                    ClubHistoryJson = table.Column<string>(type: "TEXT", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsPlayerManager = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Managers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Managers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueFixtures_AwayTeamId",
                table: "LeagueFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueFixtures_HomeTeamId",
                table: "LeagueFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueFixtures_MatchRecordId",
                table: "LeagueFixtures",
                column: "MatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Managers_TeamId",
                table: "Managers",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TeamId",
                table: "Transactions",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueFixtures");

            migrationBuilder.DropTable(
                name: "Managers");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropColumn(
                name: "ClubReputation",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "StadiumCapacity",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "StadiumImage",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TrainingFacilityLevel",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TrainingFacilityUpgradeCompleteDate",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "YouthFacilityLevel",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "YouthFacilityUpgradeCompleteDate",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "SupercupWinnerRecords");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "SupercupWinnerRecords");

            migrationBuilder.DropColumn(
                name: "AwayPenaltyGoals",
                table: "SupercupFixtures");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "SupercupFixtures");

            migrationBuilder.DropColumn(
                name: "HomePenaltyGoals",
                table: "SupercupFixtures");

            migrationBuilder.DropColumn(
                name: "MatchEnergy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Attendance",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "AwayPenaltyGoals",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "HomePenaltyGoals",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "IsUnplayedPlaceholder",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "LeagueSubtitle",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "WasDecidedByOvertime",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "WasDecidedByShootout",
                table: "MatchRecords");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MatchEvents");

            migrationBuilder.DropColumn(
                name: "Second",
                table: "MatchEvents");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "LeagueEntries");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "CupWinnerRecords");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "CupWinnerRecords");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "CupGroups");

            migrationBuilder.DropColumn(
                name: "AwayPenaltyGoals",
                table: "CupFixtures");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "CupFixtures");

            migrationBuilder.DropColumn(
                name: "HomePenaltyGoals",
                table: "CupFixtures");

            migrationBuilder.DropColumn(
                name: "CompetitionName",
                table: "ChampionRecords");

            migrationBuilder.DropColumn(
                name: "RunnerUpTeamName",
                table: "ChampionRecords");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "ChampionRecords");

            migrationBuilder.DropColumn(
                name: "ThirdPlaceTeamName",
                table: "ChampionRecords");
        }
    }
}
