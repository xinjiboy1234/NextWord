using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NextWord.Domain.Entities;

namespace NextWord.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        value => value.ToList());

    public DbSet<User> Users => Set<User>();
    public DbSet<Word> Words => Set<Word>();
    public DbSet<WordDifficultyAnnotation> WordDifficultyAnnotations => Set<WordDifficultyAnnotation>();
    public DbSet<DifficultyAnnotation> DifficultyAnnotations => Set<DifficultyAnnotation>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<UserWordRelationship> UserWordRelationships => Set<UserWordRelationship>();
    public DbSet<WordLearningLog> WordLearningLogs => Set<WordLearningLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName).HasMaxLength(80).IsRequired();
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
    }
}
