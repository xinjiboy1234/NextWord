# NextWord 当前状态（Current State）

> 版本：2026-07-20。本文描述**已实现并验证**的现状，是项目功能的权威参考。
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
  NextWord.Api.Endpoints/ 全部 Minimal API 端点（17 个端点类，纯 HTTP 层）
  NextWord.Domain/        27 个实体、9 个枚举、接口契约、领域服务（SM2/Score 映射/等级引擎/Prompt 工厂）
  NextWord.Infrastructure/ EF Core + Npgsql、仓储、约 25 个业务服务、JWT/密码、4 个后台 Worker、缓存
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

- `GET /api/words/daily?count=`：按用户 Vocabulary 分取 `[score, score+12]` 难度带单词 + `EstimatedKnownRate<0.4` 弱词，各占约一半（`DailyWordSelectionService`）。
- `POST /api/learning/submit`：提交词义作答 → SM-2 排程更新 + `MasteryScore`/`EstimatedKnownRate`/`PersonalDifficulty`（EMA）+ 连胜天数。
- SM-2 变体（`Sm2Service`）：EF 下限 1.3，间隔上限 3650 天。
- `POST /api/words` 新增单词时调用 LLM `RateDifficultyAsync` 自动定级（DifficultyLevel + CefrLevel + 0–100 IntrinsicScore 标注）。

### 5.2 拼写（`/spelling`）

- `GET /api/spelling/queue`：到期复习队列，无到期词时回退每日词。
- `POST /api/spelling/submit`：含逐字母错误位置标注；前端发音播放 + 错误高亮。

### 5.3 造句工作室（`/sentence`）

- 两个 Tab：指定词造句（`GET /api/sentences/prompts` + `POST /api/sentences/rate`）与自由表达（`POST /api/free-expression/rate`）。
- LLM 同步评分：语法/自然度/词汇/相关度各 0–5 + A–D 等级 + 改写建议；反馈语言默认 zh-CN（`Llm:SentenceRating:ExplanationLanguage`）。
- 后台 `SentenceLlmScoringWorker` 会把造句成绩写入 Score 内核（写作维度）。

### 5.4 阅读（`/reading`、`/reading/:id`）

- 短文库按难度/CEFR 筛选分组；种子含 21 篇分级短文。
- 阅读器：逐词渲染点词查义（`POST /api/reading/lookup`，先查 `ArticleVocabMappings` 文章级缓存，缺失再 LLM 并 upsert；返回音标 + 文中/其他场景双例句 + 熟悉度）。
- `POST /api/articles/{id}/vocab-extract`：LLM 提取重点词汇（含音标 + 用法例句）并持久化；存量数据 lazy backfill。
- 段落批注：`GET/POST /api/articles/{articleId}/comments`，可请求 AI 回复。
- 阅读日志：`reading/start` → `reading-logs/{logId}/finish`（计时、查词数参与评分）。
- `POST /api/reading/agent`：阅读助手 Agent（`ReadingAssistantAgent` 组合 skills）。**前端暂未使用**。

### 5.5 首次水平测评（`/assessment`）

- 4 步：词汇选择 → 拼写 → LLM 造句评分 → 阅读（查词数有惩罚）。
- `POST /api/assessment/{id}/complete`：`AssessmentScoringService` 按「最短板」定级（overall = min），写入 Score 内核，并入队 `EvaluationReport` 后台任务。

### 5.6 综合挑战（`/challenge`）

- `POST /api/challenge/start` 生成挑战包（`ChallengeSession` 存题目，客户端不拿答案）；可带 `confirmationChallenge` 锁定目标等级。
- `POST /api/challenge/submit`：客户端提交原始答案，服务端按 `ChallengeThresholds` 计分（词汇正确率 ≥0.6、写作 ≥53、阅读 ≥100、升级增量 5）；确认挑战通过则 ProfileScore 加 UpgradeDelta 并出评估报告。

