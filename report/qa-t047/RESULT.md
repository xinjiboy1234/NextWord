# T-047「V/R 维日常回写」轻量验收（周密）

> 日期：2026-08-07 ｜ 被验：worktree `.worktrees/t-047-vr-writeback`（分支 feat/t-047-vr-writeback，commit 2e4456f）
> 依据：docs/DESIGN-vr-score-writeback.md（T-046 定稿）｜ 模式同 T-022（已重度验收），本次聚焦口径一致性
> 环境：worktree 源码构建 → API Development @5187（--no-launch-profile，日志 api.log）；独立库 `nextword_qa_t047`（验完已 DROP）；LLM DashScope qwen-plus 真实链路
> 证据：`probe.py`（抽查脚本）、`probe-output.txt`（链路输出）、`api.log`（无 token 残留，已核验）

## 结论：**通过**

口径与设计 §2 逐条一致，真实链路 V/R 回写按口径生效、幂等成立、拼写无回写；测评/挑战 absolute 写入未动。

## 1. diff 静态审查（8 文件 +393/-11）— ✅

- **最小改动**：仅 `PracticeScoreWritebackService`（+102 行扩展两腿）、两个挂载点（`LearningEndpoints.submit` +6、`ArticleEndpoints.finish` +11）、新增 `VrScoreWritebackTests`（256 行）、既有测试构造器适配（1 行）、文档/CURRENT-STATE/tasks.csv 同步。无顺手重构。
- **Vocabulary 口径**（`PracticeScoreWritebackService.cs` ApplyVocabularyAsync）：observed = `EffectiveDifficultyCalculator` 有效难度分 × 表现系数（对 1.0 / 错 `WrongAnswerFactor=0.3`）；delta = clamp(round((observed − current) × `VocabStepFactor=0.05`), −1, +1)；幂等键 `vocab-score:{logId}`；Source `VocabularyPractice` —— 与设计 §2.1 逐字一致。
- **Reading 口径**（ApplyReadingAsync）：observed = 文章难度分（`LegacyScoreHelper.FromDifficulty`）× 查词修正系数（查词率 ≤5% → 1.0，每超 5% 减 0.1，下限 `LookupCoefficientFloor=0.5`，边界浮点误差有 1e-9 修正）；delta = clamp(round((observed − current) × 0.1), −2, +2)；幂等键 `reading-score:{logId}`；Source `ReadingPractice` —— 与设计 §2.2 一致。
- **ProfileScoreDelta 槽位核对**：`ProfileScoreDelta(Vocabulary, Reading, Writing, Spelling)` 顺序确认（ProfileUpdateCommand.cs:19），背词写第一槽、阅读写第二槽，无误位。
- **唯一入口**：两腿均走 `ScoreProfileService.ApplyUpdateAsync` + LearningEvents 幂等（设计 §3）；delta=0 落幂等记录注释与 T-022 一致。
- **未动面**：测评/挑战 absolute 写入零改动（diff 不触及 AssessmentService/ChallengeService）；Writing 腿（T-022）逻辑原样；拼写端点 `SpellingEndpoints` 独立未被挂载（防双重计数 ✓）；端点响应 DTO 不变；DI 注册 Scoped 适配新 `ApplicationDbContext` 依赖无误。
- 取整方式 `MidpointRounding.AwayFromZero` 与 T-022 Writing 腿一致（设计只写 round，属口径内统一）。

## 2. 真实链路抽查（probe.py，新用户 9bffe451）— ✅

基线（跳过初测）：V/R/W 全 null，Overall 0。

| 步骤 | 动作 | 结果 | 口径核验 |
|---|---|---|---|
| 背词 1 | 答对（recognition） | V null→1 | observed>0、current=0 → round 后 +1，clamp 内 ✓ |
| 背词 2 | 答对 | V 1→2 | +1 ✓ |
| 背词 3 | 答错 | V 2→3 | observed=难度分×0.3≈12 > current=2 → +1（向 observed 收敛，口径内，见观察项 1） |
| 阅读 | Advanced 文 finish，查词 0 | R null→2 | 查词率 0% → 系数 1.0，observed=75，delta=round(7.5)=+2 触 clamp 上限 ✓（live 验证了 ±2 clamp） |
| 重放 | 同 logId 再 finish | R 2→2 | 不重复加分 ✓ |
| 拼写 | spelling/submit 一次 | V/R/W 无变化 | 无回写 ✓ |

LearningEvents 库核验（DROP 前）：

```
VocabularyPractice | vocab-score:5cf8e7fb-…  (背词1)
VocabularyPractice | vocab-score:ce8f8a85-…  (背词2)
VocabularyPractice | vocab-score:7c0032ce-…  (背词3)
ReadingPractice    | reading-score:e23efaff-…(= 实际 ReadingLogId，重放未产第二条)
```
共 4 行，Source/键前缀/幂等粒度（按 logId）与设计一致；无任何拼写来源事件。

## 3. 单测抽查 — ✅

worktree 内 `dotnet test --filter VrScoreWritebackTests|PracticeScoreWritebackTests`：24/24 通过（含 ±1/±2 clamp 边界、答错缓降、重放幂等、LookupCoefficient 9 档 Theory）。全量套件按约定不重跑，采用开发自报 236+6 绿 + 本次 24 例口径测试复核。

## 观察项与不足

1. **低基线下答错也 +1**（非缺陷）：跳过初测后 V 为 null（按 0 计），答错 observed≈12 仍高于 current → delta +1。「答错缓降 / −1 clamp」方向由单测覆盖（大差距场景），真实链路因基线为 0 无条件复现。口径本身符合设计（delta 向 observed 收敛），但提示：skip 用户画像分数为 null 时前几次回写全是上行，建议 30 天仿真复跑时关注菜鸟人设 V/R 曲线起点段形态。
2. **文章缺失兜底 50 分**（`article is null ? 50`）：设计未述的防御性分支，方向合理（中性分），不阻断。
3. **设计 §4-4「30 天仿真复跑 V/R 曲线非平线」未在本轮执行**：本次按指派聚焦口径一致性轻量验收；仿真复跑建议并入 T-036 月度时间轴验收或单开仿真任务（T-022 模式已有 qa-t039 仿真打底，风险低）。
4. 环境已清理：API 进程已杀（5187 无响应）、`nextword_qa_t047` 已 DROP；api.log 核验无 API key 残留。
