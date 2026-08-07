# T-039 I7 集成仿真复验 · 第二轮（周密）

> 日期：2026-08-07 ｜ 被验：主仓库 main @ 96be950（含 T-044 毕业口径放宽 + T-045 画像重生成误杀修复）
> 环境：主仓库源码旁路构建（`report/qa-t039/build2/`，:5108 旧实例锁 bin 不动）→ API Development @5190；独立库 `nextword_qa_t039b`（已 DROP）；LLM DashScope qwen-plus 真实评分
> 仿真：`round2/sim.py`（复制自首轮 `qa-t039/sim.py`，仅改库名→nextword_qa_t039b，口径不变）；全程约 19 分钟（00:16:43–00:35:03），30/30 天无中止
> 数据：`round2/data/`（day-log.jsonl 30 天、anomalies.log 9 条、final-state.json、run.log、checkpoint.json、probe-c-grade.json、probe-d-grade.json）、`round2/api.log`、`round2/probe_c_grade.py`

## 复验结论：**通过（带保留）**（4 项断言：2 全过 / 1 探针实证通过但自然发生未达成 / 1 口径偏差非回退）

两项重点修复均实证生效：

- **T-045 完全收复**：月中洞察触发画像重生成后，新画像 6 条 findings = **5 Verified + 1 Questioned**（首轮 4/5 Questioned 全灭），计划 sourceFindingIds 切换为新画像 findings 且**个性化徽章全程保持到第 30 天**，首轮「第 12 天起退化探索期」不再复现；
- **T-044 通路双向实证**：实弹探针证明 **C 档 + 含池词 → 毕业**（`therefore` 毕业留痕），**D 档 + 含池词 → 不毕业**（防烂底线不动）。

保留项：30 天仿真内**自然毕业仍 0 次**——评级闸本轮已全程放行（12 篇自由表达 10B+2C 全部满足 T-044 口径），残余 blocker 是**命中闸**（12 文本 × 20 池词 substring 命中 0，SQL 交叉验证），即首轮 P1 的「池词短语化 × 菜鸟日常话题」半边未收口，维持 P1 登记（属产品/开发共议项，不在 T-044 修复范围）。

## 复验断言逐项证据

### 1. 毕业通路（T-044）— ⚠️ 探针实证通过；自然发生未达成（P1 残余）

- **自然毕业 0**：30 天 `graduatedWords` 全程空；期末（探针前）四阶段分布 `Recognized 170 / PromptedUse 20 / SpontaneousUse 0`（含少量 Recalled 中途流转）。首轮同锚点连续第三轮 0 自然毕业。
- **评级闸已放行**：12 篇自由表达 10 篇 B（aiScore 60-80）+ 2 篇 C（55）——按 T-044 口径（C 及以上且词汇维 ≥3）**12/12 进入毕业判定**（首轮 12 篇全 C/D 时判定代码零执行；本轮判定代码每次都执行，是实质改善）。
- **残余 blocker = 命中闸**：12 篇文本 × 20 个 PromptedUse 池词（nevertheless / therefore / in a nutshell / see eye to eye / a lot … 多为 Intermediate 多词短语）按 T-040 TargetWordMatcher 口径交叉验证命中数 = **0**。菜鸟日常话题文本结构性不含池词，与首轮 P1 结论一致。
- **实弹探针（正）**：构造 C 档文本（"Yesterday I miss the bus, **therefore** I was late for work…"，缺冠词/主谓不一致错误若干）→ 响应 `overallGrade=C, aiScore=60, graduatedWords=["therefore"]`；DB 留痕 `therefore → SpontaneousUse`，`GraduatedFreeExpressionLogId → af23ea6d`（C 档 log）。（`data/probe-c-grade.json`）
- **实弹探针（负）**：D 档烂文（aiScore 30）含 `afford` ×3 → `graduatedWords=[]`，`afford` 仍 PromptedUse——**D 档不毕业底线不动**。（`data/probe-d-grade.json`）
- 首毕业日：仿真内无自然毕业；探针毕业发生于仿真期末后（真实时间 08-07，对期末分布 +1 SpontaneousUse）。

### 2. 画像重生成不再全灭（T-045）— ✅ 通过

- 第 17 天洞察触发：`MonotonousExpression`（signals=plateau，replan=True）→ 画像重生成（Profile Id=2，`AssessmentId=NULL`）+ force Planner 链路走通。
- **新画像 6 条 findings：5 Verified + 1 Questioned**：
  - Verified ×5：冠词系统性缺失 / 正式连接副词语域误用 / 高频偏离目标场景 / 功能动词短语搭配不牢 / 查词频次偏高（reading）；
  - Questioned ×1：「场景词汇覆盖率 ≤9%」——核验注「引用数值不属实：coverage 实际 0.11」，属**核验器正常履职**，非误杀；
  - 首轮的两条「测评 FinalLevel 记录缺失」**本轮 0 条**——T-045 的 assessmentId 空回退最近测评记录修复生效。