### 5.7 Score 内核（v1）

- **模型**：`UserProgress` 持有 Vocabulary/Reading/Writing 三个 0–100 分；总分 = 三者最小值（最短板，`ScoreMappingService.ComputeOverall`）；CEFR 分带在 `appsettings.json` 的 `ScoreMapping`。
- **写入**：`ScoreProfileService.ApplyUpdateAsync` 是唯一入口，支持 absolute/delta；`LearningEvents.IdempotencyKey` 幂等去重。写入点：测评完成、确认挑战通过、后台造句评分。
- **快照**：`ProfileScoreSnapshotWorker` 每日写 `ProfileScoreSnapshots`，供 `GET /api/profile/scores/history?days=` 趋势图。
- **难度三层**：intrinsic（LLM 标注，持久化于 `WordDifficultyAnnotation`）→ personal（`EstimatedKnownRate`/`PersonalDifficulty` EMA）→ effective（`EffectiveDifficultyCalculator`，含学术语域加成）。
- **学习工具注册表**：`GET/POST /api/tools` 暴露 7 个工具（get_profile_scores、search_web(DuckDuckGo)、lookup_word_context、get_daily_words、get_evaluation_latest、get_challenge_history、get_recent_learning）。供 Agent 场景使用。

### 5.8 等级系统

- `LevelUpgradeEngine`：连胜 ≥3 且当前等级 ≥3 天，或 7 天内有挑战通过 → 升级候选；C1 封顶。
- `LevelCheckWorker` 每日刷新 `IsUpgradeCandidate`；`GET /api/level/dashboard`、`GET /api/level/history`。

### 5.9 个人中心与 LLM 设置（`/profile`、`/manage`）

- `GET /api/profile`：等级、连胜、统计、等级历史、LLM 设置；`PUT /api/profile` 改显示名。
- BYOK：`GET /api/profile/llm/presets`（OpenAI/DeepSeek/Qwen 预设）+ `PUT /api/profile/llm` 存用户自己的 OpenAI 兼容 API（`UserLlmSettings`，API key 脱敏返回）；`UserLlmProviderFactory` 按用户构建 provider。
- `GET /api/evaluation/latest|{id}` 查看评估报告（当前为模板 + 工具预取数据，LLM 结构化叙事未做）。
- `POST /api/feedback`：释义错误 / 标记已知 / 排除单词（触发 ReAnnotation 后台任务）。

### 5.10 后台 Worker（Infrastructure/Background，共 4 个 HostedService）

| Worker | 周期 | 职责 |
|---|---|---|
| `BackgroundJobWorker` | 2s 轮询 `BackgroundJobs` 表 | 处理 EvaluationReport / SentenceLlmScoring / ReAnnotation 三类任务 |
| `ReviewReminderWorker` | 6h | 刷新待复习数 |
| `LevelCheckWorker` | 24h | 刷新升级候选标记 |
| `ProfileScoreSnapshotWorker` | 24h | 写 Score 每日快照 |

Worker 异常不拖垮宿主（`BackgroundServiceExceptionBehavior=Ignore`）。

### 5.11 LLM 集成

- 统一抽象 `ILLMProvider`（5 方法：难度标注 / 释义 / 造句评分 / 词汇提取 / 批注回复）；Prompt 由 `LlmPromptFactory` 生成。
- 默认 `LlmMockProvider`：内置词典启发式，零外部调用；**未知词难度一律回退 Basic/A1**（未启用真实 LLM 时 `POST /api/words` 自动定级基本无效）。
- `Llm:OpenAI:Enabled=true` 且有 key 时切 `LlmChatClientProvider`（OpenAI `ChatClient`，默认 `gpt-4o-mini`，Temperature 0.1，异常自动回退 Mock）。
- 装饰链：Inner → `LlmRetryProvider`（指数退避 3 次）→ `LlmTelemetryProvider`（记录耗时与 ModelProfileId）。
- 用户级 BYOK 优先于服务端全局配置。

