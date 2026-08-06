# 设计方案：Vocabulary / Reading 维日常回写口径（T-046）

> 状态：已定稿（2026-08-07，顾言）
> 依据：report/qa-t039/RESULT.md（V/R 30 天死平线——T-022 只写 Writing，月度时间轴 T-036 不能展示死平线）
> 前置：T-022 练习回写模式（小步 delta + LearningEvents 幂等键）

## 1. 问题

Score 三维里只有 Writing 有日常数据源（T-022）。Vocabulary/Reading 只在测评和挑战时动，日常背词和阅读行为不产生任何分数反馈——总分 = 最短板，V/R 不动则 Overall 也基本不动，「画像持续更新」仍缺两条腿。

## 2. 方案（顾言拍板）：沿用 T-022 模式，小步 delta + 幂等键，不改总分口径

### 2.1 Vocabulary ← 背词考察（`POST /api/learning/submit`）

- observed = 考察词有效难度分（`EffectiveDifficultyCalculator` 的 0-100 口径）× 表现系数：**答对 1.0 / 答错 0.3**（答错不是零——错在难词上有信息量，且避免刷简单词涨分、错难题猛扣的失真）；
- delta = clamp(round((observed − current) × 0.05), −1, +1)（比 Writing 的 0.1 更缓——背词每天 10 次高频，防刷）；
- 幂等键 `vocab-score:{WordLearningLogId}`；Source `VocabularyPractice`。

### 2.2 Reading ← 阅读完成（`POST /api/reading-logs/{logId}/finish`）

- observed = 文章难度分（0-100）× 查词修正系数：查词率（查词数/正文词数）≤5% → 1.0；每超 5% 减 0.1，下限 0.5（查词多是正常学习行为，只降权不惩罚）；
- delta = clamp(round((observed − current) × 0.1), −2, +2)（阅读低频，步长同 Writing）；
- 幂等键 `reading-score:{ReadingLogId}`；Source `ReadingPractice`。

### 2.3 不做什么

- 总分 = 三维最小值的口径不动；测评/挑战写入优先级不动（absolute 覆盖 delta 积累是既有行为，接受）；
- 拼写不回写（与背词高度相关，防双重计数）；
- 前端不加强制打扰：分数变化在 Profile/月度时间轴自然可见，不做每次弹窗。

## 3. 约束

- 全部走 `ScoreProfileService.ApplyUpdateAsync` 唯一入口；幂等键复用 LearningEvents 既有机制；
- 系数与步长留常量（0.05/0.1、±1/±2、0.3、查词率档位），后续按仿真校准；
- T-038 展示档迟滞对 V/R 变化自然生效，无需特判。

## 4. 验收标准

1. 背词考察提交后 Vocabulary 按口径变化（答对升/答错缓降，±1 clamp 边界各一例）；
2. 阅读完成后 Reading 按口径变化（查词率修正系数三档边界）；
3. 幂等：同一 logId 重放不重复加分；
4. 30 天仿真复跑：V/R 曲线非水平线且方向合理（菜鸟稳定学习缓慢上行）；
5. 既有 Score/挑战/测评测试零回归。
