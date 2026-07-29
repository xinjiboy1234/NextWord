# 剧本：《林晓的七天》—— NextWord Agent 协作演示

> 目的：在**不改任何代码、不直接改任何数据**的前提下，用真实 API 多轮操作演示
> NextWord 各 Agent（Profiler / Verifier / Planner / Insight）如何围绕一个用户协作。
> 全部数据由 `scripts/run-story.py` 通过公开 API 真实产生；后台定时任务按剧本需要
> 用公开的手动触发端点（`POST /api/insights/bottleneck/jobs` 等）调整触发时机——
> 这是任务说明中明确允许的唯一定制。
>
> LLM 全程真实（DashScope qwen-plus），所有 Agent ↔ LLM 对话由
> `scripts/llm-proxy.py` 记录到 `output/llm-conversations.jsonl`。

## 人物设定

**林晓**，28 岁，外企行政专员。英语底子中等（约 B1）：写邮件够用，能正确使用
because / although / when 这类连接词，但词汇量一般、句式单一。最近转岗到需要
写英文周报的团队，压力变大。

**行为弧线（本剧的核心戏剧点）**：

- 第 1–4 天：学习积极，产出句子结构正常，连接词使用稳定；
- 第 5–6 天：被周报打击了自信心，写句子开始「避难就易」——连续产出全是
  简单句，复杂连接词完全消失（**回避模式**）；
- 第 7 天：系统日级筛查捕捉到这一变化，Insight Agent 细读她的产出原文，
  判定瓶颈性质并自动触发重规划——这是「Agent 价值」的高潮一幕。

## 角色（Actor）表

| 角色 | 类型 | 说明 |
|---|---|---|
| 林晓 | 用户 | 由驱动脚本以真实 API 操作扮演 |
| 规则引擎 | 确定性代码 | SM-2、Score 内核、自适应测评、指标筛查（零 LLM） |
| Profiler Agent | LLM Agent | 测评收敛后生成 WeaknessProfile 画像草稿 |
| Verifier Agent | 机械核查（不调 LLM） | 逐条核查 Finding 证据真实性与数值一致性 |
| Planner Agent | 规则规划器（零 LLM） | 消费 Verified 画像生成 7 日学习计划 |
| Insight Agent | LLM Agent | 细读产出原文，判定瓶颈性质 7 分类 |
| LLM（qwen-plus） | 外部模型 | 经记录代理转发，全部对话留痕 |

## 分幕剧本

### 第一幕（Day 1 上午）：定级

1. 林晓注册账号（`POST /api/auth/register`）。
2. 开始首次水平测评（`POST /api/assessment/initial/start`）。
3. 自适应分块作答（`GET next-block` → `POST submit`，2–3 块）：
   - 产出题（提示造句 ×2 + 情境表达 ×1）：以她的真实水平作答——语法正确、
     连接词自然、词汇普通（答案文本见 `data/persona.json`）；
   - 识别题（词义选择 + 阅读理解）：她阅读偏弱，随便选（选第 1 项）。
     识别分只作参考展示，不影响定级——这本身就是设计的一部分。
4. 测评收敛，规则引擎定级并写入 Score 内核。

**预期 Agent 行为**：测评收敛自动入队 EvaluationReport 后台任务。

### 第二幕（Day 1 晚，系统自动）：画像与首个计划

1. BackgroundJobWorker 捞起 EvaluationReport 任务：
   - **Profiler Agent** 聚合林晓的真实数据（测评 SentenceLogs、四维分数）
     调 LLM 生成 Finding 草稿（对话留痕 P1）；
   - **Verifier Agent** 逐条机械核查：证据 id 是否真实属于本人、引用数值与
     库内重算值是否一致、证据条数是否支撑置信度；不通过的标存疑、不进展示；
   - 评估报告落库（schemaVersion 2 = 已验证 Finding 列表）。
2. 报告任务自动入队当日 **Planner** 任务：
   - **Planner Agent** 只消费 Verified 的场景 weakness Finding 定主攻场景
     （新用户无场景维度 Finding 时走词覆盖率兜底——设计允许），
     生成 7 日计划：每日 8 带内词 + 2 超带接触词 + 3 个造句目标 + 3 篇阅读推荐。

**观测点**：`GET /api/profile/weakness`、`GET /api/evaluation/latest`、
`GET /api/planner/current`。

### 第三幕（Day 2–4）：按计划学习（风格正常期）

每天：背词（`GET /api/words/daily`，验证 fromPlan/接触词标记）→
造句（`GET /api/sentences/prompts` 取 Plan 目标词，`POST /api/sentences/rate`）→
阅读推荐文章（`GET /api/articles/recommended`，查词）。

此阶段林晓的造句**连接词使用正常**（每句 1–2 个复杂连接词，文本见
persona.json 的 `sentencePractice.phase1`），为后面的对比埋下基线。

### 第四幕（Day 5–6）：回避期

转岗压力显现。林晓连续 6 次造句全是简单句：没有 because、没有 although、
没有任何复杂连接词（persona.json 的 `sentencePractice.phase2`）。
自由表达也变得短促保守。

**此时系统尚未有任何反应**——规则引擎只是沉默地记录。这正是传统产品的盲区：
分数没崩（简单句语法依然正确），但学习行为已经变质。

### 第五幕（Day 7）：Agent 介入（高潮）

1. 日级指标筛查（剧本用 `POST /api/insights/bottleneck/jobs` 手动触发，
   等价于 ProfileScoreSnapshotWorker 每日自动执行的那一次，零 LLM）：
   近 12 条产出样本中，前半段连接词率 ≥0.3/句，后半段腰斩至 ≤ 50%
   ——**回避模式信号命中**，自动入队 BottleneckInsight 任务。
2. **Insight Agent** 细读林晓近 20 条产出原文 + 当前 Plan 主攻方向，
   判定瓶颈性质（预期 AvoidancePattern），给出一句中文结论并引用
   真实的 SentenceLog 证据（编造的证据 id 会在落库前被机械过滤）。
   对话留痕 P2。
3. 性质变化判定：这是林晓的第一条洞察（首次发现 = 变化）→ 自动重规划：
   - 画像重生成（Profiler Agent 再次出场，对话留痕 P3）；
   - 入队 force Planner → **当日 Plan 原地重建**（主攻方向随最新画像调整）。
4. 对比新旧 Plan，可见系统对「行为变质」的响应落到了具体的学习内容上。

### 第六幕（终评）

`output/evaluation.md`：逐项核查 Agent 链路是否如设计运作——

- 画像 Finding 是否全部有真实证据、Verifier 核查状态；
- Plan 是否只消费 Verified Finding、接触词 ≤20% 且严格超带；
- 回避信号是否被规则引擎零 LLM 捕获、Insight 结论与证据是否真实；
- 重规划副作用链（画像重生成 + Plan 原地重建）是否完整；
- 全程 LLM 调用次数与成本（每测评 1 次画像 + 每触发 1 次洞察）。

## 诚实性声明

- 全部用户侧数据（测评作答、造句、自由表达、背词、阅读）均由公开 API 真实提交，
  由真实 qwen-plus 评分；**没有**任何绕过 API 的 INSERT/UPDATE；
- 剧本预设了「期望」，但实际定级、Finding 内容、洞察结论以真实运行结果为准；
  终评如实记录与预期的偏差；
- 数据库只读查询（SELECT）仅用于观测与留痕（BackgroundJobs 进度、Plan 内容快照）。
