# T-034 验收证据（周密，2026-07-31）

- 对象：worktree `.worktrees/t-034-lifecycle-accel`，分支 feat/t-034-lifecycle-accel，commit 11478ee
- 环境：API :5195（Development，--no-launch-profile）+ 独立库 nextword_qa_t034（验完已 DROP）+ DashScope qwen-plus 真实 LLM
- 仿真器：report/sim-month/sim.py 复制件（report/qa-t034/sim/sim.py，仅改库名指向 QA 库 + 加 graduatedWords 探针），30 天完整跑完（30/30 天日志齐，final-state.json 已采集）
- 未重跑全量 dotnet test（另有任务并行占用测试库；开发自报 168+6 绿，单测 diff 已逐条审查）

## 一、diff 审查结论（commit 11478ee，18 文件 +417/-38）

最小改动成立，无无关改动。逐文件核对：

- `DailyWordSelectionService`：`RecallExamQuotaRatio = 0.4` 常量；Plan 与回退两条路径都先填回忆考察池（`ceil(count×0.4)` 名额、StageUpdatedAt 最早优先），不足新词补位；回退路径已在薄弱位的成熟词计入配额且去重。Plan 词源已排除已学词（`!learnedIds.Contains`），池词与 Plan 词不会重复——Plan 路径无需去重，实现正确。
- `LearningPlanService`：造句目标 `prompted_use 未确认池 → Recalled 池 → 当日带内词`，两级池共用 `FilterInBandLemmas`（带内、utility 非 low、最早优先），口径与既有候选池一致。
- `FreeExpressionService`/端点：毕业判定原逻辑未动，仅把毕业词列表带上响应；`graduatedWords` 为可选字段向后兼容。
- `WordEndpoints`：新增 `GET /api/words/graduated`（当前用户 spontaneous_use + 毕业时间）。
- 前端：`useGraduations` 一处接口三处展示（自由表达结果区提示 / Dashboard 本周计数无则不显示 / `/word-bank` 行内「已毕业」徽标），失败静默降级。
- 毕业标准与四阶段状态机（`WordLifecycleService`）零改动，diff 中无实体变更，无迁移——符合约束。
- 测试净增 4 例全部打在真实 PG 上，覆盖配额/补位/Plan 配额/二级补位；既有毕业测试补 graduatedWords 断言。
- 文档（development-log / CURRENT-STATE / tasks.csv）同步更新。

## 二、口径修正裁定：**成立**

设计 §2.1 字面「recognized 且 RepeatCount≥2，recall 模式」在 T-014 状态机下不可运行，开发修正为「recalled 阶段 ∪ 认识且 RepeatCount≥2 残留词，考察模式按阶段派生」。独立核对 `WordLifecycleService.ApplyReview`：

1. 认识词答对且 `RepeatCount≥2`（SM-2 成熟）的**那次认识考察即升 recalled**——所以「成熟待推进老词」的稳态位置就是 recalled 阶段，池主体取 recalled 词正确；
2. recall 模式考察**认识阶段**词不推进任何阶段（`mode==Recall` 仅对 `Recalled` 生效）——若按设计字面对认识词强行 recall 考察，答对也不升段，考察位白给。在「状态机一行不改」的硬约束下，考察模式按阶段派生（认识残留词用认识模式，答对即升 recalled）是唯一自洽口径；
3. 「认识且 RepeatCount≥2 残留词」（答错但自评 Remembered 的边界态）纳入考察池是安全网，仿真中实测该路径存在（单测 `Daily_words_fill_new_words_when_mature_pool_insufficient` 覆盖）；
4. §4.1「recall 模式占比 ≥40%」按池主体（recalled 词）达成——仿真实测回忆考察位数量始终等于当日成熟池大小（池 1-3 个时 recall 考察 1-3 个，其余新词补位），与「保底配额、不足补位」口径一致。

## 三、30 天仿真逐日生命周期轨迹（核心证据）

