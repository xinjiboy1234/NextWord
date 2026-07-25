using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // T-015：本迁移为 SQLite 口味（TEXT/INTEGER 类型），PostgreSQL 上由
            // Patch_PostgreSql_ScoreKernel.sql 幂等建表，直接跳过避免类型分叉。
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.CreateTable(
                name: "ChallengeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PackJson = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    ConfirmationChallenge = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSessions_UserId_CreatedAt",
                table: "ChallengeSessions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // T-015：与 Up 对称，PostgreSQL 上不执行（该表由幂等补丁负责）。
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.DropTable(
                name: "ChallengeSessions");
        }
    }
}