### 5.12 缓存

- `ICacheService` 由 `Cache:Provider` 切换 Memory（默认）/ Redis。
- 词义查询以 `ArticleVocabMappings` 表做 DB 级缓存；`POST /api/llm/rate-difficulty` 带 24h IMemoryCache（SHA256 key）。

## 6. API 全量清单

除健康检查与注册/登录外全部需要 JWT Bearer。

**认证**
- `POST /api/auth/register`（匿名）— 邮箱注册，直接发 JWT
- `POST /api/auth/login`（匿名）
- `GET /api/auth/me`

**单词与学习**
- `GET /api/words`、`GET /api/words/{id}`、`POST /api/words`（LLM 自动定级）
- `GET /api/words/daily?count=`
- `POST /api/learning/submit`
- `GET /api/progress`
- `POST /api/llm/rate-difficulty`

**拼写 / 造句 / 自由表达**
- `GET /api/spelling/queue`、`POST /api/spelling/submit`
- `GET /api/sentences/prompts`、`POST /api/sentences/rate`
- `POST /api/free-expression/rate`

**阅读**
- `GET /api/articles?level&cefr`、`GET /api/articles/{id}`
- `POST /api/articles/{id}/reading/start`、`POST /api/reading-logs/{logId}/finish`、`POST /api/reading-logs/{logId}/lookup`
- `POST /api/articles/{id}/vocab-extract`、`GET /api/articles/{id}/vocab`
- `POST /api/articles/{id}/lookup`（前端未用，前端走 `/api/reading/lookup`）
- `GET/POST /api/articles/{articleId}/comments`
- `POST /api/reading/lookup`
- `POST /api/reading/agent`（前端未用）

**测评 / 挑战 / 等级**
- `POST /api/assessment/initial/start`、`POST /api/assessment/initial/skip`
- `GET/POST /api/assessment/{id}/step/{step}`（step 1–4）
- `POST /api/assessment/{id}/complete`、`GET /api/assessment/{id}`
- `POST /api/challenge/start`、`POST /api/challenge/submit`、`GET /api/challenge/recent`
- `GET /api/level/dashboard`、`GET /api/level/history`

**个人中心 / Score / 反馈**
- `GET /api/profile`、`PUT /api/profile`
- `GET /api/profile/llm/presets`、`PUT /api/profile/llm`
- `GET /api/profile/scores`、`GET /api/profile/scores/history?days=`
- `GET /api/evaluation/latest`、`GET /api/evaluation/{id}`
- `POST /api/feedback`
- `GET /api/tools`、`POST /api/tools/{name}`

**日志 / 健康**
- `GET /api/logs/summary`、`GET /api/logs/recent`
- `GET /api/health`（匿名）、`GET /api/health/details`（匿名；DB CanConnect + LLM 注册检查）

## 7. 数据模型

PostgreSQL，连接串 `ConnectionStrings:PostgreSql`（默认 `Host=localhost;Database=nextword`）。`ApplicationDbContext` 共 **27 个 DbSet**：

Users、UserLlmSettings、Words、WordDifficultyAnnotations、DifficultyAnnotations、UserProgress、UserWordRelationships、WordLearningLogs、Sentences、SentenceLogs、FreeExpressionLogs、SpellingLogs、Articles、ReadingLogs、ArticleComments、ArticleVocabMappings、Assessments、AssessmentRecords、ChallengeRecords、ChallengeSessions、LevelHistories、LearningEvents、ProfileScoreSnapshots、EvaluationReports、BackgroundJobs、UserFeedbacks、UserWordExcludes。