| 日 | 日期 | Recog | Recalled | PromptedUse | Spont | 自由表达评级 | Overall/CEFR |
|---|---|---|---|---|---|---|---|
| 1 | 07-02 | 0 | 0 | 0 | 0 | – | 75/B2（定级） |
| 2 | 07-03 | 15 | 0 | 0 | 0 | – | 75/B2 |
| 3 | 07-04 | 23 | 0 | 0 | 0 | C | 75/B2 |
| 4 | 07-05 | 31 | 0 | 0 | 0 | – | 75/B2 |
| 5 | 07-06 | 37 | 2 | 0 | 0 | C | 75/B2 |
| **6** | 07-07 | 44 | 0 | **2** | 0 | – | 75/B2 |
| 7 | 07-08 | 51 | 1 | 2 | 0 | C | 69/B1 |
| 8 | 07-09 | 58 | 1 | 3 | 0 | – | 65/B1 |
| 9 | 07-10 | 64 | 2 | 3 | 0 | C | 65/B1 |
| 10 | 07-11 | 72 | 1 | 4 | 0 | – | 68/B1 |
| 11 | 07-12 | 77 | 3 | 4 | 0 | D | 70/B2 |
| 12 | 07-13 | 84 | 1 | 6 | 0 | – | 72/B2 |
| 13 | 07-14 | 84 | 1 | 6 | 0 | –（休） | 72/B2 |
| 14 | 07-15 | 91 | 2 | 6 | 0 | – | 71/B2 |
| 15 | 07-16 | 99 | 3 | 5 | 0 | C | 67/B1 |
| 16 | 07-17 | 106 | 1 | 7 | 0 | – | 69/B1 |
| 17 | 07-18 | 113 | 0 | 8 | 0 | C | 72/B2 |
| 18 | 07-19 | 113 | 2 | 8 | 0 | – | 75/B2 |
| 19 | 07-20 | 121 | 2 | 8 | 0 | C | 73/B2 |
| 20 | 07-21 | 129 | 0 | 10 | 0 | – | 75/B2 |
| 21 | 07-22 | 136 | 1 | 10 | 0 | C | 70/B2 |
| 22 | 07-23 | 144 | 0 | 11 | 0 | – | 74/B2 |
| 23 | 07-24 | 144 | 0 | 11 | 0 | –（休） | 74/B2 |
| 24 | 07-25 | 152 | 0 | 11 | 0 | – | 74/B2 |
| 25 | 07-26 | 160 | 0 | 11 | 0 | C | 75/B2 |
| 26 | 07-27 | 165 | 2 | 11 | 0 | – | 73/B2 |
| 27 | 07-28 | 173 | 0 | 13 | 0 | **B** | 68/B1 |
| 28 | 07-29 | 181 | 0 | 13 | 0 | – | 71/B2 |
| 29 | 07-30 | 189 | 0 | 13 | 0 | C | 70/B2 |
| 30 | 07-31 | 196 | 1 | 13 | 0 | – | 73/B2 |

- **首个 Recalled：第 5 天；首个 PromptedUse：第 6 天**（旧基线：30 天仅 1 个，第 27 天前后）；
- **期末分布：Recognized 196 / Recalled 1 / PromptedUse 13 / SpontaneousUse 0**（旧基线：185 词 PromptedUse 1、毕业 0）；
- **首个毕业词：30 天内未出现**；graduatedWords 探针全程为空（12 篇自由表达评级 C×10、D×1、B×1，唯一 B（第 27 天）文本不含产出池词）。

## 四、验收标准逐条判定（设计 §4）

| # | 标准 | 判定 | 证据 |
|---|---|---|---|
| 1 | 队列配额 ≥40%（两条路径）+ 不足补位不报错 | **通过** | 单测 diff 审查（3 例：回退配额/补位/Plan 配额）+ 仿真实测：recall 考察位数 == 当日成熟池大小，池空时全新词不报错（首日 10/10 新词） |
| 2 | Planner 二级补位 Recalled 池最早优先 | **通过** | 单测 `Planner_backfills_sentence_targets_from_recalled_pool`（最早优先、超带与 utility low 不进池、兜底带内词）；仿真中 PromptedUse 池持续有词进入（Recalled→PromptedUse 通路靠配额考察 + 补位编排共同驱动，第 6 天即通） |
| 3 | 毕业提示（graduatedWords/前端/Dashboard/词库） | **通过（实弹验证）** | 仿真后在活动库上实弹：B 档自由表达含池词 → 响应 `graduatedWords:["largely","disappointed"]`，第二篇 → `["counters"]`；三词阶段翻 SpontaneousUse、MasteryScore=100、留痕 LogId；`GET /api/words/graduated` 返回 3 词含 graduatedAt（Dashboard 计数与词库标记的数据源）。前端三处展示代码审查到位 |
| 4 | 仿真：PromptedUse 显著上升、毕业 ≥3、14 天内首毕业 | **未通过（部分达成）** | PromptedUse 1→13 显著上升 ✓；**毕业 0（要求 ≥3）✗；14 天内首毕业 ✗** |
| 5 | T-014 状态机零回归 | **通过** | diff 确认状态机零改动；开发 dotnet test 168+6 全绿（未复跑，并行任务占用测试库） |

