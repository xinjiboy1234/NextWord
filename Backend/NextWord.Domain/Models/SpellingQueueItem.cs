using NextWord.Domain.Entities;

namespace NextWord.Domain.Models;

/// <summary>T-052：拼写队列项——词 + 来源标记（IsReview=true 到期复习词，false 带内新词，前端徽标用）。</summary>
public sealed record SpellingQueueItem(Word Word, bool IsReview);