- 列表属性（Meanings、ErrorTags 等）存 JSON 列；枚举存字符串。
- 5 个 EF 迁移（`Data/Migrations/`）；`AddScoreKernelM1` 含 SQLite 专用 ALTER，由 `PostgreSqlSchemaPatcher` 用嵌入式 SQL（`Patch_PostgreSql_ScoreKernel.sql`）在 PG 上幂等补齐。
- 启动时 Development 或 `Database:AutoMigrate=true` 自动迁移 + 种子（演示用户、6 词、10 句、21 篇文章）。
- 生产部署走 SQL 脚本：`Backend/Scripts/generate-migration-sql.ps1` → `Scripts/Migrations/Upgrade_Idempotent.sql`（runbook 在同目录 README）。

## 8. 前端页面与路由

路由见 `Frontend/src/App.tsx` 与 `src/navigation/routes.ts`；登录页不是路由（未认证直接渲染 `LoginPage`）。

| 路径 | 页面 | 功能 |
|---|---|---|
| `/dashboard` | Dashboard | 5 个模块卡片（新词/拼写/造句/阅读/复习）+ 今日任务量 Badge |
| `/learn` | WordCard | 看英文写中文释义，SM-2 调度，Remembered/Forgot 自评 |
| `/spelling` | SpellingMode | 听写拼写 + 逐字母错误高亮 |
| `/sentence` | SentenceStudio | 造句评分 / 自由表达 双 Tab |
| `/reading` | ArticleLibrary | 按难度筛选、难度/CEFR 分组 |
| `/reading/:articleId` | ArticleReader | 点词查义弹层、词汇提取面板、评论线程、阅读计时 |
| `/assessment` | InitialAssessment | 4 步测评 + 结果页 |
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
  - `Llm:OpenAI:Enabled`（默认 false → Mock；Production 配 true）/ `Model`（gpt-4o-mini）/ `ApiKey` 或 `ApiKeyEnvironmentVariable`（默认 `OPENAI_API_KEY`）
  - `Llm:SentenceRating:ExplanationLanguage`（zh-CN）、`ScoreMapping`（CEFR 分带）、`ChallengeThresholds`、`Search`（DuckDuckGo）
- 前端 API base：axios `baseURL = VITE_API_BASE_URL ?? ''`（默认相对路径 + dev 代理）。
- `Backend/NextWord.Api/nextword-dev.db*` 是 SQLite 时代遗留文件，当前代码不使用。

## 10. 测试

**单元测试**（`NextWord.UnitTests`）：SM-2、LevelUpgradeEngine（锁级/升级候选/C1 封顶）、EffectiveDifficultyCalculator、AssessmentScoringService（最短板）、ScoreMappingService、ScoreProfileService（absolute 写入与幂等，连真实 PG `nextword_unit_test`）、LLM prompt/解析与 Mock Provider、ArticleVocabMapping 缓存、RedisCacheService 与 LlmTelemetryProvider。

**集成测试**（`NextWord.IntegrationTests`）：Testing 环境（移除后台 Worker），真实 PG `nextword_test`，真实注册/登录拿 JWT。覆盖：测评（401、开始、进度）、文章（401、种子列表）。覆盖较薄。

**E2E**（`Frontend/e2e`，Playwright，5 用例）：新用户自动跳测评、管理页进测评、短文库/阅读器、挑战 API 计分、挑战页 UI。`playwright.config.ts` 自动拉起后端（:5108 健康检查）与前端（:5173）。

```bash
docker compose up -d postgres   # 测试依赖
cd Backend && dotnet test       # 单元 + 集成
cd Frontend && npm run test:e2e # E2E
```

## 11. 已知限制

- 评估报告为模板 + 工具预取数据，LLM 结构化叙事未实现。
- Mock LLM 下新词自动定级无效（未知词回退 Basic/A1）。
- 集成测试与 E2E 覆盖较薄；`npm run test:e2e` 全绿尚未纳入常规验证。
- `/api/reading/agent` 无用户级限流。
- Release Blockers B1–B8 未正式 sign-off（详见 next-steps.md）。
