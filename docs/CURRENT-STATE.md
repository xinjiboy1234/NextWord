# NextWord 当前状态（Current State）

> 版本：2026-07-25。本文描述**已实现并验证**的现状，是项目功能的权威参考。
> 待办事项见 [next-steps.md](../next-steps.md)；架构决策的「为什么」见 [DESIGN-ai-learning-architecture.md](DESIGN-ai-learning-architecture.md)。

## 1. 项目概览

NextWord 是 AI 驱动的英语词汇学习应用，核心闭环：

```
每日选词 → 新词记忆 / 拼写 / 造句 / 阅读 → 首次测评定级 → 挑战升级 → Score 画像持续更新
```

两条设计主线：

- **规则引擎保证确定性**：SM-2 间隔重复、Score 内核（0–100 三维分数）、等级升级规则，全部由后端确定性代码执行。
- **LLM 提供智能体验**：单词难度标注、造句/自由表达评分、阅读查词与词汇提取、批注回复。LLM 全部走 `ILLMProvider` 抽象，默认 Mock（无外部依赖），可切 OpenAI 兼容接口（服务端全局配置或用户级 BYOK）。

## 2. 仓库结构

```
Backend/                  .NET 10 解决方案（NextWord.slnx）
  NextWord.Api/           Web 宿主：Program.cs 组装、认证、CORS、健康检查、启动迁移+种子
  NextWord.Api.Endpoints/ 全部 Minimal API 端点（19 个端点类，纯 HTTP 层）
  NextWord.Domain/        29 个实体、14 个枚举、场景 taxonomy 常量、接口契约、领域服务（SM2/Score 映射/等级引擎/生命周期/Prompt 工厂）
  NextWord.Infrastructure/ EF Core + Npgsql、仓储、约 28 个业务服务、JWT/密码、5 个后台 Worker、缓存
  NextWord.UnitTests/     xUnit 单元测试（Score/缓存部分连真实 PostgreSQL）
  NextWord.IntegrationTests/ WebApplicationFactory + 真实 PostgreSQL 集成测试
  Scripts/                迁移 SQL 生成脚本、backfill drill 说明、生产迁移 runbook
Frontend/                 React 19 + Vite 8 + Tailwind 4 + @base-ui/react
  src/pages/              14 个页面；src/components/ 约 35 个组件（含 ui/ 设计系统封装）
  e2e/                    Playwright E2E（5 用例）
front_design/             静态 HTML/CSS 设计原型，screens/ 与 src/pages/ 一一对应，同步维护
docker-compose.yml        postgres:16-alpine + redis:7-alpine + api（容器内 8080）
```

## 3. 技术栈

- **后端**：.NET 10、ASP.NET Core Minimal API、EF Core 10 + Npgsql（PostgreSQL 16，全环境统一，SQLite 已移除）、`Microsoft.Extensions.AI.OpenAI`（IChatClient）、JWT Bearer（HS256）、PBKDF2-SHA256 密码哈希（10 万次迭代）、`Microsoft.Extensions.Caching.StackExchangeRedis`（可选）
- **前端**：React 19.2、TypeScript、Vite 8、Tailwind CSS 4（`@tailwindcss/vite`）、`@base-ui/react` 无样式基元（封装于 `src/components/ui/`）、lucide-react 图标、react-router-dom v7、axios；无专门状态管理库（AuthContext + 15 个自定义 hooks + localStorage）
- **测试**：xUnit、WebApplicationFactory、Playwright

## 4. 认证与用户

- 全站默认要求登录（`Program.cs` 的授权 FallbackPolicy）。匿名可访问仅：`GET /api/health`、`GET /api/health/details`、`POST /api/auth/register`、`POST /api/auth/login`。
- JWT HS256，7 天有效（`Auth:JwtSecret`/`Issuer`/`Audience`/`ExpirationDays` 配置）；生产必须覆盖默认 JwtSecret。
- 用户解析只认 JWT `sub`/`NameIdentifier`（`UserResolver`），无 query userId 回退。
- 前端：token 存 localStorage（`nextword.auth.token`），axios 拦截器自动带 Bearer；401 自动清凭据；未登录时 App 只渲染登录页（非路由守卫）。
- 新用户 `hasCompletedInitialAssessment=false` 时被强制留在 `/assessment` 沉浸式 Onboarding，可确认跳过（`POST /api/assessment/initial/skip`，默认 A2）。

## 5. 功能模块

### 5.1 每日选词与新词记忆（`/learn`）

- `GET /api/words/daily?count=`：**优先执行当日 LearningPlan 词队列**（T-006，见 §5.15：带内词 + ≤20% 超带接触词，`fromPlan`/`isExposure` 标记）；无 Plan、Plan 过期（>7 天）或当日队列为空 → 回退既有逻辑——按用户 Vocabulary 分取 `[score, score+12]` 难度带单词 + `EstimatedKnownRate<0.4` 弱词，各占约一半（`DailyWordSelectionService`）。**T-014：返回词带生命周期阶段 `stage` 与考察模式 `quizMode`**（认识=recognition 看词知义，回忆及以后=recall 看义想词，新词默认认识模式）。**T-034：两条路径都保证 ≥40% 名额给「已成熟待推进」老词的回忆考察位**（`RecallExamQuotaRatio` 常量；池 = recalled 阶段 + 认识且 `RepeatCount≥2` 的残留词，`StageUpdatedAt` 最早优先，考察模式按阶段派生），不足时新词补位。
- `POST /api/learning/submit`：提交作答（`mode`=recognition/recall，回忆模式需正确拼出词本身）→ SM-2 排程更新 + `EstimatedKnownRate`/`PersonalDifficulty`（EMA）+ 连胜天数 + **生命周期阶段推进（T-014，见 §5.17）**。**自评（Remembered/Forgot）只改 SM-2 排程参数，不再按自评加减掌握度**——`MasteryScore` 由阶段派生（25/50/75/100）。
- SM-2 变体（`Sm2Service`）：EF 下限 1.3，间隔上限 3650 天；只管认识/回忆两阶段调度。
- `POST /api/words` 新增单词时调用 LLM `RateDifficultyAsync` 自动定级（DifficultyLevel + CefrLevel + 0–100 IntrinsicScore 标注）。

### 5.2 拼写（`/spelling`）

- `GET /api/spelling/queue`：到期复习队列，无到期词时回退每日词。
- `POST /api/spelling/submit`：含逐字母错误位置标注；前端发音播放 + 错误高亮。

