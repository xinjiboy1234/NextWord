# CEFR Read-Path Audit (F-5 / T-066)

> 日期：2026-06-30  
> 原则：内部决策用 Score；CEFR 仅展示映射（`ScoreMappingService` / `UserProgress.CefrDisplay`）

## 已对齐 Score 的路径

| 模块 | 读路径 | 状态 |
|------|--------|------|
| 每日词 | `DailyWordSelectionService` → `IScoreProfileService.Vocabulary` band | ✅ Score 驱动 |
| 等级面板 | `LevelDashboardService` → `UserProfileScores` | ✅ API 返回 scores |
| 侧栏 | `useProfileScores` + `formatLevelLabel` | ✅ Score 优先，CEFR 可关 |
| 初测 complete | `AssessmentService` → `ScoreProfileService.ApplyUpdateAsync` | ✅ |
| 挑战通过 | `ChallengeService` → Profile delta | ✅ |

## 仍读 CEFR 的展示路径（允许）

| 位置 | 用途 | 风险 |
|------|------|------|
| `UserProgress.OverallLevel` 等 | Legacy 展示 / 历史兼容 | 低 — 由 `SyncLegacyLevels` 从 Score 投影 |
| `Article.cefrLevel` | 文章元数据标签 | 低 — 非选题决策 |
| `Word.cefrLevel` | 词卡展示 | 低 — 每日词不以此选题 |
| `SentenceStudio userLevel` prop | 造句提示 | 中 — 可改读 Score bucket |

## 禁止 CEFR-only 决策（已检查）

- [x] `DailyWordSelectionService` — 不用 `Word.CefrLevel` 过滤
- [x] `ChallengePackGenerator` — 仍用 `OverallLevel` 定包难度（legacy level 与 Score 同步）
- [ ] `ReviewQueueService` — 仍 SM-2，未接 Score（v1 可接受）

## 建议 follow-up

1. `SentenceStudio` / `SentenceCard` 改传 `difficultyBucket` 或 overall Score。
2. 文章推荐阅读按 `EffectiveDifficulty` 而非 `Article.CefrLevel`。
3. grep CI：`rg "CefrLevel" Backend/NextWord.Infrastructure/Services/DailyWordSelectionService.cs` 应为 0。