## 五、毕业 0 的根因分析（三因素叠加，均非 T-034 机制本身）

1. **定级虚高（联动 T-023 口径，主因）**：菜鸟剧本定级表达力 75 → B2（词汇参考仅 33/B1）。Planner 带内选词 = B2 词（`blow off steam`/`up in arms`/`take issue with` 等习语），产出池 13 词全是 B2 级；菜鸟自由表达是 A1 级日常话题文本（吃饭/上班/天气），**永远不会自发使用 B2 习语**——词边界命中这一毕业前提在剧本内无解。
2. **多词短语 lemma 确认/毕业死路（既有缺陷，T-034 放大曝光）**：`BottleneckScreeningService.Tokenize` 只产单词 token，多词 lemma（`up in arms` 等）永远 `tokens.Contains==false`——既不能经造句确认（`ConfirmPromptedUse`），也不能经自由表达毕业。仿真实证：13 个 PromptedUse 词中 10 个多词全部未确认（其中 `up in arms` 造句两次拿 A 且原短语逐字出现，仍 f）；3 个单词词（largely/counters/disappointed）全部确认成功。**T-034 的配额+补位会把越来越多多词习语推进 PromptedUse 死胡同**，已登记 T-040。
3. **A/B 门槛叠加评分环境**：12 篇菜鸟自由表达仅 1 个 B（T-027 收紧生效）；实弹探针中两篇高质量 B2 段落也先被判 C（aiScore 80 仍 C，命中已登记的 T-037 扣题误判——qwen 把 targetWord=free expression 当主题）。即使文本含池词，评级 C 也不毕业。

结论：T-034 机制（配额→Recalled→补位→PromptedUse→毕业判定→可见性）**每一环都实测畅通**，断点在机制之外的上游（定级带）与既有分词口径。但验收锚点「14 天首毕业、30 天 ≥3」未达成，按纪律 T-034 不能标 done。

## 六、联动记录（只记录不判）

- 定级：综合 75 → B2（表达 75、词汇参考 33、阅读参考 67）；等级历史仅 Initial A1→B2 一条；cefrDisplay 随 Overall 在 B1/B2 边界抖动 6 次（T-038 已登记）。
- 洞察：第 10 天未触发、第 17 天触发（avoidance → AvoidancePattern，replan=true）、第 24 天未触发。
- 挑战：第 7/14/21/28 天全 passed（总分 85/85/71.7/78.3）。
- 分数曲线：Vocabulary 恒 75；Writing 75→65→77→73 区间波动（T-022 回写生效）；Overall 65–75。
- 日志量：背词 251、造句 94（A×5/B×44/C×42/D×3）、自由表达 12、拼写 135、阅读 17、计划 5 份。
- 异常：anomalies.log 仅 7 条，全部为「菜鸟句拿 A/四维满分」（LLM 生成句超出菜鸟人设，剧本噪音，非产品缺陷）。

## 七、不足分级

- **P1（验收锚点未达成，非本任务代码缺陷）**：30 天仿真毕业 0 <3。根因 1（定级虚高→产出池全是 B2 习语）登记 T-041；根因 2（多词 lemma 确认/毕业死路）登记 T-040。T-039 集成终验前需先解这两项，否则「毕业 ≥3」在菜鸟剧本下结构性不可达。
- **P2（观察）**：Recalled 在池常年仅 1-3 个——认识→成熟（RepeatCount≥2）依赖答错落薄弱位后二次考察，成熟速度本身偏慢，配额常有富余。若 T-040/041 解后毕业仍偏少，下一轮可校准成熟通路或配额常量（设计 §3 已预留）。

## 八、验收结论

**不予通过（标准 4 未达标），但代码不驳回**：标准 1/2/3/5 全部通过，口径修正成立，实现质量与纪律（最小改动/文档同步/状态机零回归）均无异议；未达标项的根因在任务边界之外（T-040/T-041 已登记为下一轮需求）。T-034 状态维持 testing，待 T-040/T-041 落地后随 T-039 集成仿真一并终验。
