# T-037 / T-038 轻量验收（周密，2026-08-06）

**结论：通过。**

Worktree：`.worktrees/t-037-freeexpr-target`（分支 feat/t-037-freeexpr-target，commits e48ec0d + c83470f）。
开发自报 dotnet test 216+6 绿（本次按任务口径未重跑全量）。

## 1. diff 审查（两个 commit 均最小改动、无夹带）

### T-037（e48ec0d）——自由表达评分去掉字面量 targetWord

- `FreeExpressionService.cs`：不再传 `"free expression"` 字面量，改传中性主题「日常自由表达」+ scene `daily-life` + `IsFreeExpression: true`；评分带仍走 T-027 的 RatingBandResolver 服务端口径。
- `LlmPromptFactory.BuildFreeExpressionRatingPrompt`（新变体）：
  - 无 Target Word 行 ✓
  - 明示 "There is NO target word: never penalize the learner for not using any particular word" ✓
  - relevance_score 口径改为「围绕日常场景/主题连贯展开、言之有物」✓
  - T-027 挑战度规则（ChallengeRules 常量）在造句与自由表达两个 prompt 中均注入，仍生效 ✓
- 造句 prompt 行为不变（Target Word 行保留），有回归测试 `Sentence_rating_prompt_unchanged_for_target_word_tasks`。
- `LlmMockProvider` 同步：自由表达不扣「未用目标词」；保留旧字面量判断作兼容兜底。
- 测试 4 例：请求侧断言、prompt 无 Target Word 行 + 挑战度规则在、造句 prompt 不变、Mock 高分回归。
- 已知同源遗留（测评情境题仍传字面量）已单列 T-043，非本次夹带，口径正确。

### T-038（c83470f）——cefrDisplay 下行迟滞

- 迟滞只在 `ScoreProfileService.ApplyUpdateAsync`：上行即时；降档需当前 Overall 与近 3 天 ProfileScoreSnapshots 全部低于当前展示档下限；快照不足 3 天不降。
- 测评 bypass：`ProfileUpdateCommand.BypassCefrDisplayHysteresis`，仅 AssessmentService 完成写入（含 T-042 矫正传导的下调）置 true，权威锚点不受迟滞 ✓。
- 快照 Overall 读取用 `"overall"` 属性，与 SnapshotWorker 的 `JsonSerializerDefaults.Web`（camelCase）序列化一致 ✓。
- 5 例边界单测：升档即时 / 单日跌破不降 / 连续 3 天跌破降档 / 快照不足不降 / 测评写入 bypass（B2→50 分立即降 B1，正是 T-042 矫正传导场景）✓。

## 2. 真实链路抽查（T-037 核心）

- API：worktree 起 Development、端口 5189、独立新库 `nextword_qa_t03738`（MigrateAsync 空库建全）、qwen-plus（DashScope 兼容端点，key 经环境变量传入不落盘）。
- 流程：注册 → `/api/assessment/initial/skip`（默认 A2）→ `POST /api/free-expression/rate` 提交一段连贯周末日常段落（见 rate-weekend.json，不含「free expression」字样）。
- 结果：`overallGrade = A`，`aiScore = 95`。
  - api.log 确认真实 LLM 调用：`LLM RateSentence completed in 10079ms (ProfileId=feedback-rich)`，无 fallback。
  - aiScore = 四维之和 × 5 = 95 → 四维之和 19/20 → **相关性维 ≥ 4/5**（其余三维各 ≤5 可推）。
  - **不再被判 off-topic、不再拿 C** —— 对照 T-027 验收时同类段落被误判 C 的记录（tasks.csv T-037 行），修复生效。
  - T-027 挑战度规则仍生效旁证：qwen 反馈按 A2 水平带指出 'slept in'/'recharged' 超带并给出带内替换建议。
- 附：T-022 回写正常（writingScoreBefore 0 → after 2）。

## 3. T-038 结论

纯规则逻辑 + 5 例边界单测（含测评 bypass），diff 审查确认，不实测——按任务口径。

## 证据文件

- `rate-weekend.json`：真实评分响应全文
- `register.json`：注册响应
- `api.log`：API 运行日志（含 LLM 调用计时）

## 清理

API 进程已杀（5189 已不可达），库 `nextword_qa_t03738` 已 DROP。

## 不足（非阻断）

- `FreeExpressionLog` 表只存总分不存四维明细，相关性维只能由总分下界推算（≥4），建议后续迭代在日志中留四维分（可作观察项，不另开任务亦可）。
- aiRevision 中 qwen 输出了个别引号渲染噪声（"our little family tradition" 处），内容正确，属模型输出风格，非本次改动引入。
