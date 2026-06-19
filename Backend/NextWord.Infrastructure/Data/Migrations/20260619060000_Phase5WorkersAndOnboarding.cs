using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5WorkersAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUpgradeCandidate",
                table: "UserProgress",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PendingReviewCount",
                table: "UserProgress",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsUpgradeCandidate", table: "UserProgress");
            migrationBuilder.DropColumn(name: "PendingReviewCount", table: "UserProgress");
        }
    }
}
