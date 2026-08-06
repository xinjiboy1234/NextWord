# T-042 测评定级防伪闸 — 验收报告（周密）

> 日期：2026-08-06 ｜ 被验：worktree `.worktrees/t-042-anti-inflation`，分支 feat/t-042-anti-inflation，commit 9c033b5
> 环境：worktree API（Development，端口 5192），独立库 nextword_qa_t042（已 DROP），LLM DashScope qwen-plus 真实评分
> 驱动脚本：`run_scripts.py`（三剧本全自动：注册→首次测评→报告/留痕/Planner 轮询）；证据：`evidence-{rookie,strong,skip}.json`、`api.log`

## 结论

**设计 §4 五条验收标准全部通过；但联动验证发现 1 个 P1 不足：矫正未传导到分数先验/CefrDisplay，Planner 词池带仍按矫正前虚高档取词，T-041 病灶（B1 用户拿到 B2 习语造句目标）在首周计划内依然成立。**

综合判定：**有条件通过**——五条标准可标过，P1 不足须登记新任务跟进（见「不足分级」）。

## 一、diff 审查（commit 9c033b5，11 文件 +202/-29）

最小改动成立，全部改动落在任务范围内：

- `AssessmentScoringService`：`BandUpThreshold=70` / `BandDownThreshold=40` / `RecognitionGuardBandGap=2` 常量集中；`ApplyRecognitionGuard` 档差 ≥2 降 1 档、A1 下限、样本缺失（null）与反向不矫正。CefrLevel 枚举 A1=1…C2=6，档差语义与设计 §2.2 一致。
- `AssessmentService.FinalizeAsync`：主定级口径不变，矫正一次性应用于定级后；comments 二选一（矫正说明或标准说明）；`OriginalLevelBeforeGuard` 随 FinalLevel 记录持久化；`Assessment.FinalLevel`、`UserProgress.OverallLevel`、LevelHistory 均为矫正后定级。
- `EvaluationReportService`：两条内容路径（schemaVersion 1/2）摘要均拼接矫正说明。
- 单测：阈值 65 Stay/75 Up（§4-3）与防伪闸 7 例（档差 ≥2 矫正、档差 1/反向/A1 下限/样本缺失不矫正）覆盖到位，diff 审查确认。
- 前端 `InitialAssessment.tsx:111` 结果页渲染 `dimensions.comments`，矫正说明可见（类型已补 `originalLevelBeforeGuard`）。

### 「跳过识别题不再记为答错」口径变化副作用面排查

- 影响面仅两处：`vocabAccuracy`（识别参考分/参考档）与 `UserProgress.VocabLevel` 初值；全跳过时 accuracy=0 与旧口径结果相同，部分跳过较旧口径参考分偏高（更合理）。
- 挑战流（`ChallengeService`）自有识别正确率逻辑，未经此路径，不受影响。
- 存量块记录 JSON 中旧的「跳过记错」样本不会与新测评混算（定级只聚合本次测评记录），无需迁移。
- 观察项（P3）：阅读题未作答仍记为答错（`AssessmentService.cs:152`），与词汇识别新口径不一致；阅读仅参考不进闸，暂无害。

## 二、真实链路实测（三剧本）

### 剧本 a：菜鸟（识别全错制造档差）— 通过

| 项 | 值 |
|---|---|
| 块表现 | A2 块 82 升带 → B1 块 78.7 升带 → B2 块 76 收敛 |
| 表达力综合分 | 79 → 表达定级 **B2** |
| 词汇识别 | 0/3 正确（0%）→ 参考 **A1**；阅读随机答 → A1 |
| 档差 | B2(4) − A1(1) = **3 ≥ 2 → 触发闸** |
| 最终定级 | **B1**（≤B1 ✓），`UserProgress.OverallLevel=B1` ✓ |
| 矫正说明（结果页 comments） | 「表达表现 B2，综合词汇掌握情况调整为 B1。」✓ |
| 矫正说明（评估报告摘要） | 同一句话已拼接进 summary ✓（但见 P2：摘要头部仍写「Overall 79（B2）」） |
| 留痕 | API 响应 `originalLevelBeforeGuard=B2`；DB FinalLevel 记录 `overallLevel=3(B1)`、`originalLevelBeforeGuard=4(B2)` ✓ |

