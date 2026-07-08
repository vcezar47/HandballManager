using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class AddChampionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChampionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChampionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeTeamName = table.Column<string>(type: "TEXT", nullable: false),
                    AwayTeamName = table.Column<string>(type: "TEXT", nullable: false),
                    HomeGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MatchweekNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    Budget = table.Column<decimal>(type: "TEXT", nullable: false),
                    FoundedYear = table.Column<int>(type: "INTEGER", nullable: false),
                    StadiumName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsPlayerTeam = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Minute = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEvents_MatchRecords_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "MatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                    table.PrimaryKey("PK_LeagueEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueEntries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    ShirtNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    Nationality = table.Column<string>(type: "TEXT", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    Dribbling = table.Column<int>(type: "INTEGER", nullable: false),
                    Finishing = table.Column<int>(type: "INTEGER", nullable: false),
                    LongThrows = table.Column<int>(type: "INTEGER", nullable: false),
                    Marking = table.Column<int>(type: "INTEGER", nullable: false),
                    SevenMeterTaking = table.Column<int>(type: "INTEGER", nullable: false),
                    Tackling = table.Column<int>(type: "INTEGER", nullable: false),
                    Technique = table.Column<int>(type: "INTEGER", nullable: false),
                    Receiving = table.Column<int>(type: "INTEGER", nullable: false),
                    Passing = table.Column<int>(type: "INTEGER", nullable: false),
                    AerialReach = table.Column<int>(type: "INTEGER", nullable: false),
                    Communication = table.Column<int>(type: "INTEGER", nullable: false),
                    Eccentricity = table.Column<int>(type: "INTEGER", nullable: false),
                    Handling = table.Column<int>(type: "INTEGER", nullable: false),
                    Throwing = table.Column<int>(type: "INTEGER", nullable: false),
                    OneOnOnes = table.Column<int>(type: "INTEGER", nullable: false),
                    Reflexes = table.Column<int>(type: "INTEGER", nullable: false),
                    Aggression = table.Column<int>(type: "INTEGER", nullable: false),
                    Anticipation = table.Column<int>(type: "INTEGER", nullable: false),
                    Composure = table.Column<int>(type: "INTEGER", nullable: false),
                    Concentration = table.Column<int>(type: "INTEGER", nullable: false),
                    Decisions = table.Column<int>(type: "INTEGER", nullable: false),
                    Determination = table.Column<int>(type: "INTEGER", nullable: false),
                    Flair = table.Column<int>(type: "INTEGER", nullable: false),
                    Leadership = table.Column<int>(type: "INTEGER", nullable: false),
                    OffTheBall = table.Column<int>(type: "INTEGER", nullable: false),
                    Positioning = table.Column<int>(type: "INTEGER", nullable: false),
                    Teamwork = table.Column<int>(type: "INTEGER", nullable: false),
                    Vision = table.Column<int>(type: "INTEGER", nullable: false),
                    IsInjured = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProgressionPhase = table.Column<int>(type: "INTEGER", nullable: false),
                    RecentAttributeChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPhaseCheckMatchweek = table.Column<int>(type: "INTEGER", nullable: false),
                    GrowthAccumulatorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonAttributeChangesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonAssists = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonSaves = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonRatingSum = table.Column<double>(type: "REAL", nullable: false),
                    CareerGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    CareerAssists = table.Column<int>(type: "INTEGER", nullable: false),
                    CareerSaves = table.Column<int>(type: "INTEGER", nullable: false),
                    CareerMatchesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeasonGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeasonAssists = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeasonSaves = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeasonMatchesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeasonAverageRating = table.Column<double>(type: "REAL", nullable: false),
                    Acceleration = table.Column<int>(type: "INTEGER", nullable: false),
                    Agility = table.Column<int>(type: "INTEGER", nullable: false),
                    Balance = table.Column<int>(type: "INTEGER", nullable: false),
                    JumpingReach = table.Column<int>(type: "INTEGER", nullable: false),
                    NaturalFitness = table.Column<int>(type: "INTEGER", nullable: false),
                    Pace = table.Column<int>(type: "INTEGER", nullable: false),
                    Stamina = table.Column<int>(type: "INTEGER", nullable: false),
                    Strength = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayerStats_MatchRecords_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "MatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayerStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueEntries_TeamId",
                table: "LeagueEntries",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchRecordId",
                table: "MatchEvents",
                column: "MatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStats_MatchRecordId",
                table: "MatchPlayerStats",
                column: "MatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStats_PlayerId",
                table: "MatchPlayerStats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChampionRecords");

            migrationBuilder.DropTable(
                name: "LeagueEntries");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "MatchPlayerStats");

            migrationBuilder.DropTable(
                name: "MatchRecords");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
