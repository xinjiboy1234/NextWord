# T-032「画像冷启动探索周」验收报告（周密）

- 日期：2026-07-30
- 对象：worktree `.worktrees/t-032-cold-start`，分支 feat/t-032-cold-start，commit 38fbc05（20 文件 +651/-19）
- 环境：API Development @5194，独立库 `nextword_qa_t032`（已 DROP），LLM DashScope qwen-plus（真实调用）；单测隔离库 `nextword_qa_t032_unit`（已 DROP）
- 依据：docs/DESIGN-cold-start-profile.md §4 验收标准

## 结论：不通过（2 项阻断）

触发器、幂等、Verifier 纪律、探索周进度展示均达标；但**冷启动的核心目标——「攒证据 → 画像 → 计划变个性化」在真实链路断裂**：表达证据进不了 Profiler，Skill Finding 进不了 Plan，三位实测用户的计划全部停留在探索期兜底。

## 逐条标准核验

### 标准 1（满 7 天自动重生成，≥1 Finding 进规划，计划变个性化）— ❌ 阻断

- 用户 B（注册回拨 8 天、零证据）：日检触发 ✓，画像落标记位 `weakness-profile-coldstart` ✓，force Planner 入队并完成 ✓（`planner:coldstart:{uid}:20260730` Completed）。
- **但画像 0 条 Finding**（db-final-state.txt：profile 2 findings=0），计划 `sourceFindingIds:[]` 仍探索期。
- 根因（代码审查 + 日志佐证）：`WeaknessProfiler.BuildDraftsAsync` 只聚合 SentenceLogs/测评/场景词统计/阅读统计；无任何输入时 LLM 800ms 返回空草稿（api-boot2.log）。

### 标准 2（攒够 10 条证据即触发，不必等满 7 天）— 触发 ✓ / 画像内容 ❌ 阻断

- 用户 A（注册当天，10 条自由表达，真实 qwen-plus 评分）：下一次日检即触发 ✓（未等 7 天）。
- **但画像同样 0 条 Finding**：触发计数含 FreeExpressionLogs，而 Profiler 输入不含 FreeExpressionLogs，Verifier 也无 `free_expression_log` 证据类型——**探索周主推的表达任务产生的证据对画像完全不可见**。
- 对照用户 C（10 条造句）：画像产出 4 条 Finding、3 条 Verified ✓；但计划仍 `sourceFindingIds:[]`——`LearningPlanService` 只把 **Scenario 维 Weakness** Finding 计入 sourceFindingIds（LearningPlanService.cs:105-119），Skill 维 Finding 读了不用。**即「Finding 进规划、计划变个性化」在表达-only 用户身上永远无法达成**，设计要打破的「计划永远探索期」死锁未破。

### 标准 3（Dashboard 进度文案 + 今日表达入口）— ✓ 通过

- API 实测：用户 A 注册当天 `exploration = {active:true, day:1, totalDays:7, evidenceCount:0, remainingEvidence:10, scenarioKey:daily_routine, prompt:...}`（planner-current-day1.json）；刷满 10 条后 `evidenceCount:10, remainingEvidence:0`（exploration-after10.json），与库内计数一致；用户 B 满 7 天后 `active:false` ✓。
- 前端静态审查（commit diff）：Dashboard 计划卡两分支均含「探索周·第 x/7 天」「再完成 N 次表达，生成你的专属画像」「去写今日表达」按钮；FreeExpression 任务横幅 + placeholder；SentenceStudio 探索期默认落自由表达 Tab。

### 标准 4（第二份画像恢复 Verifier 纪律）— ✓ 通过

- 定向单测（隔离库，9/9 通过）：`Verifier_relaxed_downgrades_thin_evidence_but_keeps_mechanical_checks`——放宽档条数不足降 low 标 Verified 注「初步判断」；伪造引用/篡改数值不放宽；默认档同草稿仍 Questioned。
- 真实链路佐证：用户 C 的 grammar(High) Finding 在放宽档下仍因「引用数值不属实：实际 3，声称 <= 2」判 Questioned——机械核查未放宽 ✓。

### 标准 5（幂等，每用户仅一次）— ✓ 通过

- 实测：第三次日检（boot3）仅新达标用户 C 触发，A/B 无第二份画像、无重复 planner 任务（db-final-state.txt、trigger-and-test-evidence.txt）。
- 单测 `Trigger_fires_only_once_and_distinguishes_bottleneck_regens`、`Cold_start_profile_persists_low_verified_with_marker_and_closes_trigger` 覆盖标记位闭环与瓶颈重生成不混淆。

## 不足清单

### 阻断

