# T-035「挑战有结果化」验收报告（周密）

- 被验：worktree `.worktrees/t-035-challenge-outcome`，分支 feat/t-035-challenge-outcome，commit `5315fad`
- 日期：2026-08-06/07（实测于独立库 `nextword_qa_t035`，验完已 DROP；API 5188 已停）
- 设计：docs/DESIGN-challenge-outcome.md §4

## 结论：通过（2 项轻微不足，不阻塞）

## 逐项证据

### §4.1 Daily 通过点评 + 计数；未通过有鼓励无点评 —— 通过
- 真实链路（chain.mjs / chain-result.json）：阅读 2/3 + 词汇全对 + 造句认真写（qwen-plus 实评 3.75/5 → 写作 75）→ `passed=true`、`readingScore=67`、`feedback="词汇是你的最长板，继续保持；阅读还有提升空间。"`、`passCount=1`。点评最长板=词汇（100）、最短板=阅读（67），与本次得分一致。
- 规则审查（ChallengeFeedback.cs）：差 ≥10 分（MeaningfulGap）才说「高出一截/拖后腿」，否则降档为「最长板/还有提升空间」，与任务要求一致；纯规则零 LLM，不改分数。
- 未通过：`feedback=null`；前端 ChallengeMode 未通过分支显示鼓励文案「差一点点，回看短板维度的得分，明天再来一次」，不渲染点评（代码审查）。
- 计数派生自 ChallengeRecords，不加新表，符合 §3 约束。

### §4.2 升级候选强引导 —— 通过（代码级 + dashboard 实测）
- 实测：一次 Daily 通过后 `GET /api/level/dashboard` → `upgradeCandidate=true`（LevelUpgradeEngine recentPass 条件，无需 SQL 置位）。
- 前端链路代码审查：App.tsx 引导条主按钮「去确认挑战」→ `navigate('/challenge', { state: { confirmation: true } })` → ChallengeMode useEffect 自动 `start(true)` → useChallengeFlow POST body `{ confirmationChallenge: true }`；挑战页内候选引导条按钮同样 `start(true)`。Dashboard 与挑战页双入口齐全，文案「你已具备冲击 {下一级} 的实力，来确认挑战」符合设计。

### §4.3 阅读 3 题计分 + 兼容 —— 通过
- 实测 start：`readings` 3 题，每题考点词（eventually/encourage/memory 等）均出自摘要正文，4 选项。
- 实测计分：2/3 → 67 过阈值（`Math.Round(66.67)=67`，ReadingScoreMin 100→67 配置已改）；1/3 → 33 不过。
- 旧单题会话兼容：单测 `Legacy_single_reading_session_still_scores`（无 readings 属性 JSON + 旧客户端 readingSelectedIndex 提交 → 单题计分 100 通过）覆盖；本人定向复跑 `dotnet test --filter ChallengeOutcomeTests`：**6/6 全绿**（19s）。
- diff 审查回退路径：`GetReadings`（Readings 空则回退 [Reading]）、`ReadingSelectedIndexes ?? [ReadingSelectedIndex]`，双字段并存，旧客户端不炸。

### §4.4 历史记录可读性 —— 通过（代码级）
- ChallengeRecentList：总分/词汇/阅读带 `/100`、造句 `/5`（toFixed(1)），AttemptedLevel 下加「为挑战目标档：挑战比当前等级高一档的内容」。
- 实测 `/api/challenge/recent` 返回 2 条记录字段齐全（attemptedLevel/passed/各项得分），前端有数据可渲染。

### §4.5 既有挑战测试新口径全绿 —— 通过
- 定向复跑新增 ChallengeOutcomeTests 6 例全绿；e2e challenge.spec.ts 预期同步更新为 readings 数组，diff 合理。全量 225+6 按任务要求未重跑，采信开发自报 + 抽查。

## 不足（均为轻微 P2，不阻塞）

1. **接口注释与行为不一致**：`IAssessmentServices.cs` 注释称 PassCount「确认挑战/未通过为 null」，实际未通过的 Daily 也返回 passCount（实测 run2 返回 1，单测也断言 0）。行为本身合理（前端可用于展示），建议修正注释。
2. **阅读考点词偶发跨档兜底**：出题先按目标档带内词选考点词，找不到时回退全词池（`?? allWords...`），可能抽到非目标档词；实测 3 题均为带内常用词，影响小，设计上属「可发起挑战」兜底，记录备查。

## 证据文件
- `chain.mjs` / `chain-result.json`：真实链路脚本与逐步断言结果（不含 token）
- `api.log`：API 运行日志（无密钥，仅 EF 列名）
- 单测复跑输出见会话记录（6/6 绿）