### 5.3 造句工作室（`/sentence`）

- 两个 Tab：指定词造句（`GET /api/sentences/prompts` + `POST /api/sentences/rate`）与自由表达（`POST /api/free-expression/rate`）。
- 指定词出题（T-006）：登录用户走个性化——有当日 Plan 用 Plan 造句目标（带内、主攻场景优先，`fromPlan` 标记）；无 Plan/过期回退**带内约束**选词（目标词 CEFR 与水平带一致，带池不足向下一带补充，产出任务只用带内词）；匿名保持既有按难度出题。
- LLM 同步评分：语法/自然度/词汇/相关度各 0–5 + A–D 等级 + 改写建议；反馈语言默认 zh-CN（`Llm:SentenceRating:ExplanationLanguage`）。
- **挑战度口径（T-027）**：评分尺子 = 用户当前水平带——服务端从 UserProgress 投影解析（CefrDisplay，ScoreMapping 单一来源；无进度回退调用方传入带，再退默认 A2），不再信任客户端传带。Prompt 与 Mock 口径同步：复杂度/用词与水平带相称且正确才可拿 A/满分；明显低于水平带的「安全简单句」即使完全正确也词汇维 ≤3、总评封顶 B；高于水平带的尝试不因难度额外扣分；与水平带相称的简单句（如 A2 用户的简单句）不受影响，仍可拿 B/A（公平性，prompted_use 确认链路依赖 A/B 档）。Mock 启发式：各带设最低句长/连接词数期望，两项都未达到即压分。
- **自由表达评分无目标词（T-037）**：自由表达评分请求不再把字面量 `free expression` 当 targetWord（qwen-plus 会把它当写作主题，高质量段落被判 off-topic 拿 C）——改传中性主题描述（「日常自由表达」/daily-life）+ `SentenceRatingRequest.IsFreeExpression` 标记；prompt 走专门变体（`LlmPromptFactory.BuildFreeExpressionRatingPrompt`，无 Target Word 行），相关性维度按「内容是否围绕日常场景/主题连贯展开、言之有物」评，不要求出现任何特定词；挑战度规则（T-027）两变体共用。Mock 同步按标记判定，不扣「未用目标词」。
- **生命周期证据（T-014）**：指定词造句评分后按目标词使用情况推进/回退——句中含目标词（统一命中口径 T-040：单词词边界、多词短语连续词序列）且 A/B 档 → 确认 prompted use（待自发）；含目标词但 D 档或词汇维 ≤2 → 退回回忆阶段重进 SM-2 调度；指定目标词的产出永远不算自发。自由表达（非指定目标词）评分达标（A/B）时，文中自发出现的 prompted_use 候选池词毕业（spontaneous_use）并留痕 `GraduatedFreeExpressionLogId`。
- **Score 小步回写（T-022）**：两个评分端点落库后经 `PracticeScoreWritebackService` 回写 Writing 维——observed = `MapSentenceToScore(四维均分)`（自由表达 `AiScore` 同口径），delta = clamp(round((observed − current) × 0.1), −2, +2)，delta=0 也落幂等记录（`sentence-score:{logId}` / `freeexpr-score:{logId}`）；响应带 `writingScoreBefore/After`，前端结果区显示「写作 64→65（+1）」徽标。测评与挑战复用 `SentenceService.RateAsync` 但不经过该端点，绝不叠加 delta。原 `SentenceLlmScoringWorker`（无入队点的死链路）已删除。

### 5.4 阅读（`/reading`、`/reading/:id`）

- 短文库按难度/CEFR 筛选分组；种子含 21 篇分级短文。
- `GET /api/articles/recommended`（T-006）：有当日 Plan 按主攻场景选文（TopicTag/场景匹配 + 难度就近，`fromPlan=true`）；无 Plan/过期按难度就近回退。前端短文库顶部展示「今日推荐」。
- 阅读器：逐词渲染点词查义（`POST /api/reading/lookup`，先查 `ArticleVocabMappings` 文章级缓存，缺失再 LLM 并 upsert；返回音标 + 文中/其他场景双例句 + 熟悉度）。
- `POST /api/articles/{id}/vocab-extract`：LLM 提取重点词汇（含音标 + 用法例句）并持久化；存量数据 lazy backfill。
- 段落批注：`GET/POST /api/articles/{articleId}/comments`，可请求 AI 回复。
- 阅读日志：`reading/start` → `reading-logs/{logId}/finish`（计时、查词数参与评分）。
- `POST /api/reading/agent`：阅读助手 Agent（`ReadingAssistantAgent` 组合 skills）。**前端暂未使用**。

### 5.5 首次水平测评（`/assessment`）— I2 T-004 重构

- **自适应分块**：以当前估计水平为起点（未测评用户 A2），每块 5 题（提示造句 ×2 + 情境表达 ×1 + 词义选择 ×1 + 阅读理解 ×1，产出占 60%），块表现 ≥70 升带、<40 降带（T-042：升带阈值 60→70，慷慨评分下 60 等于不设防），满 2 块且稳定即收敛、最多 3 块（总题量 ≤15）。
- **产出题全部走 LLM 真实评分**：复用造句工作室四维链路（`SentenceService.RateAsync`，情境表达走 `free expression` 目标），词数启发式已废弃；评分留痕 `SentenceLogs`。
- **主定级 = 表达力综合分**（语法/自然度 0.3 + 词汇/相关度 0.2 加权，阈值直接派生自 ScoreMapping 分带、封顶 C1）；识别题仅作参考展示，不加权进表达分——「最短板 min」已废弃。档案各维度分数以表达力综合分为初始先验写入 Score 内核。
- **识别防伪闸（T-042）**：定级完成后一次性矫正——表达定级档 − 词汇识别参考档 ≥2 时下调 1 档（下限 A1，C1 封顶在前）；识别样本缺失（全跳过识别题）或反向（识别高于表达）不矫正。矫正留痕（`AssessmentFinalResult.OriginalLevelBeforeGuard` + FinalLevel 记录），结果页 comments 与评估报告摘要含「表达表现 X，综合词汇掌握情况调整为 Y」说明；常量在 `AssessmentScoringService`（`BandUpThreshold=70` / `BandDownThreshold=40` / `RecognitionGuardBandGap=2`）。确认挑战路径不受影响。
- **矫正传导（qa-t042 P1 修复）**：矫正触发时三维分数先验逐维 clamp 到矫正后档上限以内（`GetBandScoreCeiling`，保持维度相对形状），`CefrDisplay` 与评估报告头部随之取矫正后档——Planner 词池/造句目标按矫正后定级取词；识别/阅读题未作答均不计样本（同口径）。
- **词池纪律**：出题词只选水平带内且 `utility=high/medium`（顶端带词池过薄时向下一带补充，绝不超带）；情境场景取自 I1 taxonomy（优先词池已标注场景）。
- **阅读题**：从库内文章按难度带就近选文，考点词取自正文中出现的库内词，正确答案位置随机（硬编码与 index-0 恒定已废除）。
- 端点：`GET /api/assessment/{id}/next-block`（幂等，未提交的块重发原题）→ `POST /api/assessment/{id}/blocks/{n}/submit`（同步 LLM 评分，收敛时直接定级并入队 `EvaluationReport`）。

