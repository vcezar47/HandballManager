using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfersNewsYouthRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NewsType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
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
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TransferOffers");
            migrationBuilder.DropTable(name: "PendingTransfers");
            migrationBuilder.DropTable(name: "NewsItems");
            migrationBuilder.DropTable(name: "YouthIntakePlayers");

            migrationBuilder.DropColumn(name: "IsRetired", table: "Players");
            migrationBuilder.DropColumn(name: "IsRetiringAtEndOfSeason", table: "Players");
        }
    }
}