1. **FreeExpressionLogs 不计入画像输入**：触发计数（ColdStartExplorationService.CountEvidenceAsync）含自由表达，但 WeaknessProfiler 不聚合自由表达、Verifier 无对应证据类型 → 纯表达用户冷启动画像必为空（0 Finding）。验收标准 1/2 的「≥1 Finding」无法达成。建议：Profiler 增加 FreeExpressionLogs 证据包 + Verifier 增加 `free_expression_log` 证据类型（AiScore 等数值核查）。
2. **Skill 维 Finding 不进 Plan**：LearningPlanService 仅 Scenario 维 Weakness Finding 计入 sourceFindingIds；冷启动画像（表达证据只能产出 Skill 维 Finding）无法让计划变「个性化」，探索期死锁未破。建议：sourceFindingIds 纳入 Verified 的 Skill 维 Weakness Finding（造句目标/难度编排消费），或由 Profiler 基于表达场景产出 Scenario 维 Finding。

### 非阻断

3. **同日幂等口径串味**：`WeaknessProfileService.GenerateAsync(assessmentId:null)` 的同日去重不区分 ModelProfileId。同日先有瓶颈重生成 → 冷启动当天落不下标记位（延迟到次日，最终仍仅一次）；同日先有冷启动 → 瓶颈重生成当天被静默吞掉。建议同日幂等加 ModelProfileId 维度。
4. **触发循环无单用户容错**：ProfileScoreSnapshotWorker 的冷启动 foreach 内 `GenerateAsync` 未包 try——单用户 LLM 异常会中断当轮其余用户的判定（次日恢复）。建议逐用户 try/catch。
5. **多实例并发无防重**：标记位判定与生成之间无锁，多实例部署理论可双触发（单实例无此问题）。记录备查。
6. **存量老用户一次性批量触发**：上线后首个日检，所有注册满 7 天用户同时触发（每用户 1 次画像 LLM + force Planner）。符合设计语义，属一次性成本，建议上线时知会运维关注 LLM 配额。
7. **「初步判断」实测未出现**：用户 C 的 LLM 引用证据充足，3 条直接 medium/high Verified，放宽降 low 路径仅单测覆盖。不构成缺陷，但真实 LLM 下 low「初步判断」徽标的曝光率可能很低，产品侧知悉。

## 证据文件（本目录）

- `db-final-state.txt`：Users/WeaknessProfiles/ProfileFindings/LearningPlans/BackgroundJobs/证据计数终态
- `planner-current-day1.json`、`exploration-after10.json`：exploration 字段实测
- `userC-assertions.txt`：用户 C Finding 明细与计划 sourceFindingIds
- `trigger-and-test-evidence.txt`：日检触发日志 + 定向单测结果（9/9）
- `free-expr-results.jsonl`、`sentence-results.jsonl`：真实 LLM 评分回执
- `api-boot1/2/3.log`：API 全量日志（boot1 含迁移建库；boot2 触发 A/B；boot3 触发 C、A/B 未重复）

---

# 复验（周密，2026-08-06）

- 对象：同一 worktree，commit 34dd577（首验 38fbc05 之后修订，工作树干净无未提交改动）
- 环境：API Development @5194，独立库 `nextword_qa_t032b`（验毕已 DROP）；定向单测隔离库 `nextword_qa_t032b_unit`（验毕已 DROP）；LLM DashScope qwen-plus 真实调用
- 注：本目录 `rv-*`/`api-rv-boot*.log` 为 07-31 一次未收尾的复验尝试遗留（未出结论），本轮全部重新实测，证据以 `rv2-*` 为准

## 复验结论：通过 ✅

两项首验阻断均在真实链路实测修复，首验已通过项抽查无回退。T-032 可标 done。

## 阻断 1（FreeExpressionLogs 对画像不可见）— ✅ 已修复，真实链路实证

- 纯表达用户（rv2-expr）：注册 → 跳过初测 → 真实 qwen-plus 刷 10 条自由表达（aiScore 35–70，rv2-free-expr.jsonl）→ 重启触发日检（boot2）→ 日志 `Cold-start profile regeneration triggered for 1 users`（api-rv2-boot2.log）。
- 画像 `weakness-profile-coldstart` 落库，**3 条 Finding 全部 Verified**（rv2-expr-findings.txt）：
  - grammar(High)、vocabulary(Medium) 证据均为 `free_expression_log` 引用（refId=自由表达留痕 Id、metric=aiscore，数值与库内一致）；
  - reading(Low) 带「**初步判断：证据真实、数值一致，样本量不足…冷启动放宽档置信下调为 low**」——放宽档 low 初步判断在真实链路首次实测出现（首验非阻断 7 的空白补齐）。