注：第一次试跑（识别仅 2/3 答错、带小错答案）表达 69→自然定 B1，识别 33%→参考 B1 档差 1 未触发闸——符合「档差 <2 不矫正」语义，间接佐证标准 2。识别映射口径备查：`MapVocabAccuracy` 阈值 [9/29/49/69]，33%→B1、0%→A1。

### 剧本 b：对照强用户（表达识别均认真）— 通过

块表现 76/96/90.7 → 表达 88→**C1**；识别 100%→C1、阅读 100%→C1；档差 0 → **不矫正**，`originalLevelBeforeGuard=null`，定级=表达定级 ✓。comments 为标准口径说明。

### 剧本 c：识别全跳过 — 通过

识别题（词汇+阅读）全部不作答 → 样本缺失：表达 87→**C1** 原样保留、**不矫正不报错**，`originalLevelBeforeGuard=null` ✓。界面参考分显示「词汇识别 0（A1）」系既有展示口径，未参与任何决策。

## 三、联动验证（Planner 词池带）— **不通过（P1）**

菜鸟用户矫正后定级 B1，但其首日计划实测：

- `sentenceTargets`（造句目标）：`counters`、`weigh in`、`stance` —— **全部 B2**；
- `wordIds`（带内背词队列）：8 词**全部 B2**（含 `up in arms`、`hit the spot`、`that rings true` 等习语）；
- 接触词 2 个为 C1（库内 C1 仅 4 词，超带接触属设计内）。

根因（代码实证）：`AssessmentService.cs:422-430` 以**未矫正的表达分 79** 作为各维度分数先验写 Score 内核，`ScoreProfileService.SyncLegacyLevels` 据此算 `CefrDisplay=B2`；`AssessmentService.cs:435` 只回写 `OverallLevel=B1`，**不回写 CefrDisplay**。而 Planner（`LearningPlanService.cs:93`）及文章/造句难度选取全部读 `CefrDisplay` → 按 B2 取词。设计 §2.2「本闸只管首测定级这一刻、水平带随 Score 自然演化」给了部分免责，但分数先验本身即虚高值，「自然演化」起点就是错的——T-041 的毕业链路结构性不可达在矫正用户身上依旧成立（B1 菜鸟拿不到带内造句目标）。

## 四、不足分级

| 级别 | 问题 | 证据 |
|---|---|---|
| **P1** | 防伪闸矫正未传导到分数先验/CefrDisplay：矫正后 B1 用户的 Planner 造句目标与背词队列仍全为 B2（含 B2 习语），评估报告头部亦按 B2 展示。T-041 病灶未真正收口 | 本报告 §三；`AssessmentService.cs:422-437` 与 `LearningPlanService.cs:93` |
| P2 | 评估报告摘要自相矛盾：「你的综合水平为 Overall 79（B2）」与「调整为 B1」同屏出现（与 P1 同根因，矫正后摘要头部应取矫正后定级） | `evidence-rookie.json` reportSummary |
| P3 | 阅读题未作答仍记答错，与词汇识别「跳过不计样本」新口径不一致（仅参考展示，不进闸） | `AssessmentService.cs:152` |
| P3 | 跳识别用户的识别参考分显示 0/A1，语义应为「无样本」而非「零基础」（既有展示口径，非本次引入） | `evidence-skip.json` |

## 五、环境清理

QA 库 `nextword_qa_t042` 已 DROP，API 进程已杀。证据文件不含 JWT（脚本不落 token；api.log 已核查无 Authorization 头输出）。