- **计划侧不退化**：计划 4（07-25）与计划 5（08-01）`sourceFindingIds=[6,7,8,10]`（新画像 finding Id）；sim 侧徽章第 1–30 天**全程个性化**（首轮第 12 天起全程探索期）。
- 第 10/24 天筛查未触发（信号阈值未达，口径内）；洞察总计 1 次，满足 ≥1。

### 3. 首轮已过项抽查 — 基本不回退，1 项口径偏差

- **定级：本轮 B2（表达力 71，词汇/阅读参考 67/67）——超过 ≤B1 口径，判口径偏差而非代码回退**：测评代码两轮间无变更（96be950 仅触 FindingVerifier/FreeExpression），偏差来自 qwen 评分波动（本轮菜鸟作答句质量偏高，对照 anomalies 9 条评分偏宽）；防伪闸方向为「表达虚高」，本轮表达 71 与参考 67 档差 4 分未达矫正阈值（`originalLevelBeforeGuard` 场景未触发，同首轮）。**分数权威自愈可见**：第 26 天 Overall 跌至 69，OverallLevel 随 ScoreKernel 回落 B1（`SyncLegacyLevels`，不写 LevelHistory 属既有口径），期末 B1/Overall 60。
- **词池带一致 ✅**：5 份计划 wordIds 100% Intermediate（56/56、20/20、56/56×3，SQL JOIN 验证），与 B2 定级带一致；sentenceTargets 除计划 3 有 2 个 Basic（带内相邻，同首轮 A2/B1 混编口径）外全 Intermediate。无越带词。
- **洞察 ≥1 ✅**：1 次（第 17 天，见断言 2）。
- **分数曲线非水平线 ✅**：Writing 71→79（峰，第 8 天）→60（期末），区间 19 分真实波动；Overall 71→60 联动。V/R 全程 71 死平线 = 首轮 P2-2 既有缺口（无回写源，本轮 LearningEvents 仍仅 SentencePractice×81 + FreeExpressionPractice×14 + AssessmentCompleted×1），T-047 已在开发，不算回退。

### 4. 异常记录 — 9 条全部可解释，无链路故障

- `anomalies.log` 9 条 = 菜鸟句拿 A ×6 + 四维满分 ×3，均为 qwen 生成「流畅短句 + 正确嵌入目标短语」（that rings true / that said / technically / up in arms / yeah right）——同首轮 **P3-1**（T-027 压分口径对「相称句」偏宽），无新增类型。
- **无 http-fail、无 day-abort、无 sql-fail、无 planner/insight 超时**；本轮未复现首轮 learning-judge 的 sim 侧用例缺陷（P3-2）。

## 新问题分级（相对首轮增量）

| 级 | 问题 | 证据与说明 |
|---|---|---|
| **P1（沿用，未收口）** | 30 天自然毕业连续三轮 0：评级闸已被 T-044 放行，残余为命中闸——产出池多词短语化 × 菜鸟日常话题文本 0 命中 | 本报告断言 1；首轮 P1 的后半。建议下轮立项：Planner 编排考虑自由表达可命中性（掺入可操作单词），或毕业命中口径再议 |
| **P2（新）** | 定级结果对 LLM 评分波动敏感：同人设同代码首轮 B1、本轮 B2；防伪闸对小幅档差（4 分）不介入 | checkpoint `assessment.final`（expressionScore=71 vs 参考 67）；分数权威第 26 天自愈回 B1。建议：测评产出题评分加稳定性约束（多题汇总/锚定样例），或闸阈值复审 |
| **P2-2（沿用）** | Vocabulary/Reading 无回写源，30 天死平线 | LearningEvents 仅 3 类；T-047 开发中 |
| **P3-1（沿用）** | 「自然嵌入短语的相称句」仍可拿 A/四维满分 | anomalies 9 例 |
| **P3-2（未复现）** | sim 侧 typo_of 双写词用例缺陷 | 本轮 0 例 |

## 收尾确认

- API 进程已杀、`nextword_qa_t039b` 已 DROP、`build2/` 旁路构建产物已删；:5108 旧实例全程未动（含其 `dotnet run` 残留进程，非本轮产物）。
- 归档：`round2/sim.py`、`round2/probe_c_grade.py`、`round2/data/`（day-log / anomalies / final-state / run.log / checkpoint / probe-c-grade / probe-d-grade）、`round2/api.log`。
