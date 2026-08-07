# T-039 I7 集成仿真终验（周密）

> 日期：2026-08-06 ｜ 被验：主仓库 main @ 23f2f68（含 T-022/23/27/32/33/40/42 全链）
> 环境：主仓库源码旁路构建（`report/qa-t039/build/`，因 :5108 旧实例锁定 bin 无法原地 build）→ API Development @5190；独立库 `nextword_qa_t039`（已 DROP）；LLM DashScope qwen-plus 真实评分
> 仿真：`report/qa-t039/sim.py`（复制自 `report/sim-month/sim.py`，仅改 API base→5190、库名→nextword_qa_t039，30 天/菜鸟人设/时间回拨/挑战·筛查节奏口径全保持）；全程 17.5 分钟，30/30 天无中止
> 数据：`data/day-log.jsonl`（30 天）、`data/anomalies.log`（12 条）、`data/final-state.json`、`data/run.log`、`api.log`

## 终验结论：**不通过**（6 项断言 3 通过 / 1 口径内通过 / 2 未通过）

整轮修复叠加效果真实可见：定级不再虚高（B1 全程）、计划词池 100% 带内、短语确认通路修复、PromptedUse 1→15、洞察信号 v2 三类全触发、Writing 分数有真实曲线。但**「毕业 ≥3 / 14 天首毕业」再次 0 达成**（T-034 同一锚点连续两轮不过），且**洞察重生成画像后计划个性化徽章退化为探索期**（新发现 P2）。

## 逐日关键指标