### 5.6 综合挑战（`/challenge`）

- `POST /api/challenge/start` 生成挑战包（`ChallengeSession` 存题目，客户端不拿答案）；可带 `confirmationChallenge` 锁定目标等级。
- `POST /api/challenge/submit`：客户端提交原始答案，服务端按 `ChallengeThresholds` 计分（词汇正确率 ≥0.6、写作 ≥53、阅读 ≥100、升级增量 5）；确认挑战通过则 ProfileScore 加 UpgradeDelta 并出评估报告。

### 5.7 Score 内核（v1）

- **模型**：`UserProgress` 持有 Vocabulary/Reading/Writing 三个 0–100 分；总分 = 三者最小值（最短板，`ScoreMappingService.ComputeOverall`）；CEFR 分带在 `appsettings.json` 的 `ScoreMapping`（T-023 校准：A1 0–20 / A2 20–35 / B1 35–70 / B2 70–85 / C1 85–95 / C2 95–100，全局口径：测评定级、Profile CEFR 展示、计划水平带、挑战定档共用；`DifficultyBuckets` 不变）。
- **写入**：`ScoreProfileService.ApplyUpdateAsync` 是唯一入口，支持 absolute/delta；`LearningEvents.IdempotencyKey` 幂等去重。写入点三处：测评完成（absolute 先验）、确认挑战通过（+UpgradeDelta）、日常造句/自由表达评分（T-022 小步 delta：observed = `MapSentenceToScore(四维均分)`，delta = clamp(round((observed − current) × 0.1), −2, +2)，幂等键 `sentence-score:{logId}` / `freeexpr-score:{logId}`；测评/挑战的 `SentenceService.RateAsync` 调用不触发该 delta）。
- **快照**：`ProfileScoreSnapshotWorker` 每日写 `ProfileScoreSnapshots`，供 `GET /api/profile/scores/history?days=` 趋势图。
- **难度三层**：intrinsic（LLM 标注，持久化于 `WordDifficultyAnnotation`）→ personal（`EstimatedKnownRate`/`PersonalDifficulty` EMA）→ effective（`EffectiveDifficultyCalculator`，含学术语域加成）。
- **学习工具注册表**：`GET/POST /api/tools` 暴露 7 个工具（get_profile_scores、search_web(DuckDuckGo)、lookup_word_context、get_daily_words、get_evaluation_latest、get_challenge_history、get_recent_learning）。供 Agent 场景使用。

### 5.8 等级系统

- `LevelUpgradeEngine`：连胜 ≥3 且当前等级 ≥3 天，或 7 天内有挑战通过 → 升级候选；C1 封顶。
- `LevelCheckWorker` 每日刷新 `IsUpgradeCandidate`；`GET /api/level/dashboard`、`GET /api/level/history`。

### 5.9 个人中心与 LLM 设置（`/profile`、`/manage`）

- `GET /api/profile`：等级、连胜、统计、等级历史、LLM 设置；`PUT /api/profile` 改显示名。
- BYOK：`GET /api/profile/llm/presets`（OpenAI/DeepSeek/Qwen 预设）+ `PUT /api/profile/llm` 存用户自己的 OpenAI 兼容 API（`UserLlmSettings`，API key 脱敏返回）；`UserLlmProviderFactory` 按用户构建 provider。
- `GET /api/evaluation/latest|{id}` 查看评估报告（测评触发的报告内容为已验证 Finding 画像，见 §5.14；其余触发为模板文案）。
- `GET /api/profile/weakness`：最新 WeaknessProfile（含每条 Finding 的核查状态与存疑原因）。
- `POST /api/feedback`：释义错误 / 标记已知 / 排除单词（触发 ReAnnotation 后台任务）。

### 5.10 后台 Worker（Infrastructure/Background，共 5 个 HostedService）

| Worker | 周期 | 职责 |
|---|---|---|
| `BackgroundJobWorker` | 2s 轮询 `BackgroundJobs` 表 | 处理 EvaluationReport / ReAnnotation / ScenarioAnnotation / Planner / BottleneckInsight 五类任务（SentenceLlmScoring 已随 T-022 移除，日常造句/自由表达评分改由端点层 PracticeScoreWritebackService 同步小步回写 Writing 维）；**T-013 僵尸回收**：Processing 超 5 分钟重置回 Pending（RetryCount+1），超 3 次标记 Failed 留痕 |
| `ReviewReminderWorker` | 6h | 刷新待复习数 |
| `LevelCheckWorker` | 24h | 刷新升级候选标记 |
| `ProfileScoreSnapshotWorker` | 24h | 写 Score 每日快照 + 跑瓶颈指标筛查（T-007，零 LLM，触发则入队 BottleneckInsight） |
| `WeeklyReplanWorker` | 24h 检查 | 每周兜底重规划（T-007）：活跃存量用户按 ISO 周入队 force Planner |

Worker 异常不拖垮宿主（`BackgroundServiceExceptionBehavior=Ignore`）。

### 5.11 LLM 集成

