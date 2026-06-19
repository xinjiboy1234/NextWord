using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ChallengePackGenerator(ApplicationDbContext db) : IChallengePackGenerator
{
    public async Task<ChallengePack> GenerateAsync(Guid userId, CefrLevel currentLevel, bool confirmationChallenge, CancellationToken cancellationToken)
    {
        _ = userId;
        var targetLevel = confirmationChallenge ? currentLevel : GetNextLevel(currentLevel);

        var words = await db.Words.AsNoTracking()
            .Where(word => word.CefrLevel == targetLevel || word.CefrLevel == GetNextLevel(targetLevel))
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToListAsync(cancellationToken);

        if (words.Count < 5)
        {
            words = await db.Words.AsNoTracking().OrderBy(_ => Guid.NewGuid()).Take(5).ToListAsync(cancellationToken);
        }

        var vocabulary = words.Select(word =>
        {
            var options = BuildOptions(word.Meanings.FirstOrDefault() ?? word.Lemma, words);
            return new VocabQuizQuestion(word.Lemma, options.Options, options.CorrectIndex, word.DifficultyLevel);
        }).ToList();

        var sentenceWord = words.FirstOrDefault() ?? words[0];
        var sentence = new SentenceQuizQuestion(sentenceWord.Id, sentenceWord.Lemma, "academic");

        var article = await db.Articles.AsNoTracking()
            .Where(item => item.CefrLevel == targetLevel || item.CefrLevel == currentLevel)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.Articles.AsNoTracking().FirstAsync(cancellationToken);

        var excerpt = article.Content.Length > 220 ? article.Content[..220] + "..." : article.Content;
        var reading = new ReadingQuizQuestion(
            article.Id,
            "What is the main idea of this passage?",
            ["Daily life and learning", "Sports competition", "Space exploration", "Cooking recipes"],
            0,
            excerpt);

        return new ChallengePack(vocabulary, sentence, reading, targetLevel);
    }

    private static (IReadOnlyList<string> Options, int CorrectIndex) BuildOptions(string correct, IReadOnlyList<Domain.Entities.Word> pool)
    {
        var distractors = pool
            .Select(word => word.Meanings.FirstOrDefault() ?? word.Lemma)
            .Where(meaning => !string.Equals(meaning, correct, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        while (distractors.Count < 3)
        {
            distractors.Add($"Option {distractors.Count + 1}");
        }

        var options = new List<string> { correct };
        options.AddRange(distractors);
        options = options.OrderBy(_ => Guid.NewGuid()).ToList();
        var correctIndex = options.FindIndex(option => string.Equals(option, correct, StringComparison.OrdinalIgnoreCase));
        return (options, Math.Max(0, correctIndex));
    }

    private static CefrLevel GetNextLevel(CefrLevel current) =>
        current >= CefrLevel.C1 ? CefrLevel.C1 : (CefrLevel)((int)current + 1);
}