| 日 | 日期 | 等级 | V/R/W/O | 词关系 | Recog | Recalled | PromptedUse | Spont | 计划徽标 | 造句 | 自由表达 | 挑战 | 洞察 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 07-08 | B1 | 60/60/60/60 | 0 | 0 | 0 | 0 | 0 | 个性化 | - | - | - | - |
| 2 | 07-09 | B1 | 60/60/62/60 | 15 | 15 | 0 | 0 | 0 | 个性化 | B,C,C | - | - | - |
| 3 | 07-10 | B1 | 60/60/65/60 | 25 | 25 | 0 | 0 | 0 | 个性化 | B,A,A | C | - | - |
| 4 | 07-11 | B1 | 60/60/70/60 | 35 | 35 | 0 | 0 | 0 | 个性化 | A,B,A | - | - | - |
| 5 | 07-12 | B1 | 60/60/68/60 | 43 | 41 | 2 | 0 | 0 | 个性化 | A,C,C | D | - | - |
| 6 | 07-13 | B1 | 60/60/72/60 | 51 | 49 | 1 | 1 | 0 | 个性化 | A,B,B | - | - | - |
| 7 | 07-14 | B1 | 60/60/71/60 | 59 | 56 | 1 | 2 | 0 | 个性化 | B,C,B | C | 78.3过 | - |
| 8 | 07-15 | B1 | 60/60/73/60 | 67 | 63 | 1 | 3 | 0 | 个性化 | C,B,B | - | - | - |
| 9 | 07-16 | B1 | 60/60/67/60 | 74 | 69 | 2 | 3 | 0 | 个性化 | C,B,C | C | - | - |
| 10 | 07-17 | B1 | 60/60/67/60 | 82 | 77 | 1 | 4 | 0 | 个性化 | B,C,C | - | - | GrammarErrors(plateau)⤾replan |
| 11 | 07-18 | B1 | 60/60/64/60 | 89 | 82 | 2 | 5 | 0 | 个性化 | B,C,C | C | - | - |
| 12 | 07-19 | B1 | 60/60/58/58 | 97 | 90 | 1 | 6 | 0 | 探索期 | C,D,D | - | - | - |
| 13 | 07-20 | B1 | 60/60/58/58 | 97 | 90 | 1 | 6 | 0 | 探索期 | - | - | - | - |
| 14 | 07-21 | B1 | 60/60/59/59 | 105 | 97 | 2 | 6 | 0 | 探索期 | C,B,C | - | 40败 | - |
| 15 | 07-22 | B1 | 60/60/58/58 | 113 | 105 | 2 | 6 | 0 | 探索期 | C,B,B | C | - | - |
| 16 | 07-23 | B1 | 60/60/61/60 | 120 | 111 | 1 | 8 | 0 | 探索期 | A,B,B | - | - | - |
| 17 | 07-24 | B1 | 60/60/64/60 | 129 | 120 | 0 | 9 | 0 | 探索期 | B,B,B | C | - | GrammarErrors(avoidance,safe_word) |
| 18 | 07-25 | B1 | 60/60/66/60 | 137 | 126 | 2 | 9 | 0 | 探索期 | C,B,B | - | - | - |
| 19 | 07-26 | B1 | 60/60/60/60 | 145 | 134 | 4 | 7 | 0 | 探索期 | C,C,D | D | - | - |
| 20 | 07-27 | B1 | 60/60/60/60 | 151 | 140 | 1 | 10 | 0 | 探索期 | B,C,C | - | - | - |
| 21 | 07-28 | B1 | 60/60/62/60 | 160 | 149 | 0 | 11 | 0 | 探索期 | B,C,B | D | 66.7败 | - |
| 22 | 07-29 | B1 | 60/60/64/60 | 169 | 157 | 1 | 11 | 0 | 探索期 | C,C,A | - | - | - |
| 23 | 07-30 | B1 | 60/60/64/60 | 169 | 157 | 1 | 11 | 0 | 探索期 | - | - | - | - |
| 24 | 07-31 | B1 | 60/60/63/60 | 177 | 164 | 1 | 12 | 0 | 探索期 | C,C,C | - | - | GrammarErrors(plateau,avoidance,safe_word) |
| 25 | 08-01 | B1 | 60/60/60/60 | 186 | 173 | 0 | 13 | 0 | 探索期 | C,C,B | D | - | - |
| 26 | 08-02 | B1 | 60/60/56/56 | 194 | 180 | 3 | 11 | 0 | 探索期 | C,C,C | - | - | - |
| 27 | 08-03 | B1 | 60/60/57/57 | 201 | 187 | 0 | 14 | 0 | 探索期 | C,B,C | C | - | - |
| 28 | 08-04 | B1 | 60/60/57/57 | 210 | 195 | 1 | 14 | 0 | 探索期 | C,C,C | - | 60败 | - |
| 29 | 08-05 | B1 | 60/60/57/57 | 219 | 204 | 0 | 15 | 0 | 探索期 | B,C,B | D | - | - |
| 30 | 08-06 | B1 | 60/60/60/60 | 228 | 212 | 1 | 15 | 0 | 探索期 | B,A,C | - | - | - |

## 六项断言逐项核验

### 1. 定级 ≤B1（T-023/42）— ✅ 通过

- 首次测评：A2 块→B1 块收敛，表达力 60 → **定级 B1**；词汇识别参考 100（C1）、阅读参考 50（B1）（`checkpoint.json.assessment.final`）。
- 防伪闸未触发属**预期**：档差方向为「识别(C1) > 表达(B1)」，闸只矫正「表达虚高」方向，反向不矫正（`originalLevelBeforeGuard=null`，T-042 单测口径一致）。本轮无矫正传导可验场景（闸未触发），T-042 P1 缺口（Planner 按矫正前档取词）**无复现条件，不算回归覆盖**。
- 词池带核验（矫正传导的零号断言）：5 份计划 `wordIds` **全部 B1**（56 词/份 × 5，SQL 直接 JOIN 验证）；`sentenceTargets` 无 A2/B1 之外词。**无「B2 习语给 B1 用户」的计划级违规**。
- 30 天 `overallLevel` 恒为 B1，`levelHistory` 仅 Initial A1→B1 一条，无异常跳档；`isUpgradeCandidate=false`。
- 观察：产出池出现 1 个 B2 词 `that rings true`、造句留痕另有 4 个 B2 词（largely/in brief/literally/in a nutshell），**全部落在每周挑战日**（attemptedLevel=B2，升带尝试属设计内），非计划编排。