- 统一抽象 `ILLMProvider`（8 方法：难度标注 / 释义 / 造句评分 / 词汇提取 / 批注回复 / 场景标注 / 画像生成 / 瓶颈洞察）；Prompt 由 `LlmPromptFactory` 生成。
- 默认 `LlmMockProvider`：内置词典启发式，零外部调用；**未知词难度一律回退 Basic/A1**（未启用真实 LLM 时 `POST /api/words` 自动定级基本无效）；**Mock 场景标注只按词性推 role、场景一律空（全落 core 桶）**，不代表真实标注质量。
- `Llm:OpenAI:Enabled=true` 且有 key 时切 `LlmChatClientProvider`（OpenAI `ChatClient`，默认 `gpt-4o-mini`，Temperature 0.1，异常自动回退 Mock）；`Llm:OpenAI:BaseUrl` 可选，指向任意 OpenAI 兼容端点（如 DashScope compatible-mode）。
- 装饰链：Inner → `LlmRetryProvider`（指数退避 3 次）→ `LlmTelemetryProvider`（记录耗时与 ModelProfileId）。
- 用户级 BYOK 优先于服务端全局配置。

### 5.12 缓存

- `ICacheService` 由 `Cache:Provider` 切换 Memory（默认）/ Redis。
- 词义查询以 `ArticleVocabMappings` 表做 DB 级缓存；`POST /api/llm/rate-difficulty` 带 24h IMemoryCache（SHA256 key）。

### 5.13 场景 taxonomy 与词级标注（I1 T-002）

- **Taxonomy**（`NextWord.Domain/Scenarios/ScenarioTaxonomy.cs`）：常量集，两层 7 大类 × 20 子场景（key + 中文名），设计见 `docs/DESIGN-scenario-taxonomy.md`；无管理后台。
- **词级标注**：`Word.Utility`（high/medium/low，low 不入库）、`Word.Role`（core_verb/connector/scene_noun/phrase_pattern）、`Word.ScenarioAnnotationVersion`（0=未标注）；`WordScenarios` 关联表（WordId + ScenarioKey 复合主键）承载 0–3 个子场景多对多，0 个 = core 通用桶。难度仍走 `WordDifficultyAnnotations`，不重复；接触词是运行时概念，无静态字段。
- **内置词表**：`Infrastructure/Data/wordlist-scenarios.json`（嵌入资源，1520 词，由 `Backend/Scripts/generate-wordlist.py` 用 LLM 按设计 §4 标准生成，含 scenario/utility/role 标注；T-008 已统一 `examples` 数组字段、补全空音标、修正 `shop around`/`have` 场景标注），`SeedData` 空库时灌入并标记为已标注；验收口径有 `WordlistSeedTests` 守护（每子场景 ≥60、core ≥500、core_verb+connector ≥40%）。
- **标注 worker**：`ScenarioAnnotationWorker`（复用 BackgroundJob 模式）对 `ScenarioAnnotationVersion` 低于当前版本的词分批（默认 20/批，payload `batchSize` 可调 ≤50）调 `AnnotateScenarioAsync`；已标注词自动跳过 → 幂等可重跑、断点可续；LLM 漏标的词保持未标注等下轮。
- **触发**：`POST /api/scenarios/annotation-jobs?batchSize=`（幂等 key 按小时分桶）；`POST /api/words` 新增词自动入队标注。
- **查询**：`GET /api/scenarios` 返回 taxonomy + 各子场景词数 + core 桶词数；`GET /api/words?scenario=` 按子场景过滤；`WordDto` 带 scenarios/utility/role。
- **迁移**：T-015 已收口进 EF 迁移链（`ConsolidateI1ToI4Schema`，PG 走幂等 SQL 分支）；`Patch_PostgreSql_ScoreKernel.sql` 幂等补丁保留，存量 dev/prod 库升级路径不破坏。

### 5.14 WeaknessProfile 画像 + Verifier（I2 T-005）

- **画像结构**：`WeaknessProfiles`（一次测评一份，`(UserId, AssessmentId)` 唯一）+ `ProfileFindings`（Finding 五要素：维度 scenario/skill/reading、强弱 strength/weakness/neutral、结论文案、证据引用 EvidenceJson、置信度 high/medium/low）；设计见 `docs/DESIGN-weakness-profile.md`。
- **触发链路**：测评收敛 → `EvaluationReportService.EnqueueForUserAsync`（InitialAssessment）→ `BackgroundJobWorker` → `ProcessJobAsync` 内 `WeaknessProfileService.GenerateAsync`（同一测评幂等，仅测评触发，成本 = 每用户每测评 1 次 LLM 调用）。
- **Profiler Agent**（`WeaknessProfiler`）：聚合库内真实数据（最近 30 条 SentenceLogs、最近 30 条 FreeExpressionLogs（T-032 起，与造句留痕同权作为产出证据——探索周表达任务的证据对画像可见）、测评 FinalLevel 四维均值、场景词覆盖/正确率、阅读查词统计）→ `ILLMProvider.GenerateWeaknessProfileAsync`（第 7 个 LLM 方法）产出 Finding 草稿；场景/阅读统计由 `WeaknessProfileStats` 单一来源计算（两位小数），供引用值与重算值机械比对。
- **Verifier Agent**（`FindingVerifier`，不调 LLM）：逐条机械核查——证据引用真实存在且属于本人（sentence_log / free_expression_log（T-032 新增，Metric=aiScore）按 UserId 过滤）、引用数值与库内重算值一致（sentence_log 四维分 / free_expression_log AiScore / assessment_dimension / word_stats / reading_stats）、证据条数支撑置信度（high≥3 / medium≥2 / low≥1）；任一不通过标 `Questioned` 并留原因，不展示、不进规划输入（T-006 只消费 Verified）。
- **展示**：报告 `ContentJson` schemaVersion 2 = 已验证 Finding 列表（strengths/weaknesses 由 Finding 派生兼容旧前端）；画像失败或全部存疑时回退 schemaVersion 1 模板。前端 LevelPanel 优先渲染 findings（维度/置信度徽标 + 结论）。
- **解析容错**：qwen 会把枚举白名单原样照抄（`"skill|grammar"`），`LlmResponseParser` 按 `|` 逐 token 取第一个可识别值；提示词模板已改为具体占位值。
- **画像去重（T-010，随 T-006 修复）**：Profiler 提示词要求「每维度至多一条 Finding、不跨 Finding 复用同一证据」；草稿交 Verifier 前经 `WeaknessProfiler.Deduplicate` 后处理——同维度（Dimension+DimensionKey）保留证据更强者（条数多优先、并列取置信度高），同一证据引用被多条复用时只留在置信度最高者、被剥夺后无证据的整条丢弃。Verifier 职责不变。
- **冷启动放宽档（T-032，设计见 `docs/DESIGN-cold-start-profile.md`）**：`ColdStartExplorationService` 纯服务判定——注册满 7 天或产出证据（SentenceLogs + FreeExpressionLogs）≥10 条且从未冷启动重生成 → 挂 `ProfileScoreSnapshotWorker` 日检（面向全部用户，含跳过首测者）触发 `WeaknessProfileService.GenerateAsync(assessmentId: null, coldStart: true)` + 入队 force Planner（幂等键 `planner:coldstart:{userId}:{yyyyMMdd}`）。放宽档只放宽样本量纪律：证据真实、数值一致但条数不足的 Finding 置信下调 low 标 Verified、`VerificationNote` 注「初步判断」（可进规划，前端低置信带「初步」徽标）；伪造/越权/数值不符的机械核查不放宽。画像落 `ModelProfileId = "weakness-profile-coldstart"` 标记位——「每用户仅一次」判据，与瓶颈触发（T-007）的重生成（`"weakness-profile"`）区分，无 schema 变更。第二份画像起（默认档）恢复既有样本量纪律（不足即 Questioned）。

