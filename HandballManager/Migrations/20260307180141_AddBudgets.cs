using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandballManager.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "Budget",
                table: "Teams",
                newName: "WageBudget");

            migrationBuilder.AddColumn<decimal>(
                name: "TransferBudget",
                table: "Teams",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "Birthdate",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyWage",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransferBudget",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Birthdate",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MonthlyWage",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "WageBudget",
                table: "Teams",
                newName: "Budget");

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
