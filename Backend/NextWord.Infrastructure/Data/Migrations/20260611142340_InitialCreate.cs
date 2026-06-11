using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DifficultyAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ItemHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyAnnotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Lemma = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Phonetics = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Meanings = table.Column<string>(type: "TEXT", nullable: false),
                    ExampleSentences = table.Column<string>(type: "TEXT", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    LlmAnnotationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsCore = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OverallLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    VocabLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SpellingLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SentenceLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    ReadingLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StreakDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LastStudyDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWordRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MasteryScore = table.Column<double>(type: "REAL", nullable: false),
                    TimesLearned = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesCorrect = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EaseFactor = table.Column<double>(type: "REAL", nullable: false),
                    RepeatCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastReviewDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextReviewDue = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWordRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWordRelationships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWordRelationships_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WordDifficultyAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordDifficultyAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordDifficultyAnnotations_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WordLearningLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    Rating = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordLearningLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordLearningLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WordLearningLogs_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DifficultyAnnotations_ItemType_ItemHash",
                table: "DifficultyAnnotations",
                columns: new[] { "ItemType", "ItemHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProgress_UserId",
                table: "UserProgress",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWordRelationships_UserId_WordId",
                table: "UserWordRelationships",
                columns: new[] { "UserId", "WordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWordRelationships_WordId",
                table: "UserWordRelationships",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_WordDifficultyAnnotations_WordId",
                table: "WordDifficultyAnnotations",
                column: "WordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordLearningLogs_UserId",
                table: "WordLearningLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WordLearningLogs_WordId",
                table: "WordLearningLogs",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_Words_Lemma",
                table: "Words",
                column: "Lemma",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DifficultyAnnotations");

            migrationBuilder.DropTable(
                name: "UserProgress");

            migrationBuilder.DropTable(
                name: "UserWordRelationships");

            migrationBuilder.DropTable(
                name: "WordDifficultyAnnotations");

            migrationBuilder.DropTable(
                name: "WordLearningLogs");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Words");
        }
    }
}