### 2. 探索周生效（T-032）— ❌ 未通过（断言口径未达成，但非功能回退）

- 断言要求「sourceFindingIds 从空变非空（探索期→个性化）」，实测**反向**：计划 1-2（第 1/8 天）`sourceFindingIds=[1,2,3,4]`（个性化），计划 3-5（第 12 天起）为空（探索期）。
- 根因（代码级）：
  1. **做过初测的用户首日即个性化**：测评链路生成画像带 4 条 Verified Skill Weakness findings（grammar/vocabulary/natural/expressionScore），T-032 修复后 Skill 维计入计划来源（`LearningPlanService.cs:115-126`）——探索期徽章对「初测用户」本就不适用，断言口径与人设路径错配；
  2. **冷启动重生成在仿真内结构性不可触发**：唯一触发点是 `ProfileScoreSnapshotWorker`（24h 日检，`ProfileScoreSnapshotWorker.cs:122-128`），17 分钟仿真等不到（T-032 验收当时用重启日检单独验证过，不在本仿真覆盖范围）；
  3. **第 12 天起退化为探索期的原因**：第 10 天洞察（replan=True）触发画像重生成，新画像 5 条 findings 中 4 条 Questioned（2 条「测评 FinalLevel 记录缺失」、2 条「引用数值不属实」），Planner 只消费 Verified findings → 来源标记诚实置空。见「新发现 P2-1」。
- Dashboard exploration 进度：终态 `exploration={active:false, day:0, totalDays:0}` 在第 30 天正确；首周进度数据 sim 未采集该字段（harness 缺口，qa-t032 已实测 endpoint 合理）。

### 3. 洞察 ≥1 次触发（T-033）— ✅ 通过

- **3 次触发**：第 10 天 `plateau` → GrammarErrors，replan=True；第 17 天 `avoidance + safe_word`；第 24 天 `plateau + avoidance + safe_word`（信号 v2 三类全部出现）。
- 定性合理：nature 恒为 GrammarErrors，与菜鸟人设（缺冠词/主谓一致/时态）吻合；statement 引用真实错例（"she speak"、"go school"、give away 未变过去式），evidenceLogIds 5 条留痕齐全。
- 首次洞察触发 replan → 画像重生成 + force Planner 链路实测走通（次日新计划落地）。

### 4. 生命周期（T-034/40）— ❌ 未通过（同一锚点连续第二轮不过）

- **PromptedUse 期末 15 ≫ 基线 1** ✅，首个第 6 天出现 ✅（基线：30 天仅 1 个、第 27 天前后）。
- **T-040 短语确认通路实证修复**：15 个 PromptedUse 词中 6 个已 confirmed，含多词短语 `on the other hand`/`keep up`/`no wonder`/`on schedule`/`a little further`（qa-t034 时多词确认 10/10 全死）。
- **毕业 0（要求 ≥3）✗；14 天内首毕业 ✗；graduatedWords 全程空 ✗**。
- 根因（代码级，双闸门叠加）：
  1. **评级闸**：`FreeExpressionService.cs:67` 要求整篇自由表达 A/B 才进入毕业判定；菜鸟 12 篇全部 C/D（aiScore 25-45）——**该人设下判定代码一次都没执行到**；
  2. **命中闸**：即使放开评级闸，12 篇文本 × 15 个池词 substring 命中数为 **0**（SQL 交叉验证）——产出池 2/3 是多词短语（on the other hand / as soon as / come up…），菜鸟日常话题文本（吃饭/上学/运动）结构性不会自发使用。