### 5.15 LearningPlan + PlannerWorker（I3 T-006）

- **计划结构**：`LearningPlans` 表（`(UserId, StartDate)` 唯一 → 同日幂等；枚举-free，内容明细存 `ContentJson`）：7 日计划 = 主攻场景（1–2 个子场景）+ 每日词队列（带内词 + ≤20% 超带接触词）+ 阅读推荐（3 篇）+ 每日造句目标（3 词）+ 生成依据 Finding id 列表；设计见 `docs/DESIGN-planner-worker.md`。
- **生成（`LearningPlanService`）**：主攻场景只取自最新画像的 **Verified 场景 weakness Finding**（存疑不进规划），画像不足按场景词覆盖率最低者兜底；`sourceFindingIds` 来源标记诚实反映计划消费的 Verified Finding——场景维 weakness（主攻场景依据）+ 技能维 weakness（T-032 修复：技能画像也让计划对消费者即「个性化」，顾言口径 = 基于任何 Verified Finding），存疑条目始终不计；水平带用 **CEFR**（`CefrDisplay`，与测评词池口径一致——词库词多数无 IntrinsicScore 标注，intrinsic 带会落空），带池过薄向下一带补充、绝不超带；接触词 = CEFR 严格高于水平带的词，每天 ≤2 个（10 × 20%），只进背词识别队列；**每日造句目标优先取 T-014 产出候选池**（prompted_use 阶段且未确认的词，带内、utility 非 low，按进池时间 7 天顺次消耗），**T-034 二级补位 Recalled 池**（recalled 且带内、utility 非 low，`StageUpdatedAt` 最早优先），两级都空才取当日带内词。
- **触发（`PlannerWorker`，BackgroundJob 新任务类型）**：测评完成 → 评估报告任务处理时入队（幂等键 `planner:{userId}:{yyyyMMdd}`，同日重复触发复用同一 job 且不重复生成）；`POST /api/planner/jobs` 可手动触发当前用户当日任务；`GET /api/planner/current` 查当日有效 Plan。
- **内容来源切换**：每日选词 / 阅读推荐 / 造句出题均优先执行当日 Plan（`GetActiveAsync`：StartDate 起 7 天内有效），无 Plan、过期（>7 天）或生成失败 → 回退既有逻辑（用户永远有内容可学）。前端以「来自今日计划」徽标标示（WordDisplay / SentenceCard / 短文库推荐区）。
- **重规划（T-007）**：`GenerateAsync(force: true)` 同日已有 Plan 时原地重建内容（`(UserId, StartDate)` 唯一不破，`CreatedAt` 刷新）；由瓶颈性质变化（`planner:replan:{userId}:{yyyyMMdd}`）或每周兜底（`planner:weekly:{userId}:{ISO 周}`）触发。
- **探索周任务编排（T-032）**：注册起 7 天为探索周，`ColdStartExplorationService` 每日按 taxonomy 轮转选 1 个子场景（优先词池已标注场景，无标注回退全 taxonomy）出 1 道轻量情境表达题（1–2 句即可），经 `GET /api/planner/current` 响应附带的 `exploration` 字段下发（第 x/7 天、证据条数、还差 N 条、今日任务场景与题目）；表达走既有 free-expression 评分链路（评分与 T-022 回写不动），目的是攒画像证据，当天不做不惩罚、跳过后补；证据计数 = SentenceLogs + FreeExpressionLogs，N = max(0, 10 − 条数）。

### 5.16 瓶颈性质洞察 + 重规划触发（I3 T-007）

- **三层机制**（设计见 `docs/DESIGN-bottleneck-insight.md`）：指标筛查（规则、零 LLM、日级）→ InsightAgent（LLM、事件驱动、细读产出原文）→ 重规划（事件驱动 + 每周兜底）；洞察只影响解读与规划，不改任何分数。
- **指标筛查**（`BottleneckScreeningService`，随 `ProfileScoreSnapshotWorker` 日级运行；T-033 信号口径 v2，设计见 `docs/DESIGN-insight-signals-v2.md`）：四类信号满足其一即触发——平台期（近 12 次产出四维均分斜率≤0.05 且标准差≤1.0、窗口跨度 ≤30 天）、回避模式（近 12 次样本复杂连接使用率后半段 ≤ 前半段 ×0.5 且前半段率 >0——相对自身基线，不设绝对下限，从未用过连接词的不判回避）、零起步（近 10 次产出复杂连接恒 0 且后半段平均句长 ≤ 前半段 ×1.1、窗口跨度 ≤30 天——只触发不定性，性质交 InsightAgent 细读）、安全词策略（生效 Plan 造句目标词在最近 5 篇自由产出中出现率为 0——窗口按篇数跨计划周期累计，新 Plan 24h 宽限期保留；多词短语目标拆词去停用词取内容词、全部同现才算用过）；规则只判「要不要细看」，不触发零 LLM 成本。触发入队 `BottleneckInsight` 任务（幂等键 `insight:{userId}:{yyyyMMdd}`）。
- **InsightAgent**（`BottleneckInsightService`，ILLMProvider 第 8 个方法 `GenerateBottleneckInsightAsync`）：取近 20 条 SentenceLogs **原文** + 当前生效 Plan 主攻方向细读，产出 `BottleneckInsights` 落库——瓶颈性质 7 分类（词汇量不足 / 会词但组织不成句 / 语法错误多 / 语法正确但表达单调 / 回避模式 / 中式搭配 / 安全词策略）+ 一句中文结论 + SentenceLog 证据引用（沿用画像证据纪律：编造/越权 id 持久化前机械过滤）；同日幂等（已有当日洞察直接返回，零 LLM）。
- **性质变化判定**：与上一条洞察比对（Plan 主攻方向由最近一次洞察驱动，两者等价）——首次发现或性质不同 = 已变 → 事件驱动重规划：重生成画像（`WeaknessProfileService.GenerateAsync(assessmentId: null)`，幂等维度按日）→ 入队 force Planner；性质相同 → 仅记录（`ReplanTriggered=false`）。
- **每周兜底**（`WeeklyReplanWorker`，24h 检查）：所有完成初测的存量用户按 ISO 周入队 force Planner（幂等键 `planner:weekly:{userId}:{yyyy}-W{ww}`）——补齐 T-006「无测评用户不获新 Plan」缺口。
- **端点**：`POST /api/insights/bottleneck/jobs`（手动跑筛查，触发则入队，幂等按日）、`GET /api/insights/bottleneck/latest`（最新洞察；自 I6 T-019 起供前端「学习洞察」卡用户可见展示）。
- Mock 洞察由信号与真实分数确定性推导性质，结论带 [Mock] 前缀。

### 5.17 词毕业四阶段生命周期（I4 T-014）

- **状态机**（`WordLifecycleService` 纯规则，设计见 `docs/DESIGN-word-lifecycle.md`）：`recognized`（认识·看词知义）→ `recalled`（回忆·看义想词）→ `prompted_use`（造句使用·产出候选池）→ `spontaneous_use`（自发使用·毕业）。存 `UserWordRelationships.LifecycleStage`（枚举存字符串）+ `StageUpdatedAt`/`PromptedUseConfirmedAt`/`GraduatedFreeExpressionLogId`。
- **推进**：认识→回忆 = SM-2 调度内看词知义连续正确达成熟阈值（`RepeatCount≥2`，复用 repetitions/interval 口径）；回忆→造句使用 = 回忆模式考察通过（看义正确拼出词）→ 进产出候选池；造句使用→待自发 = 提示造句中正确使用（目标词命中 + A/B 档，`PromptedUseConfirmedAt` 留痕）；待自发→毕业 = 自由表达中自发出现且当次评分达标（同一命中口径做词级判定），留痕所在 `FreeExpressionLog` Id。
- **命中口径（T-040，`TargetWordMatcher` 纯函数，Domain）**：所有生命周期命中判定（造句确认、使用错误回退、自发毕业）统一走这一个工具，不再各自分词副本——单词走词边界匹配（不误伤子串）；多词短语按词序列连续匹配（大小写不敏感、容忍标点/多余空白分隔，"up, in arms,"、"up  in  arms" 均命中；词序必须一致，乱序/中间插词不命中；不做词形变换，原样小写词序列匹配）。修复前词边界分词只产单词 token，多词 lemma（up in arms 等）恒判未命中——prompted_use 永不确认、永不毕业。瓶颈筛查的安全词信号仍用自己的内容词口径（T-033 `BottleneckScreeningService`），与本口径分开。
- **回退**：仅造句使用阶段——产出证据显示不会用（句中含目标词但 D 档或词汇维 ≤2）→ 退回回忆阶段重进 SM-2 调度（RepeatCount/Interval 归零）；认识/回忆阶段不回退（SM-2 管遗忘调度）。
- **自评职责收窄**：Remembered/Forgot 只改 SM-2 排程参数（interval/repetitions）与接触词排程输入（EstimatedKnownRate/PersonalDifficulty EMA），**不再参与掌握度与 Score**——`MasteryScore` 由阶段派生（25/50/75/100，recognized/recalled 只算「认识」、prompted_use 算「会用」、spontaneous_use 才算「毕业」）；Score 写入点不变（测评/挑战/后台造句评分三处，均不经自评路径）。
- **Planner 编排**：产出候选池（prompted_use 未确认、带内、utility 非 low）优先编入每日造句目标（见 §5.15）；确认过或已毕业的词不再重复编排。**T-034 二级补位**：prompted_use 池空时接 Recalled 池（最早进阶段的优先），两级都空才落当日带内词。
- **背词考察模式**：`/api/words/daily` 按阶段返回 `stage`/`quizMode`（认识=看词知义答释义，回忆及以后=看义想词答拼写）；`/api/learning/submit` 按 `mode` 判定正确性，响应带阶段与下次考察模式；前端 WordCard 随模式切换题面（看义想词模式隐藏单词、提交后揭示）并显示阶段徽标。**T-034 回忆考察位**：每日词队列 ≥40% 名额给成熟待推进老词（见 §5.1），解决「老词成熟后很少再被抽到」的曝光瓶颈。
- **毕业时刻可见（T-034）**：自由表达评分响应带 `graduatedWords`（本次毕业词 lemma 列表），前端自由表达结果区弹毕业提示；`GET /api/words/graduated` 返回当前用户已毕业词列表（含毕业时间），Dashboard 计划卡下方显示本周毕业计数（无则不显示），词库（`/word-bank`）行内加「已毕业」标记。
- **存量映射**：幂等补丁 SQL 回填——SM-2 已成熟（RepeatCount≥2）→ recalled，掌握度按阶段派生；Development 删库重建下新关系默认 recognized。

### 5.18 Agent 价值用户可见（I6 T-018/T-019）

- **今日学习计划卡**（Dashboard）：消费 `GET /api/planner/current`——主攻场景中文名（经 `GET /api/scenarios` 映射）、第 x/7 天、今日带内词/接触词数、造句目标词、来源徽章（`sourceFindingIds` 非空=「个性化·依据你的弱点画像」；空=探索期：T-032 起探索周内显示「探索周·第 x/7 天」徽章 + 进度文案「再完成 N 次表达，生成你的专属画像」+ 今日探索任务题目与「去写今日表达」入口——跳造句工作室并默认落到自由表达 Tab，横幅带今日题目；无 Plan 但探索周内同样显示该进度与入口；探索周外回退「探索期·积累数据后更精准」）；画像低置信 Finding 在 LevelPanel 带「初步」徽标（T-032）。
- **学习洞察卡**（Dashboard）：消费 `GET /api/insights/bottleneck/latest`——瓶颈性质中文名+人话解释（前端 `NATURE_META` 7 类映射）、Agent 结论、时间、「已为你调整学习计划」徽章（ReplanTriggered）；不暴露证据 id 等内部字段；无洞察显示「状态良好」文案。
- 两卡均为前端纯增量（`hooks/useLearningPlan.ts` / `useBottleneckInsight.ts`），请求失败/加载中静默不渲染。

## 6. API 全量清单

除健康检查与注册/登录外全部需要 JWT Bearer。

**认证**
- `POST /api/auth/register`（匿名）— 邮箱注册，直接发 JWT
- `POST /api/auth/login`（匿名）
- `GET /api/auth/me`

**单词与学习**
- `GET /api/words`、`GET /api/words/{id}`、`POST /api/words`（LLM 自动定级）
- `GET /api/words/daily?count=`
- `GET /api/words/graduated`（T-034：当前用户已毕业词列表，含毕业时间）
- `POST /api/learning/submit`
- `GET /api/progress`
- `POST /api/llm/rate-difficulty`

**拼写 / 造句 / 自由表达**
- `GET /api/spelling/queue`、`POST /api/spelling/submit`
- `GET /api/sentences/prompts`、`POST /api/sentences/rate`
- `POST /api/free-expression/rate`

**阅读**
- `GET /api/articles?level&cefr`、`GET /api/articles/{id}`、`GET /api/articles/recommended`（当日 Plan 阅读推荐）
- `POST /api/articles/{id}/reading/start`、`POST /api/reading-logs/{logId}/finish`、`POST /api/reading-logs/{logId}/lookup`
- `POST /api/articles/{id}/vocab-extract`、`GET /api/articles/{id}/vocab`
- `POST /api/articles/{id}/lookup`（前端未用，前端走 `/api/reading/lookup`）
- `GET/POST /api/articles/{articleId}/comments`
- `POST /api/reading/lookup`
- `POST /api/reading/agent`（前端未用）

**测评 / 挑战 / 等级**
- `POST /api/assessment/initial/start`、`POST /api/assessment/initial/skip`
- `GET /api/assessment/{id}/next-block`（自适应块，幂等）
- `POST /api/assessment/{id}/blocks/{blockIndex}/submit`（块提交，收敛时定级）
- `GET /api/assessment/{id}`
- `POST /api/challenge/start`、`POST /api/challenge/submit`、`GET /api/challenge/recent`
- `GET /api/level/dashboard`、`GET /api/level/history`

**个人中心 / Score / 反馈**
- `GET /api/profile`、`PUT /api/profile`
- `GET /api/profile/llm/presets`、`PUT /api/profile/llm`
- `GET /api/profile/scores`、`GET /api/profile/scores/history?days=`
- `GET /api/profile/weakness`（最新 WeaknessProfile + Finding 核查状态）
- `GET /api/evaluation/latest`、`GET /api/evaluation/{id}`
- `POST /api/feedback`
- `GET /api/tools`、`POST /api/tools/{name}`

**学习计划（T-006）**
- `POST /api/planner/jobs`（手动触发当日 Planner 任务，幂等按日）
- `GET /api/planner/current`（当日有效 Plan 摘要）

**瓶颈洞察（T-007）**
- `POST /api/insights/bottleneck/jobs`（手动跑指标筛查，触发则入队 InsightAgent，幂等按日）
- `GET /api/insights/bottleneck/latest`（最新 BottleneckInsight：性质/结论/证据引用/是否触发重规划）

**日志 / 健康**
- `GET /api/logs/summary`、`GET /api/logs/recent`
- `GET /api/health`（匿名）、`GET /api/health/details`（匿名；DB CanConnect + LLM 注册检查）

## 7. 数据模型

PostgreSQL，连接串 `ConnectionStrings:PostgreSql`（默认 `Host=localhost;Database=nextword`）。`ApplicationDbContext` 共 **31 个 DbSet**：

Users、UserLlmSettings、Words、WordScenarios、WordDifficultyAnnotations、DifficultyAnnotations、UserProgress、UserWordRelationships、WordLearningLogs、Sentences、SentenceLogs、FreeExpressionLogs、SpellingLogs、Articles、ReadingLogs、ArticleComments、ArticleVocabMappings、Assessments、AssessmentRecords、ChallengeRecords、ChallengeSessions、LevelHistories、LearningEvents、ProfileScoreSnapshots、EvaluationReports、WeaknessProfiles、ProfileFindings、LearningPlans、BottleneckInsights、BackgroundJobs、UserFeedbacks、UserWordExcludes。

- 列表属性（Meanings、ErrorTags 等）存 JSON 列；枚举存字符串。
- 6 个 EF 迁移（`Data/Migrations/`）。`AddScoreKernelM1`/`AddChallengeSession` 为 SQLite 口味，T-015 起在 PG 上由 `ActiveProvider` 守卫跳过，对应 schema 由 `PostgreSqlSchemaPatcher` 嵌入式 SQL（`Patch_PostgreSql_ScoreKernel.sql`）幂等补齐；I1–I4 全部变化（WordScenarios、Words 场景标注列、WeaknessProfiles/ProfileFindings、LearningPlans、BottleneckInsights、BackgroundJobs.StartedAt/RetryCount、UserWordRelationships 生命周期四列）由 `ConsolidateI1ToI4Schema` 收口——PG 分支为幂等 SQL（与补丁口径一致、可共存），非 PG 路径走生成代码。启动时 MigrateAsync 在空库一次建全（失败即抛错，不再吞错），补丁随后全量 no-op。
- 启动时 Development 或 `Database:AutoMigrate=true` 自动迁移 + 种子（演示用户、6 词、10 句、21 篇文章）。
- 生产部署走 SQL 脚本：`Backend/Scripts/generate-migration-sql.ps1` → `Scripts/Migrations/Upgrade_Idempotent.sql`（runbook 在同目录 README）。

## 8. 前端页面与路由

路由见 `Frontend/src/App.tsx` 与 `src/navigation/routes.ts`；登录页不是路由（未认证直接渲染 `LoginPage`）。

| 路径 | 页面 | 功能 |
|---|---|---|
| `/dashboard` | Dashboard | 今日学习计划卡 + 学习洞察卡（I6）+ 5 个模块卡片（新词/拼写/造句/阅读/复习）+ 今日任务量 Badge |
| `/learn` | WordCard | 按生命周期阶段切换考察模式（看词知义/看义想词）+ 阶段标识，SM-2 调度，Remembered/Forgot 自评只影响排程 |
| `/spelling` | SpellingMode | 听写拼写 + 逐字母错误高亮 |
| `/sentence` | SentenceStudio | 造句评分 / 自由表达 双 Tab |
| `/reading` | ArticleLibrary | 按难度筛选、难度/CEFR 分组 |
| `/reading/:articleId` | ArticleReader | 点词查义弹层、词汇提取面板、评论线程、阅读计时 |
| `/assessment` | InitialAssessment | 自适应分块测评（2–3 块，产出为主）+ 结果页 |
| `/challenge` | ChallengeMode | 三阶段挑战 + 近期挑战列表 |
| `/review` | ReviewQueue | 翻转卡片复习 + 活动统计 + 最近记录 |
| `/word-bank` | Home | 全量词条表格 + 搜索 + 详情 |
| `/profile` | ProfilePage | 用户信息、LevelPanel、ProgressDetail、CEFR 开关、管理入口 |
| `/manage` | ManagePage | LLM 设置抽屉、测评/挑战/词库/学习数据入口 |
| `/level`、`/progress` | — | 重定向到 `/profile` 锚点 |

- 底部主导航实际只有「首页 / 我的」两个 Tab；其余功能经 Dashboard 卡片进入。
- 定义了但前端未调用的 API：`/api/reading/agent`、`/api/llm/rate-difficulty`、`/api/profile/scores/history`、`/api/level/history`、`/api/articles/{id}/lookup`。

## 9. 启动与配置

- 本地开发：`docker compose up -d postgres` → `dotnet run --launch-profile http`（Backend/NextWord.Api，:5108）→ `npm run dev`（Frontend，:5173，/api 代理 :5108，可用 `VITE_API_PROXY_TARGET` 覆盖，例如 API 跑 Docker 时指 8080）。
- 全容器：`docker compose up -d`（postgres + redis + api:8080，Production 环境，AutoMigrate=true）。
- 关键配置（`appsettings.json` / `.Development` / `.Production` / `.Testing`）：
  - `ConnectionStrings:PostgreSql` / `ConnectionStrings:Redis`、`Database:AutoMigrate`、`Cache:Provider`（Memory|Redis）
  - `Auth:JwtSecret`（**生产必须覆盖**，默认值仅开发用）/ `Issuer` / `Audience` / `ExpirationDays:7`
  - `Llm:OpenAI:Enabled`（默认 false → Mock；Production 配 true）/ `Model`（gpt-4o-mini）/ `ApiKey` 或 `ApiKeyEnvironmentVariable`（默认 `OPENAI_API_KEY`）/ `BaseUrl`（可选，OpenAI 兼容端点）
  - `Llm:SentenceRating:ExplanationLanguage`（zh-CN）、`ScoreMapping`（CEFR 分带）、`ChallengeThresholds`、`Search`（DuckDuckGo）
- 前端 API base：axios `baseURL = VITE_API_BASE_URL ?? ''`（默认相对路径 + dev 代理）。
- `Backend/NextWord.Api/nextword-dev.db*` 是 SQLite 时代遗留文件，当前代码不使用。

## 10. 测试

**单元测试**（`NextWord.UnitTests`）：SM-2、LevelUpgradeEngine（锁级/升级候选/C1 封顶）、EffectiveDifficultyCalculator、AssessmentScoringService（表达力综合分/自适应决策）、AdaptiveAssessmentService（分块/词池/收敛/定级，连真实 PG + LLM 桩）、WeaknessProfile（生成持久化与幂等、Verifier 篡改/伪造/样本量核查、报告 schemaVersion 2 切换与回退、解析枚举容错、T-010 画像去重，连真实 PG）、LearningPlan（Verified-only 主攻场景、接触词上限与超带、同日幂等、过期/无 Plan 回退、造句 Plan 目标、阅读推荐，连真实 PG）、BottleneckInsight（T-007：三类信号触发/不误触发、洞察落库证据过滤、性质变→重规划、性质未变→只记录、未触发零 LLM 计数桩、force 原地重建 Plan、每周兜底入队与幂等、解析容错，连真实 PG）、WordLifecycle（T-014：SM-2 成熟阈值边界推进、自评不改掌握度对比、回忆通过进候选池、造句确认/回退、自发毕业留痕、指定目标词不算自发、Planner 候选池优先、每日词阶段与考察模式，连真实 PG + 评分桩）、BackgroundJobReclaim（T-013：超时回收重跑、超限 Failed 留痕、未超时与空 StartedAt 边界，连真实 PG）、ScoreMappingService、ScoreProfileService（absolute 写入与幂等，连真实 PG `nextword_unit_test`）、LLM prompt/解析与 Mock Provider、ArticleVocabMapping 缓存、RedisCacheService 与 LlmTelemetryProvider。

**集成测试**（`NextWord.IntegrationTests`）：Testing 环境（移除后台 Worker），真实 PG `nextword_test`，真实注册/登录拿 JWT。覆盖：测评（401、开始、进度）、文章（401、种子列表）。覆盖较薄。

**E2E**（`Frontend/e2e`，Playwright，5 用例）：新用户自动跳测评、管理页进测评、短文库/阅读器、挑战 API 计分、挑战页 UI。`playwright.config.ts` 自动拉起后端（:5108 健康检查）与前端（:5173）。

```bash
docker compose up -d postgres   # 测试依赖
cd Backend && dotnet test       # 单元 + 集成
cd Frontend && npm run test:e2e # E2E
```

## 11. 已知限制

- 画像场景/阅读维度依赖学习行为数据：初测后新用户无背词/阅读记录，首轮画像以技能维度为主（场景/阅读随学习积累出现）；此时 LearningPlan 主攻场景走场景词覆盖率兜底。
- 瓶颈洞察（T-007）先服务重规划、不做用户可见展示；多瓶颈并存只取最主要性质（优先级排序留待迭代）；「性质是否变化」与上一条洞察比对（近似「与当前 Plan 主攻方向比对」）。
- Mock LLM 下新词自动定级无效（未知词回退 Basic/A1）；Mock 画像/洞察结论带 [Mock] 前缀，不代表个性化成立。
- 集成测试与 E2E 覆盖较薄；`npm run test:e2e` 全绿尚未纳入常规验证。
- `/api/reading/agent` 无用户级限流。
- Release Blockers B1–B8 未正式 sign-off（详见 next-steps.md）。
