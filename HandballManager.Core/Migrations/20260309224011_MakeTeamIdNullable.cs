using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class MakeTeamIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Players",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetiringAtEndOfSeason",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransferredThisSeason",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NewsType = table.Column<string>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgreedMonthlyWage = table.Column<decimal>(type: "TEXT", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransferType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingTransfers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingTransfers_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingTransfers_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    ForPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    OfferType = table.Column<string>(type: "TEXT", nullable: false),
                    OfferAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProposedMonthlyWage = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProposedContractYears = table.Column<int>(type: "INTEGER", nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Players_ForPlayerId",
                        column: x => x.ForPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YouthIntakePlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClubId = table.Column<int>(type: "INTEGER", nullable: false),
                    IntakeYear = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Birthdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    Nationality = table.Column<string>(type: "TEXT", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedShirtNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YouthIntakePlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YouthIntakePlayers_Teams_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransfers_FromTeamId",
                table: "PendingTransfers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransfers_PlayerId",
                table: "PendingTransfers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTransfers_ToTeamId",
                table: "PendingTransfers",
                column: "ToTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_ForPlayerId",
                table: "TransferOffers",
                column: "ForPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_FromTeamId",
                table: "TransferOffers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_YouthIntakePlayers_ClubId",
                table: "YouthIntakePlayers",
                column: "ClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "PendingTransfers");

            migrationBuilder.DropTable(
                name: "TransferOffers");

            migrationBuilder.DropTable(
                name: "YouthIntakePlayers");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsRetiringAtEndOfSeason",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TransferredThisSeason",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