- 即：T-034 验收时的三根因中，T-041（定级虚高→B2 习语池）已被 T-042 消除、T-040（短语确认死路）已修复，但「整篇 A/B 评级闸 × 池词短语化」这一层在本轮浮出为毕业通路的**主矛盾**。需产品+开发共议：毕业判定改「句级正确使用」口径，或 Planner 编排时考虑自由表达可命中性（登记新任务，见 P1）。

### 5. 分数曲线有趋势（T-022）— ✅ 通过（口径内，附 P2 观察）

- `scores/history` 30 天**非水平线**：Writing 60→73（峰，第 8 天）→56（谷，第 26 天）→60，Overall 56-60 联动——T-022 验收口径（造句/自由表达回写 Writing 维）在整月尺度成立，方向随真实评级波动（第 12 天 D,D 两句当日 Writing 58、第 19-29 天自由表达 4 个 D 对应 Writing 低位）。
- **P2 观察**：Vocabulary/Reading **全程 60 绝对水平线**。`LearningEvents` 全库仅 SentencePractice×81 + FreeExpressionPractice×12 + AssessmentCompleted×1——背词/拼写/阅读/挑战均无任何 Score 回写源（T-022 设计口径本就只写 Writing）。月度时间轴（T-031/36）若展示 V/R 曲线将是两条死平线，建议随 T-035（挑战有结果化）一并定义 V/R 回写口径。

### 6. 异常记录 — 12 条全部可解释，无 HTTP/链路故障

- `anomalies.log` 12 条 = 菜鸟句拿 A ×8 + 四维满分 ×4 + learning-judge ×1；**无 http-fail、无 day-abort、无 planner/insight 超时**。
- 拿 A 的句子均为 qwen 生成的「流畅短句 + 正确嵌入目标短语」（如 `Opera's just not my thing.`）。T-027 挑战度约束的是「安全简单句」，对「自然使用目标短语的相称句」仍给满——4 例四维满分说明压分口径对这类句子偏宽，记 **P3 观察**。
- learning-judge ×1 为 **sim 侧用例缺陷**（非产品问题）：`typo_of` swap 模式对双写词 `keep up` 交换两个相同字母 'ee' 产生原词，sim 预期错但答案本身正确，服务端判对无误。

## 新发现分级

| 级 | 问题 | 证据与根因 |
|---|---|---|
| **P1** | 毕业通路对菜鸟人设结构性不可达：毕业 ≥3 / 14 天首毕业连续两轮（T-034、T-039）0 达成 | `FreeExpressionService.cs:67` 整篇 A/B 闸 + 产出池多词短语化致文本命中为 0（本报告断言 4）。建议下轮立项：毕业判定口径改句级 / 池编排考虑可命中性 |
| **P2-1** | 洞察触发画像重生成丢失测评上下文，Skill findings 永不得 Verified → 计划个性化徽章退化为探索期（第 12 天起至月末） | `BottleneckInsightService.cs:98` 调 `GenerateAsync(userId, null, …)` 不传 assessmentId → `FindingVerifier.cs:183-188` assessment_dimension 类证据核验「测评 FinalLevel 记录缺失」→ Questioned。修复方向：重生成时带用户最近完成测评的 assessmentId |
| **P2-2** | Vocabulary/Reading 分数无回写源，30 天死平线 | `LearningEvents` 仅 3 类事件（见断言 5）；建议随 T-035 定义挑战/背词/阅读回写口径 |
| **P3-1** | 「自然嵌入短语的相称句」仍可四维满分，T-027 压分口径覆盖不全 | anomalies 4 例（not my thing / keep up / pop by / break in） |
| **P3-2** | sim 侧 `typo_of` 对含双写字母单词可产生未变答案（用例缺陷，非产品） | anomalies learning-judge 1 例 |

## 收尾确认

- API 进程已杀、`nextword_qa_t039` 已 DROP（见会话记录）；:5108 旧实例全程未动。
- 归档：`sim.py`（复制件）、`data/`（day-log/anomalies/final-state/run.log/checkpoint）、`api.log`；`build/` 旁路构建产物已删除（非证据，约 200MB）。
