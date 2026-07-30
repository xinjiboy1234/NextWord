using NextWord.Domain.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-040 统一命中口径（TargetWordMatcher，纯函数）：
/// 单词词边界匹配（不误伤子串）；多词短语连续词序列匹配（大小写不敏感、
/// 容忍标点/多余空白分隔、词序必须一致）；不做词形变换。
/// </summary>
public class TargetWordMatcherTests
{
    // ── 单词：既有词边界行为回归 ─────────────────────────────

    [Fact]
    public void Single_word_hits_on_word_boundary_only()
    {
        Assert.True(TargetWordMatcher.IsHit("arm", "He raised his arm."));
        Assert.True(TargetWordMatcher.IsHit("Arm", "ARM and a leg"));   // 大小写不敏感
        Assert.False(TargetWordMatcher.IsHit("arm", "He is armed."));   // 子串不算命中
        Assert.False(TargetWordMatcher.IsHit("arm", string.Empty));
        Assert.False(TargetWordMatcher.IsHit("arm", null));
    }

    // ── 多词短语：连续词序列匹配 ─────────────────────────────

    [Fact]
    public void Phrase_hits_with_punctuation_and_extra_whitespace_variants()
    {
        Assert.True(TargetWordMatcher.IsHit("up in arms", "They are up in arms about the plan."));
        Assert.True(TargetWordMatcher.IsHit("up in arms", "They are up, in arms, about the plan."));
        Assert.True(TargetWordMatcher.IsHit("up in arms", "They are up  in  arms about the plan."));
        Assert.True(TargetWordMatcher.IsHit("up in arms", "UP IN ARMS again!"));
        Assert.True(TargetWordMatcher.IsHit("  up in arms  ", "They are up in arms.")); // 目标词首尾空白容忍
        Assert.True(TargetWordMatcher.IsHit("see eye to eye", "We finally see eye to eye on this."));
    }

    [Fact]
    public void Phrase_misses_when_words_out_of_order_or_not_contiguous()
    {
        Assert.False(TargetWordMatcher.IsHit("up in arms", "armed up in"));                  // 乱序不命中
        Assert.False(TargetWordMatcher.IsHit("up in arms", "up and in his arms"));           // 中间插词不连续
        Assert.False(TargetWordMatcher.IsHit("up in arms", "arms up in"));                   // 逆序
        Assert.False(TargetWordMatcher.IsHit("up in arms", "He is up and armed."));          // 只命中部分词
        Assert.False(TargetWordMatcher.IsHit("up in arms", "He is upbeat in arms."));        // 词边界仍生效（upbeat ≠ up）
    }

    [Fact]
    public void Phrase_matching_is_literally_lowercased_no_lemma_transform()
    {
        // 不做词形变换：文本与目标词必须原样（小写后）一致
        Assert.False(TargetWordMatcher.IsHit("go up", "He went up the hill."));
    }

    [Fact]
    public void Empty_target_never_hits()
    {
        Assert.False(TargetWordMatcher.IsHit("", "any text"));
        Assert.False(TargetWordMatcher.IsHit("   ", "any text"));
    }
}
