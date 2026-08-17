using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// T-061 带内选词共用助手：拼写队列与每日词回退路径的统一取词口径。
/// 首选 [vocabScore, vocabScore+12] 难度带内的未学词；带内不足时向相邻带上下各扩 12
/// （两轮，覆盖 [score-24, score+36]）；仍不足则按与原始带的距离排序取任意未学词兜底——
/// 与每日词既有 candidates.Take 兜底对齐，保证「有未学词就永不返回空队列」。
/// 内在难度分解析：IntrinsicScore（LLM 标注）优先，缺失回退 CEFR 六档映射（LegacyScoreHelper.FromCefr）。
/// </summary>
internal static class BandWordSelector
{
    /// <summary>内在难度分（0–100）：LLM 标注优先，回退 CEFR 六档映射（词库词多数无 IntrinsicScore）。</summary>
    public static int IntrinsicScoreOf(Word word) =>
        word.LlmAnnotation?.IntrinsicScore ?? LegacyScoreHelper.FromCefr(word.CefrLevel);

    /// <summary>
    /// 取未学词（无 UserWordRelationships），按 [score, score+12] 带优先、相邻带扩展、全量兜底的顺序，
    /// 随机取最多 <paramref name="count"/> 个。候选为空（全部词已学）返回空列表。
    /// </summary>
    public static async Task<List<Word>> PickUnlearnedAsync(
        ApplicationDbContext db,
        Guid userId,
        int vocabScore,
        int count,
        CancellationToken cancellationToken)
    {
        var learnedIds = await db.UserWordRelationships
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);

        var candidates = await db.Words.AsNoTracking()
            .Include(word => word.LlmAnnotation)
            .Where(word => !learnedIds.Contains(word.Id))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var scored = candidates
            .Select(word => (Word: word, Score: IntrinsicScoreOf(word)))
            .ToList();

        var min = vocabScore;
        var max = Math.Min(100, vocabScore + 12);
        // 相邻带扩展边界（两轮，各上下扩 12，覆盖 [score-24, score+36]）
        var min2 = Math.Max(0, min - 12);
        var max2 = Math.Min(100, max + 12);
        var min3 = Math.Max(0, min2 - 12);
        var max3 = Math.Min(100, max2 + 12);

        var picked = scored
            .Where(item => item.Score >= min && item.Score <= max)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        if (picked.Count < count)
        {
            // 第一轮相邻带扩展：上下各扩 12
            picked.AddRange(scored
                .Where(item => item.Score < min || item.Score > max)
                .Where(item => item.Score >= min2 && item.Score <= max2)
                .OrderBy(_ => Random.Shared.Next()));
        }

        if (picked.Count < count)
        {
            // 第二轮相邻带扩展：再上下各扩 12
            picked.AddRange(scored
                .Where(item => item.Score < min2 || item.Score > max2)
                .Where(item => item.Score >= min3 && item.Score <= max3)
                .OrderBy(_ => Random.Shared.Next()));
        }

        if (picked.Count < count)
        {
            // 全量兜底：按与原始带距离排序（带内优先已在上面），同距随机
            picked.AddRange(scored
                .Where(item => picked.All(existing => existing.Word.Id != item.Word.Id))
                .OrderBy(item => Distance(item.Score, min, max))
                .ThenBy(_ => Random.Shared.Next()));
        }

        return picked.Take(count).Select(item => item.Word).ToList();
    }

    private static int Distance(int score, int min, int max) =>
        score < min ? min - score : score > max ? score - max : 0;
}