- Finding 语义贴合输入（主谓一致/时态错误、词汇简单——与刻意植入的病句一致）。
- force Planner 入队并 Completed（`planner:coldstart:{uid}:20260806`）；新计划 **`sourceFindingIds:[1,3]` 非空**，探索期死锁在表达-only 链路上实破。

## 阻断 2（Skill 维 Finding 不进 sourceFindingIds）— ✅ 已修复，真实链路实证

- 造句用户（rv2-sent，首验用户 C 同剧本）：10 条造句真实评分（rv2-sentences.jsonl）→ boot3 日检触发 → 画像 4 条 Skill 维 Weakness Finding 全 Verified（rv2-sent-findings.txt）。
- 新计划 **`sourceFindingIds:[4,5,6,7]`**——技能 Finding id 全数计入（首验同场景为 `[]`）。
- 主攻场景选择逻辑不变：两用户均无场景维 Finding，`focusScenarios` 同为 `["agree_disagree","daily_routine"]` 覆盖率兜底 ✓（单测 `Generate_marks_plan_personal_with_skill_only_verified_findings` 同断言）。

## Verifier 对 free_expression_log 纪律（真实链路 + 定向单测）

- 定向单测 18/18 通过（rv2-targeted-tests.txt），含新增 `Verifier_checks_free_expression_log_evidence_with_same_discipline`：真实引用 Verified；**伪造 refId → Questioned「不存在」；篡改数值 → Questioned「不属实」；未知指标 → Questioned「未知指标」**；放宽档条数不足同步降 low 注「初步判断」。
- 越权归属：预载查询按 `UserId` 过滤（FindingVerifier.cs:43-46），他人记录与伪造同路径判「不存在或不属于该用户」。
- diff 复审确认数值核查同源同纪律：`aiscore` 走统一 `CheckValue`（容差/比较符一致），metric 入 switch 前统一 `ToLowerInvariant`，与 prompt 的 `aiScore` 写法自洽。

## 首验已通过项抽查（不回退）

- 探索周进度：注册当天 `active:true, day:1/7, evidenceCount:0, remaining:10`（rv2-exploration-day1.json）；刷满 10 条后 `evidenceCount:10, remaining:0`（rv2-exploration-after10.json）✓。前端文案本轮 fix diff 零前端文件（9 文件全后端+文档），无回退面。
- 幂等仅一次：boot3 仅造句用户触发，表达用户无第二份画像、无重复 planner 任务（rv2-db-final-state.txt：profiles=2、jobs=2 均 Completed）✓；`Trigger_fires_only_once_and_distinguishes_bottleneck_regens` 在 18/18 内。
- Verifier 默认档纪律：`Verifier_relaxed_downgrades_thin_evidence_but_keeps_mechanical_checks`（默认档同草稿仍 Questioned）在 18/18 内 ✓。

## 修复增量 diff 复审（38fbc05→34dd577，+221/-10）

最小改动确认：WeaknessProfiler 增 FreeExpressionLogs 聚合（最近 30 条）、LlmPromptFactory 增证据段落与引用规则、FindingVerifier 增 free_expression_log 分支、LearningPlanService 的 sourceFindingIds 追加 Verified 技能维 Weakness（主攻场景逻辑未动）、单测 +5 例、文档同步。无顺手重构、无越界改动。

## 遗留不足（分级，均为首验已记录项的延续）

- **非阻断（沿用首验 3/4/5，本次 fix 未涉及，仍建议后续迭代处理）**：同日幂等口径不区分 ModelProfileId；触发循环无单用户 try/catch 容错；多实例并发无防重。
- **非阻断（观察）**：种子 demo 用户（11111111-…）零证据注册当天不触发，行为正确；存量老用户批量触发的一次性 LLM 成本仍建议上线知会运维（首验 6）。

## 复验证据文件（本目录，rv2- 前缀）

- `rv2-exploration-day1.json`、`rv2-exploration-after10.json`：探索周进度实测
- `rv2-free-expr.jsonl`、`rv2-sentences.jsonl`：真实 LLM 评分回执（10+10）
- `rv2-expr-findings.txt`、`rv2-sent-findings.txt`：两用户 Finding 明细（含 VerificationNote）与计划 sourceFindingIds/focusScenarios
- `rv2-db-final-state.txt`：终态计数（用户/画像/Finding 7 Verified 0 Questioned/计划/任务/留痕）
- `rv2-targeted-tests.txt`：定向单测 18/18 逐条名单
- `api-rv2-boot1/2/3.log`：API 全量日志（boot2 触发表达用户、boot3 触发送句用户且表达用户未重复）
