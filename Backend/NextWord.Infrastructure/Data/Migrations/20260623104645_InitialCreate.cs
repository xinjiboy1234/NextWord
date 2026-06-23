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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ItemHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifficultyAnnotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Lemma = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Phonetics = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Meanings = table.Column<string>(type: "text", nullable: false),
                    ExampleSentences = table.Column<string>(type: "text", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    LlmAnnotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCore = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AnnotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TopicTag = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_DifficultyAnnotations_AnnotationId",
                        column: x => x.AnnotationId,
                        principalTable: "DifficultyAnnotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VocabularyScore = table.Column<double>(type: "double precision", nullable: false),
                    SentenceScore = table.Column<double>(type: "double precision", nullable: false),
                    ReadingScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptedLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "FreeExpressionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AiScore = table.Column<int>(type: "integer", nullable: false),
                    OverallGrade = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    AiRevision = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ErrorSentences = table.Column<string>(type: "text", nullable: false),
                    Suggestions = table.Column<string>(type: "text", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "LevelHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ToLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "UserProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    VocabLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SpellingLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SentenceLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReadingLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    StreakDays = table.Column<int>(type: "integer", nullable: false),
                    LastStudyDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LevelStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsLevelLocked = table.Column<bool>(type: "boolean", nullable: false),
                    HasCompletedInitialAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    PendingReviewCount = table.Column<int>(type: "integer", nullable: false),
                    IsUpgradeCandidate = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "SentenceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetWord = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Scene = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UserSentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AiRevision = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    GrammarScore = table.Column<int>(type: "integer", nullable: false),
                    NaturalScore = table.Column<int>(type: "integer", nullable: false),
                    VocabularyScore = table.Column<int>(type: "integer", nullable: false),
                    RelevanceScore = table.Column<int>(type: "integer", nullable: false),
                    OverallGrade = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ErrorTags = table.Column<string>(type: "text", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Suggestion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    TargetWord = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Scene = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AnnotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSpelling = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CorrectSpelling = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorPositions = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "UserWordRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    MasteryScore = table.Column<double>(type: "double precision", nullable: false),
                    TimesLearned = table.Column<int>(type: "integer", nullable: false),
                    TimesCorrect = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    RepeatCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    LastReviewDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextReviewDue = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ArticleComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParagraphIndex = table.Column<int>(type: "integer", nullable: false),
                    ParagraphText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CommentText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AiReply = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleComments_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleVocabMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: true),
                    WordLemma = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContextMeaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SpecialUsage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DifficultyInContext = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsKeyVocab = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleVocabMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleVocabMappings_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleVocabMappings_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReadingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    LookupCount = table.Column<int>(type: "integer", nullable: false),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingLogs_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuestionsJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    AnswersJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ScoresJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_ArticleComments_ArticleId",
                table: "ArticleComments",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleComments_UserId",
                table: "ArticleComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_AnnotationId",
                table: "Articles",
                column: "AnnotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleVocabMappings_ArticleId_WordLemma",
                table: "ArticleVocabMappings",
                columns: new[] { "ArticleId", "WordLemma" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleVocabMappings_WordId",
                table: "ArticleVocabMappings",
                column: "WordId");

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
                name: "IX_DifficultyAnnotations_ItemType_ItemHash",
                table: "DifficultyAnnotations",
                columns: new[] { "ItemType", "ItemHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeExpressionLogs_UserId",
                table: "FreeExpressionLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelHistories_UserId",
                table: "LevelHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingLogs_ArticleId",
                table: "ReadingLogs",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingLogs_UserId",
                table: "ReadingLogs",
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
                name: "ArticleComments");

            migrationBuilder.DropTable(
                name: "ArticleVocabMappings");

            migrationBuilder.DropTable(
                name: "AssessmentRecords");

            migrationBuilder.DropTable(
                name: "ChallengeRecords");

            migrationBuilder.DropTable(
                name: "FreeExpressionLogs");

            migrationBuilder.DropTable(
                name: "LevelHistories");

            migrationBuilder.DropTable(
                name: "ReadingLogs");

            migrationBuilder.DropTable(
                name: "SentenceLogs");

            migrationBuilder.DropTable(
                name: "Sentences");

            migrationBuilder.DropTable(
                name: "SpellingLogs");

            migrationBuilder.DropTable(
                name: "UserProgress");

            migrationBuilder.DropTable(
                name: "UserWordRelationships");

            migrationBuilder.DropTable(
                name: "WordDifficultyAnnotations");

            migrationBuilder.DropTable(
                name: "WordLearningLogs");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Words");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "DifficultyAnnotations");
        }
    }
}
