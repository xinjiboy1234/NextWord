using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ChallengePackGenerator(ApplicationDbContext db) : IChallengePackGenerator
{
    /// <summary>T-035：阅读题量 1→3，按目标档从库内选文出题，降单题 0/100 方差。</summary>
    private const int ReadingQuestionCount = 3;

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

        var readings = await BuildReadingsAsync(targetLevel, cancellationToken);

        return new ChallengePack(vocabulary, sentence, readings[0], targetLevel) { Readings = readings };
    }

    /// <summary>
    /// 阅读题组（T-035）：选文与考点词口径复用测评（AssessmentService.BuildReadingItemAsync）——
    /// 按难度带就近选文，考点词为正文（摘要）中出现的带内单词，词义选择题，答案位置随机。
    /// </summary>
    private async Task<IReadOnlyList<ReadingQuizQuestion>> BuildReadingsAsync(CefrLevel targetLevel, CancellationToken cancellationToken)
    {
        // 与测评同一词池纪律：utility=high/medium；low 永不入池
        var allWords = await db.Words.AsNoTracking()
            .Where(word => word.Utility == WordUtility.High || word.Utility == WordUtility.Medium)
            .ToListAsync(cancellationToken);
        var bandWords = allWords.Where(word => word.CefrLevel == targetLevel).ToList();
        if (bandWords.Count < 4 && targetLevel > CefrLevel.A1)
        {
            // 兜底：顶端带词池过薄时向下一带补充——仍不超目标带
            bandWords.AddRange(allWords.Where(word => word.CefrLevel == (CefrLevel)((int)targetLevel - 1)));
        }

        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        if (articles.Count == 0)
        {
            throw new InvalidOperationException("No articles available for challenge pack.");
        }

        // 按目标档就近 + 随机排序；每篇出 1 题，取满 3 篇为止
        var candidates = articles
            .OrderBy(item => Math.Abs((int)item.CefrLevel - (int)targetLevel))
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        var readings = new List<ReadingQuizQuestion>();
        foreach (var article in candidates)
        {
            if (readings.Count >= ReadingQuestionCount)
            {
                break;
            }

            var excerpt = article.Content.Length > 220 ? article.Content[..220] + "..." : article.Content;
            var question = BuildReadingQuestion(article.Id, excerpt, bandWords, allWords);
            if (question is not null)
            {
                readings.Add(question);
            }
        }

        if (readings.Count == 0)
        {
            // 极端兜底：文章里找不到任何考点词时，保留旧的主旨题口径，保证挑战可发起
            var article = candidates[0];
            var excerpt = article.Content.Length > 220 ? article.Content[..220] + "..." : article.Content;
            readings.Add(new ReadingQuizQuestion(
                article.Id,
                "What is the main idea of this passage?",
                ["Daily life and learning", "Sports competition", "Space exploration", "Cooking recipes"],
                0,
                excerpt));
        }

        return readings;
    }

    /// <summary>单篇出题：考点词必须出现在摘要正文中且为单词（非短语）；找不到返回 null（跳过该篇）。</summary>
    private static ReadingQuizQuestion? BuildReadingQuestion(
        Guid articleId,
        string excerpt,
        IReadOnlyList<Domain.Entities.Word> bandWords,
        IReadOnlyList<Domain.Entities.Word> allWords)
    {
        var tokens = excerpt
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '"', '(', ')', '’', '\'', '…'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .ToHashSet();

        var keyWord = bandWords.Where(word => word.Meanings.Count > 0 && !word.Lemma.Contains(' ') && tokens.Contains(word.Lemma.ToLowerInvariant()))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault()
            ?? allWords.Where(word => word.Meanings.Count > 0 && !word.Lemma.Contains(' ') && tokens.Contains(word.Lemma.ToLowerInvariant()))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();
        if (keyWord is null)
        {
            return null;
        }

        var correct = keyWord.Meanings[0];
        var options = allWords
            .Where(word => word.Id != keyWord.Id && word.Meanings.Count > 0)
            .Select(word => word.Meanings[0])
            .Distinct()
            .Where(meaning => meaning != correct)
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .Append(correct)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        return new ReadingQuizQuestion(
            articleId,
            $"文中 \"{keyWord.Lemma}\" 的含义是什么？",
            options,
            options.IndexOf(correct),
            excerpt);
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
