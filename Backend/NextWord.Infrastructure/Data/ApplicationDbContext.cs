using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NextWord.Domain.Entities;
using System.Text.Json;

namespace NextWord.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        value => value.ToList());
    private static readonly ValueComparer<List<int>> IntListComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        value => value.ToList());

    public DbSet<User> Users => Set<User>();
    public DbSet<UserLlmSettings> UserLlmSettings => Set<UserLlmSettings>();
    public DbSet<Word> Words => Set<Word>();
    public DbSet<WordDifficultyAnnotation> WordDifficultyAnnotations => Set<WordDifficultyAnnotation>();
    public DbSet<DifficultyAnnotation> DifficultyAnnotations => Set<DifficultyAnnotation>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<UserWordRelationship> UserWordRelationships => Set<UserWordRelationship>();
    public DbSet<WordLearningLog> WordLearningLogs => Set<WordLearningLog>();
    public DbSet<Sentence> Sentences => Set<Sentence>();
    public DbSet<SentenceLog> SentenceLogs => Set<SentenceLog>();
    public DbSet<FreeExpressionLog> FreeExpressionLogs => Set<FreeExpressionLog>();
    public DbSet<SpellingLog> SpellingLogs => Set<SpellingLog>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ReadingLog> ReadingLogs => Set<ReadingLog>();
    public DbSet<ArticleComment> ArticleComments => Set<ArticleComment>();
    public DbSet<ArticleVocabMapping> ArticleVocabMappings => Set<ArticleVocabMapping>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentRecord> AssessmentRecords => Set<AssessmentRecord>();
    public DbSet<ChallengeRecord> ChallengeRecords => Set<ChallengeRecord>();
    public DbSet<LevelHistory> LevelHistories => Set<LevelHistory>();
    public DbSet<LearningEvent> LearningEvents => Set<LearningEvent>();
    public DbSet<ProfileScoreSnapshot> ProfileScoreSnapshots => Set<ProfileScoreSnapshot>();
    public DbSet<EvaluationReport> EvaluationReports => Set<EvaluationReport>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<UserFeedback> UserFeedbacks => Set<UserFeedback>();
    public DbSet<UserWordExclude> UserWordExcludes => Set<UserWordExclude>();
    public DbSet<ChallengeSession> ChallengeSessions => Set<ChallengeSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(120);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).HasMaxLength(256);
            entity.HasOne(user => user.LlmSettings)
                .WithOne(settings => settings.User)
                .HasForeignKey<UserLlmSettings>(settings => settings.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserLlmSettings>(entity =>
        {
            entity.HasKey(settings => settings.UserId);
            entity.Property(settings => settings.Provider).HasConversion<string>().HasMaxLength(32);
            entity.Property(settings => settings.BaseUrl).HasMaxLength(256).IsRequired();
            entity.Property(settings => settings.Model).HasMaxLength(120).IsRequired();
            entity.Property(settings => settings.ApiKey).HasMaxLength(512);
        });

        modelBuilder.Entity<Word>(entity =>
        {
            entity.HasKey(word => word.Id);
            entity.HasIndex(word => word.Lemma).IsUnique();
            entity.Property(word => word.Lemma).HasMaxLength(80).IsRequired();
            entity.Property(word => word.PartOfSpeech).HasMaxLength(40);
            entity.Property(word => word.Phonetics).HasMaxLength(120);
            entity.Property(word => word.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(word => word.CefrLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(word => word.Meanings)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.Property(word => word.ExampleSentences)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.HasOne(word => word.LlmAnnotation)
                .WithOne(annotation => annotation.Word)
                .HasForeignKey<WordDifficultyAnnotation>(annotation => annotation.WordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WordDifficultyAnnotation>(entity =>
        {
            entity.HasKey(annotation => annotation.Id);
            entity.Property(annotation => annotation.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(annotation => annotation.CefrLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(annotation => annotation.RecommendedAction).HasConversion<string>().HasMaxLength(32);
            entity.Property(annotation => annotation.ModelProfileId).HasMaxLength(80);
            entity.Property(annotation => annotation.DimensionsJson).HasMaxLength(2000);
            entity.Property(annotation => annotation.SourcesJson).HasMaxLength(4000);
            entity.Property(annotation => annotation.PromptVersion).HasMaxLength(40);
        });

        modelBuilder.Entity<DifficultyAnnotation>(entity =>
        {
            entity.HasKey(annotation => annotation.Id);
            entity.HasIndex(annotation => new { annotation.ItemType, annotation.ItemHash }).IsUnique();
            entity.Property(annotation => annotation.ItemType).HasConversion<string>().HasMaxLength(32);
            entity.Property(annotation => annotation.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(annotation => annotation.CefrLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(annotation => annotation.RecommendedAction).HasConversion<string>().HasMaxLength(32);
            entity.Property(annotation => annotation.ItemHash).HasMaxLength(128);
            entity.Property(annotation => annotation.ModelProfileId).HasMaxLength(80);
        });

        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.HasKey(progress => progress.Id);
            entity.HasIndex(progress => progress.UserId).IsUnique();
            entity.Property(progress => progress.OverallLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(progress => progress.VocabLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(progress => progress.SpellingLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(progress => progress.SentenceLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(progress => progress.ReadingLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(progress => progress.LevelStartDate);
            entity.Property(progress => progress.IsLevelLocked);
            entity.Property(progress => progress.HasCompletedInitialAssessment);
            entity.Property(progress => progress.PendingReviewCount);
            entity.Property(progress => progress.IsUpgradeCandidate);
            entity.Property(progress => progress.DifficultyBucket).HasMaxLength(32);
            entity.Property(progress => progress.CefrDisplay).HasMaxLength(8);
            entity.Property(progress => progress.LegacyCefrJson).HasMaxLength(2000);
            entity.HasOne(progress => progress.User)
                .WithMany(user => user.ProgressRecords)
                .HasForeignKey(progress => progress.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserWordRelationship>(entity =>
        {
            entity.HasKey(relationship => relationship.Id);
            entity.HasIndex(relationship => new { relationship.UserId, relationship.WordId }).IsUnique();
            entity.Property(relationship => relationship.Source).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(relationship => relationship.User)
                .WithMany(user => user.WordRelationships)
                .HasForeignKey(relationship => relationship.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(relationship => relationship.Word)
                .WithMany(word => word.UserRelationships)
                .HasForeignKey(relationship => relationship.WordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WordLearningLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Rating).HasConversion<string>().HasMaxLength(32);
            entity.Property(log => log.Answer).HasMaxLength(500);
            entity.HasOne(log => log.User)
                .WithMany(user => user.LearningLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(log => log.Word)
                .WithMany(word => word.LearningLogs)
                .HasForeignKey(log => log.WordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Sentence>(entity =>
        {
            entity.HasKey(sentence => sentence.Id);
            entity.Property(sentence => sentence.Content).HasMaxLength(600).IsRequired();
            entity.Property(sentence => sentence.TargetWord).HasMaxLength(80).IsRequired();
            entity.Property(sentence => sentence.Scene).HasMaxLength(40);
            entity.Property(sentence => sentence.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(sentence => sentence.CefrLevel).HasConversion<string>().HasMaxLength(8);
            entity.HasOne(sentence => sentence.Word)
                .WithMany(word => word.Sentences)
                .HasForeignKey(sentence => sentence.WordId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(sentence => sentence.Annotation)
                .WithMany()
                .HasForeignKey(sentence => sentence.AnnotationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SentenceLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.TargetWord).HasMaxLength(80).IsRequired();
            entity.Property(log => log.Scene).HasMaxLength(40);
            entity.Property(log => log.UserSentence).HasMaxLength(1000).IsRequired();
            entity.Property(log => log.AiRevision).HasMaxLength(1000);
            entity.Property(log => log.OverallGrade).HasMaxLength(4);
            entity.Property(log => log.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(log => log.Suggestion).HasMaxLength(1000);
            entity.Property(log => log.ErrorTags)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.HasOne(log => log.User)
                .WithMany(user => user.SentenceLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(log => log.Word)
                .WithMany(word => word.SentenceLogs)
                .HasForeignKey(log => log.WordId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FreeExpressionLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.UserText).HasMaxLength(4000).IsRequired();
            entity.Property(log => log.OverallGrade).HasMaxLength(4);
            entity.Property(log => log.AiRevision).HasMaxLength(4000);
            entity.Property(log => log.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(log => log.ErrorSentences)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.Property(log => log.Suggestions)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.HasOne(log => log.User)
                .WithMany(user => user.FreeExpressionLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpellingLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.UserSpelling).HasMaxLength(120).IsRequired();
            entity.Property(log => log.CorrectSpelling).HasMaxLength(120).IsRequired();
            entity.Property(log => log.ErrorPositions)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<int>>(value, JsonOptions) ?? new List<int>())
                .Metadata.SetValueComparer(IntListComparer);
            entity.HasOne(log => log.User)
                .WithMany(user => user.SpellingLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(log => log.Word)
                .WithMany(word => word.SpellingLogs)
                .HasForeignKey(log => log.WordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(article => article.Id);
            entity.Property(article => article.Title).HasMaxLength(200).IsRequired();
            entity.Property(article => article.Content).HasMaxLength(8000).IsRequired();
            entity.Property(article => article.DifficultyLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(article => article.CefrLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(article => article.Source).HasConversion<string>().HasMaxLength(16);
            entity.Property(article => article.TopicTag).HasMaxLength(80);
            entity.HasOne(article => article.Annotation)
                .WithMany()
                .HasForeignKey(article => article.AnnotationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReadingLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.HasOne(log => log.User)
                .WithMany(user => user.ReadingLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(log => log.Article)
                .WithMany(article => article.ReadingLogs)
                .HasForeignKey(log => log.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleComment>(entity =>
        {
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.ParagraphText).HasMaxLength(2000).IsRequired();
            entity.Property(comment => comment.CommentText).HasMaxLength(2000).IsRequired();
            entity.Property(comment => comment.AiReply).HasMaxLength(4000);
            entity.HasOne(comment => comment.User)
                .WithMany(user => user.ArticleComments)
                .HasForeignKey(comment => comment.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(comment => comment.Article)
                .WithMany(article => article.Comments)
                .HasForeignKey(comment => comment.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArticleVocabMapping>(entity =>
        {
            entity.HasKey(mapping => mapping.Id);
            entity.HasIndex(mapping => new { mapping.ArticleId, mapping.WordLemma }).IsUnique();
            entity.Property(mapping => mapping.WordLemma).HasMaxLength(80).IsRequired();
            entity.Property(mapping => mapping.ContextMeaning).HasMaxLength(500).IsRequired();
            entity.Property(mapping => mapping.Phonetics).HasMaxLength(64);
            entity.Property(mapping => mapping.ExamplesJson);
            entity.Property(mapping => mapping.SpecialUsage).HasMaxLength(500);
            entity.Property(mapping => mapping.DifficultyInContext).HasConversion<string>().HasMaxLength(32);
            entity.Property(mapping => mapping.RecommendedAction).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(mapping => mapping.Article)
                .WithMany(article => article.VocabMappings)
                .HasForeignKey(mapping => mapping.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(mapping => mapping.Word)
                .WithMany(word => word.ArticleVocabMappings)
                .HasForeignKey(mapping => mapping.WordId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.FinalLevel).HasConversion<string>().HasMaxLength(8);
            entity.HasOne(item => item.User)
                .WithMany(user => user.Assessments)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssessmentRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Step).HasConversion<string>().HasMaxLength(16);
            entity.Property(record => record.QuestionType).HasMaxLength(32);
            entity.Property(record => record.QuestionsJson).HasMaxLength(16000);
            entity.Property(record => record.AnswersJson).HasMaxLength(8000);
            entity.Property(record => record.ScoresJson).HasMaxLength(4000);
            entity.HasOne(record => record.Assessment)
                .WithMany(assessment => assessment.Records)
                .HasForeignKey(record => record.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChallengeRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.ChallengeType).HasConversion<string>().HasMaxLength(32);
            entity.Property(record => record.AttemptedLevel).HasConversion<string>().HasMaxLength(8);
            entity.HasOne(record => record.User)
                .WithMany(user => user.ChallengeRecords)
                .HasForeignKey(record => record.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LevelHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.FromLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(history => history.ToLevel).HasConversion<string>().HasMaxLength(8);
            entity.Property(history => history.Reason).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(history => history.User)
                .WithMany(user => user.LevelHistories)
                .HasForeignKey(history => history.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LearningEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.OccurredAt });
            entity.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany(user => user.LearningEvents)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileScoreSnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.UserId, s.Date }).IsUnique();
            entity.Property(s => s.ScoresJson).HasMaxLength(2000).IsRequired();
            entity.HasOne(s => s.User)
                .WithMany(user => user.ProfileScoreSnapshots)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvaluationReport>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.IdempotencyKey).IsUnique();
            entity.HasIndex(r => new { r.UserId, r.CreatedAt });
            entity.Property(r => r.TriggerType).HasMaxLength(32).IsRequired();
            entity.Property(r => r.InputSnapshotJson).HasMaxLength(16000).IsRequired();
            entity.Property(r => r.InputSnapshotHash).HasMaxLength(64).IsRequired();
            entity.Property(r => r.ContentJson).HasMaxLength(16000).IsRequired();
            entity.Property(r => r.Status).HasMaxLength(16).IsRequired();
            entity.Property(r => r.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(r => r.ModelProfileId).HasMaxLength(80);
            entity.HasOne(r => r.User)
                .WithMany(user => user.EvaluationReports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Assessment)
                .WithMany()
                .HasForeignKey(r => r.AssessmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BackgroundJob>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.HasIndex(j => j.IdempotencyKey).IsUnique();
            entity.HasIndex(j => new { j.Status, j.CreatedAt });
            entity.Property(j => j.JobType).HasMaxLength(64).IsRequired();
            entity.Property(j => j.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(j => j.Status).HasMaxLength(16).IsRequired();
            entity.Property(j => j.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(j => j.ErrorMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<UserFeedback>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.UserId, f.CreatedAt });
            entity.Property(f => f.FeedbackType).HasMaxLength(32).IsRequired();
            entity.Property(f => f.TargetWord).HasMaxLength(80).IsRequired();
            entity.Property(f => f.ContextJson).HasMaxLength(4000);
            entity.Property(f => f.Status).HasMaxLength(16).IsRequired();
            entity.HasOne(f => f.User)
                .WithMany(user => user.UserFeedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserWordExclude>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.WordLemma }).IsUnique();
            entity.Property(e => e.WordLemma).HasMaxLength(80).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany(user => user.WordExcludes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChallengeSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => new { session.UserId, session.CreatedAt });
            entity.Property(session => session.PackJson).HasMaxLength(32000).IsRequired();
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
