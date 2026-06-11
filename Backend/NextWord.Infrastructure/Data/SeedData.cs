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
            db.Words.AddRange(CreateWords());
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
}
