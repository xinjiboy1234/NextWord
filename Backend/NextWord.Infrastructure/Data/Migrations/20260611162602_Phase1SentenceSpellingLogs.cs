using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1SentenceSpellingLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FreeExpressionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AiScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallGrade = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    AiRevision = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ErrorSentences = table.Column<string>(type: "TEXT", nullable: false),
                    Suggestions = table.Column<string>(type: "TEXT", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeExpressionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreeExpressionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SentenceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetWord = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Scene = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    UserSentence = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AiRevision = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    GrammarScore = table.Column<int>(type: "INTEGER", nullable: false),
                    NaturalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    VocabularyScore = table.Column<int>(type: "INTEGER", nullable: false),
                    RelevanceScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallGrade = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    ErrorTags = table.Column<string>(type: "TEXT", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Suggestion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentenceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SentenceLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SentenceLogs_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sentences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    TargetWord = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Scene = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AnnotationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sentences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sentences_DifficultyAnnotations_AnnotationId",
                        column: x => x.AnnotationId,
                        principalTable: "DifficultyAnnotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sentences_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SpellingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserSpelling = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CorrectSpelling = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorPositions = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpellingLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpellingLogs_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreeExpressionLogs_UserId",
                table: "FreeExpressionLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SentenceLogs_UserId",
                table: "SentenceLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SentenceLogs_WordId",
                table: "SentenceLogs",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_Sentences_AnnotationId",
                table: "Sentences",
                column: "AnnotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sentences_WordId",
                table: "Sentences",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_SpellingLogs_UserId",
                table: "SpellingLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpellingLogs_WordId",
                table: "SpellingLogs",
                column: "WordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreeExpressionLogs");

            migrationBuilder.DropTable(
                name: "SentenceLogs");

            migrationBuilder.DropTable(
                name: "Sentences");

            migrationBuilder.DropTable(
                name: "SpellingLogs");
        }
    }
}
