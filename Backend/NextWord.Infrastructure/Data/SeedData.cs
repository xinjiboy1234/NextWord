using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Infrastructure.Data;

public static class SeedData
{
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task InitializeAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.Add(new User
            {
                Id = DefaultUserId,
                DisplayName = "MVP Learner",
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.UserProgress.Add(new UserProgress
            {
                UserId = DefaultUserId,
                StreakDays = 0
            });
        }

        if (!await db.Words.AnyAsync(cancellationToken))
        {
            // 内置精选词表（含场景标注）为主，历史 6 词兜底合并缺失 lemma
            var words = WordlistSeedData.LoadEntries().Select(WordlistSeedData.ToWord).ToList();
            var lemmas = words.Select(word => word.Lemma).ToHashSet(StringComparer.OrdinalIgnoreCase);
            words.AddRange(CreateWords().Where(word => !lemmas.Contains(word.Lemma)));
            db.Words.AddRange(words);
        }

        if (!await db.Sentences.AnyAsync(cancellationToken))
        {
            db.Sentences.AddRange(CreateSentences());
        }

        if (!await db.Articles.AnyAsync(cancellationToken))
        {
            db.Articles.AddRange(ArticleSeedData.CreateArticles());
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Word> CreateWords()
    {
        yield return CreateWord("apple", "n.", "/ˈæpəl/", ["苹果"], ["I eat an apple every morning."], DifficultyLevel.Basic, CefrLevel.A1);
        yield return CreateWord("friend", "n.", "/frend/", ["朋友"], ["She is my best friend."], DifficultyLevel.Basic, CefrLevel.A1);
        yield return CreateWord("practice", "v.", "/ˈpræktɪs/", ["练习", "实践"], ["We practice English after dinner."], DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateWord("memory", "n.", "/ˈmeməri/", ["记忆", "回忆"], ["Good memory needs regular review."], DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateWord("ambiguous", "adj.", "/æmˈbɪɡjuəs/", ["模棱两可的", "含糊的"], ["The sentence is ambiguous without context."], DifficultyLevel.Advanced, CefrLevel.C1);
        yield return CreateWord("synthesize", "v.", "/ˈsɪnθəsaɪz/", ["综合", "合成"], ["Learners synthesize new ideas from examples."], DifficultyLevel.Advanced, CefrLevel.C1);
    }

    private static Word CreateWord(
        string lemma,
        string partOfSpeech,
        string phonetics,
        List<string> meanings,
        List<string> examples,
        DifficultyLevel difficulty,
        CefrLevel cefr)
    {
        return new Word
        {
            Lemma = lemma,
            PartOfSpeech = partOfSpeech,
            Phonetics = phonetics,
            Meanings = meanings,
            ExampleSentences = examples,
            DifficultyLevel = difficulty,
            CefrLevel = cefr,
            IsCore = true
        };
    }

    private static IEnumerable<Sentence> CreateSentences()
    {
        yield return CreateSentence("I eat an apple after lunch.", "apple", "life", DifficultyLevel.Basic, CefrLevel.A1);
        yield return CreateSentence("A good friend listens when you need help.", "friend", "life", DifficultyLevel.Basic, CefrLevel.A1);
        yield return CreateSentence("Regular practice makes speaking feel easier.", "practice", "school", DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateSentence("This memory helps me understand the story.", "memory", "life", DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateSentence("The teacher gave an ambiguous answer.", "ambiguous", "academic", DifficultyLevel.Advanced, CefrLevel.C1);
        yield return CreateSentence("Writers synthesize facts into a clear argument.", "synthesize", "academic", DifficultyLevel.Advanced, CefrLevel.C1);
        yield return CreateSentence("We compare two ideas before making a decision.", "decision", "work", DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateSentence("A healthy habit can improve your daily energy.", "habit", "life", DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateSentence("The new feature saves time for the whole team.", "feature", "work", DifficultyLevel.Intermediate, CefrLevel.B1);
        yield return CreateSentence("The report should validate the main hypothesis.", "validate", "academic", DifficultyLevel.Advanced, CefrLevel.C1);
    }

    private static Sentence CreateSentence(string content, string targetWord, string scene, DifficultyLevel difficulty, CefrLevel cefr)
    {
        return new Sentence
        {
            Content = content,
            TargetWord = targetWord,
            Scene = scene,
            DifficultyLevel = difficulty,
            CefrLevel = cefr
        };
    }
}
