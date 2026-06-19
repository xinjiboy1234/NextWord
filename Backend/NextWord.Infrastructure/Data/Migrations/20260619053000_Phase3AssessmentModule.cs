using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3AssessmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedInitialAssessment",
                table: "UserProgress",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLevelLocked",
                table: "UserProgress",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LevelStartDate",
                table: "UserProgress",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinalLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChallengeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    VocabularyScore = table.Column<double>(type: "REAL", nullable: false),
                    SentenceScore = table.Column<double>(type: "REAL", nullable: false),
                    ReadingScore = table.Column<double>(type: "REAL", nullable: false),
                    TotalScore = table.Column<double>(type: "REAL", nullable: false),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttemptedLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevelHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    ToLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevelHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Step = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    QuestionType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    QuestionsJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    AnswersJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ScoresJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentRecords_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecords_AssessmentId",
                table: "AssessmentRecords",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_UserId",
                table: "Assessments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeRecords_UserId",
                table: "ChallengeRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelHistories_UserId",
                table: "LevelHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AssessmentRecords");
            migrationBuilder.DropTable(name: "ChallengeRecords");
            migrationBuilder.DropTable(name: "LevelHistories");
            migrationBuilder.DropTable(name: "Assessments");

            migrationBuilder.DropColumn(name: "HasCompletedInitialAssessment", table: "UserProgress");
            migrationBuilder.DropColumn(name: "IsLevelLocked", table: "UserProgress");
            migrationBuilder.DropColumn(name: "LevelStartDate", table: "UserProgress");
        }
    }
}
