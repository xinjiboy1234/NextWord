# NextWord 开发日志

按时间倒序记录需求、决策、实现与验收。

## 2026-07-20 — 文档集整理与本地开发代理修复

### 实现
- 新增根 `README.md`（功能、技术栈、快速启动、配置、测试）
- 重写 `docs/CURRENT-STATE.md`：对齐 React Router、全量 PostgreSQL、Score 内核 v1、Base UI 前端等现状
- 删除已完成/被取代的文档：`plans/` 全部 Phase 计划、`docs/SPEC-ai-learning-*`（4 份）、`docs/DESIGN-frontend-ux.md`、`docs/AUDIT-cefr-read-path.md`、`docs/superpowers/`、`英语学习.md`；活口待办并入 `next-steps.md`
- 保留 `docs/DESIGN-ai-learning-architecture.md`（架构 why）与 `docs/DESIGN-auth-profile.md`，补「已实现」状态标注
- 修复 `Frontend/vite.config.ts` 代理端口漂移：8080 → 默认 5108（可用 `VITE_API_PROXY_TARGET` 覆盖）

### 验收
- [x] 后端启动（`dotnet run`，:5108）：注册/登录、每日词、文章、profile、progress、匿名 401 全部实测通过
- [x] 前端 `npm run dev`（:5173）页面与 /api 代理实测通过

---

## 2026-07-19 — UI checkpoint

### 实现
- `front_design/` 7 个屏幕与 `Frontend` 同步（设计原型与实现并行维护）

---

## 2026-07-10 — fix：PG Score 内核补丁缺失列

### 问题
`AddScoreKernelM1` 在 PG 上无法走 EF 迁移，补丁曾标记已应用但未创建 `WordDifficultyAnnotations.DimensionsJson` 等列，导致阅读查词失败。

### 实现
- `Patch_PostgreSql_ScoreKernel.sql` 补齐缺失列（幂等）

---

## 2026-07-08 — 阅读查词例句与重点词汇增强

### 需求
- 查词/重点词：结构化双例句（文中场景 + 其他场景）+ 精髓说明，UI 点击「查看例句」展开
- 文章级缓存：先查 `ArticleVocabMappings`，缺失再 LLM 并 upsert
- 重点词汇提取：音标 + 用法例句持久化；存量数据按需 lazy backfill

### 实现
- `WordExample` 模型；`ArticleVocabMapping.Phonetics` / `ExamplesJson`
- Prompt/Parser/Mock 结构化 examples；vocab extract 含 phonetics + usageExample
- `ArticleVocabService.GetOrCreateWordDetailAsync` 统一缓存；`ReadingLookupService` 使用 `ArticleId`
- 扩展 `ReadingLookupResponse`、`ArticleVocabMappingDto`；前端 WordPopover / VocabExtractPanel
- Migration `20260708161532_AddArticleVocabPhoneticsAndExamples` + Upgrade/Rollback SQL

### 验收
- [x] `dotnet test` 54 通过
- [x] `npm run build` 通过

---

## 2026-07-08 — Base UI 前端重构 + Score 日快照 + PG schema 补丁

### 需求
按 `2026-07-07-base-ui-frontend-redesign` spec（已随文档整理删除，内容已全部落地）重构前端：`@base-ui/react` 组件封装、黑白主题、沉浸式首次测评 Onboarding + 跳过测评。

### 实现
- 前端：`@base-ui/react` 封装层（`src/components/ui/`：Button/Dialog/Drawer/Select/Switch/Tabs/Badge/Progress/RadioGroup）、黑白主题 tokens、底部导航精简为「首页/我的」
- 首次测评沉浸式 `OnboardingLayout` + `POST /api/assessment/initial/skip`（可跳过，默认 A2）
- 后端：`ProfileScoreSnapshotWorker`（Score 每日快照）+ `GET /api/profile/scores/history`
- `PostgreSqlSchemaPatcher` + `Patch_PostgreSql_ScoreKernel.sql`：PG 上幂等补齐 Score 内核 schema
- `ApplicationDbContextFactory`（design-time Npgsql）

---

## 2026-07-06 — 全环境切换 PostgreSQL（移除 SQLite）

### 需求
开发、默认、生产环境统一使用 PostgreSQL，不再使用 SQLite。

### 实现
- `appsettings.json` / `appsettings.Development.json`：`Database:Provider` → `PostgreSql`，移除 `Sqlite` 连接串
- `DependencyInjection.cs`：固定 `UseNpgsql`，移除 SQLite 分支；保留 `PendingModelChangesWarning` 忽略（模型快照与历史迁移仍有差异）
- 移除 `Microsoft.EntityFrameworkCore.Sqlite` 包引用
- 新增 `appsettings.Testing.json`（集成测试库 `nextword_test`）
- 单元/集成测试改为 PostgreSQL；docker init 脚本创建 `nextword_test` / `nextword_unit_test`
- 测试库 bootstrap：`EnsureCreated` + 首次运行重建库

### 验收
- [x] `dotnet build` 通过
- [x] `dotnet test` 45 通过（需 `docker compose up -d postgres`）

### 本地开发
```powershell
docker compose up -d postgres
cd Backend/NextWord.Api
dotnet run
```
Development 启动时自动 `MigrateAsync()` 到 `nextword` 库。

---

## 2026-07-06 — SQLite 排序修复 + 生产 SQL 迁移流程

### 需求
- 开发环境：`BackgroundJobWorker` SQLite 不支持 `DateTimeOffset` SQL ORDER BY
- Docker/生产：`PendingModelChangesWarning`（Snapshot 被 `AddScoreKernelM1` 污染为 SQLite 类型）
- 生产 Schema 改为 SQL 脚本迁移，发布前本地抽取

### 实现
- 7 个文件改为先 `ToListAsync()` 再内存排序（BackgroundJob / Challenge / Spelling / LearningTool / Endpoints）
- `ApplicationDbContextFactory`（Design-Time 默认 Npgsql）
- EF 迁移 `20260706132041_AlignPostgresModelSnapshot`（TEXT → uuid/timestamptz 等）
- `Scripts/generate-migration-sql.ps1` → 输出 `Scripts/Migrations/Upgrade_Idempotent.sql`
- `Program.cs`：仅 Development 自动 `MigrateAsync()`；Production 走 SQL
- `Scripts/Migrations/README.md` 发布 runbook

### 验收
- [x] `dotnet ef migrations has-pending-model-changes`（PostgreSql）→ No changes
- [x] `dotnet test` 45 通过
- [x] `Upgrade_Idempotent.sql` 已生成

### 生产部署
1. `.\Backend\Scripts\generate-migration-sql.ps1`
2. `psql "$DATABASE_URL" -f Backend/Scripts/Migrations/Upgrade_Idempotent.sql`
3. 可选：`Upgrade_ScoreBackfill.sql`
4. 部署 API（Production 不自动 Migrate）

---

## 2026-06-30 — Score 内核 v1 批量落地

### 需求
按 `docs/SPEC-ai-learning-*` 一次性完成 Layer 0–5：Score Profile 内核、初测/挑战服务端计分、阅读查词 AI、每日词 Score 驱动、评估报告、前端 Score 展示；FR-6 选项 A（真实挑战 UI + 服务端阈值）。

### 后端
- M1 迁移 `AddScoreKernelM1` + `AddChallengeSession` 已 apply
- `ScoreMappingService` / `ScoreProfileService`（唯一写入路径）/ `EffectiveDifficultyCalculator`
- `AssessmentService.CompleteInitialAsync` → Profile 更新 + 评估/造句 LLM 任务入队
- `ChallengeService` 重写：`ChallengeSession` 存包，客户端提交原始答案，服务端计分
- `ReadingLookupService`、`DailyWordSelectionService`、`EvaluationReportService`（模板报告）
- `BackgroundJobWorker`、`SentenceLlmScoringWorker`
- `DuckDuckGoSearchService` + `LearningToolRegistry`（7 handlers）+ `/api/tools`
- 学习提交 `ApplyKnownRateEma` 更新 `EstimatedKnownRate` / `PersonalDifficulty`

### 前端
- `types/score.ts`、`useProfileScores`、`useEvaluationReport`
- `ChallengeMode` 三阶段 UI，提交原始答案（无客户端 correctIndex）
- `LevelDashboard` Score 维度 + 评估报告轮询
- `InitialAssessment` 定级结果展示 Score
- `useWordLookup` → `POST /api/reading/lookup`；`WordPopover` 熟悉度
- `AppShell` 侧栏 CEFR + Score；每日词 count=10

### 验收
- [x] `dotnet test` 39 unit + 6 integration 通过
- [x] `npm run build` 通过
- [x] `dotnet ef database update`（含 ChallengeSessions）

### 未完成 / 已知缺口
- T-005 staging backfill drill 实际执行（脚本与 README 已就绪）
- 评估报告 LLM 结构化（已有 toolPrefetch 字段）
- Annotation lookup singleflight
- Release Blockers B1–B8 正式 sign-off

---

## 2026-06-30 — Score 内核收尾（T-043 / FR-7 / E2E）

### 实现
- `ProfileScoreSnapshotWorker` 日批 + `GET /api/profile/scores/history`
- `ReAnnotationWorker` + `UserFeedbackService`（DefinitionWrong / MarkKnown / ExcludeWord）
- `EvaluationDataAssembler` 预取工具数据写入报告
- 前端 `FeedbackButton`、`useDisplaySettings` CEFR toggle
- E2E `challenge.spec.ts`；CEFR read-path audit；`Scripts/README_BackfillDrill.md`

### 验收
- [x] `dotnet test` 45 通过
- [x] `npm run build` 通过

---

## 2026-06-27 — AI 学习架构叙事归档

### 背景
产品方向讨论：从五项 AI 体验需求（评价化等级、Agent 工具、DuckDuckGo、AI 每日词、阅读查词 AI）出发，梳理定级机制，进而重估 CEFR 在系统中的角色。

### 决策记录
- **定级与评价不冲突**：规则引擎产出权威 Score/等级；LLM 产出叙事评价，不改定级结果
- **CEFR 降级为映射层**：内部以 DifficultyScore (0–100) + User Profile 为核心；CEFR 仅展示与互操作
- **AI 判官 + CEFR 翻译官**：AI 负责标注、解释、辅导；规则负责 SM-2、升级、复习
- **收紧原则**：AI 标注持久化（非全链路实时）；区分 intrinsic / personal difficulty；规则引擎不可省略

### 产出
- 新增 `docs/DESIGN-ai-learning-architecture.md`（完整来龙去脉、理想分层、待决问题）
- §10 待决问题随后在产品规格中全部锁定，并由 Score 内核 v1（06-30）实现

---

## 2026-06-26 — 前端 UX 换皮 P3：React Router + 挑战历史

### 实现
- `react-router-dom`：`BrowserRouter` + `Routes` 替代 `view` state
- `navigation/routes.ts`：路径映射（词库 `/word-bank`，阅读 `/reading/:articleId`）
- `AppShell` 改用 `useLocation` / `useNavigate`
- `ArticleReaderRoute`：`useParams` 包装阅读页
- `ChallengeRecentList` 挂到挑战页空闲态，调用 `/api/challenge/recent`
- 未完成初测时 `navigate('/assessment', { replace: true })`
- E2E：`helpers.ts` 注册登录 + API 跳过初测；Vite 代理改为 `:5108`

### 验收
- [x] `npm run build` 通过
- [x] `npm run test:e2e` 3/3 通过

---

## 2026-06-25 — 句子评分中文反馈 + 测评流程导航优化

### 实现
- `Llm:SentenceRating:ExplanationLanguage` 配置：error_analysis 与 suggestion 按指定语言（默认 zh-CN）输出
- `ExplanationLanguageHelper`；Mock Provider 同步支持
- Initial Assessment 各阶段独立题目索引，Timeline 步骤跳转改进

---

## 2026-06-24 — 认证与个人中心

### 需求
邮箱注册/登录、JWT 认证、个人主页、用户级 LLM 配置（BYOK）。

### 实现
- `User` 扩展 Email/PasswordHash（PBKDF2-SHA256）；`UserLlmSettings` 1:1
- JWT HS256 发 token；全站授权 FallbackPolicy，匿名仅健康检查与注册/登录
- OpenAI/DeepSeek/Qwen 预设；`IUserLlmProviderFactory` 按用户构建 provider
- 前端 AuthContext + 登录页 + ProfilePage（等级、统计、LLM 设置）
- 设计文档：`docs/DESIGN-auth-profile.md`

---

## 2026-06-24 — 主导航重构与首次测评自动引导

### 需求
1. 测评、挑战、词库移至「我的」菜单
2. 其余功能以卡片形式展示在主界面（登录后默认首页）
3. 未完成首次测评时自动进入测评流程（取代黄色引导横幅）

### 实现
- 新增 `Dashboard.tsx` 卡片首页（学习、拼写、造句、阅读、等级、复习、进度）
- `App.tsx` 精简顶栏为「返回首页」+「我的」；默认视图改为 dashboard
- `ProfilePage` 增加「更多功能」区块（测评、挑战、词库）
- `InitialAssessment` 支持 `autoStart` 与 `onComplete` 回调
- 移除 `OnboardingBanner` 使用；进度加载完成前显示加载态避免首页闪烁

### 验收
- [x] npm run build 通过

---

## 2026-06-19 — Phase 6 生产增强

### 需求
Redis 缓存、docker-compose 生产栈、LLM 遥测、EF snapshot/迁移对齐、Playwright E2E、升级候选横幅。

### 决策
- Cache:Provider 切换 Memory / Redis，应用层仍用 ICacheService
- LLM 链：Inner → LlmRetryProvider → LlmTelemetryProvider
- 合并 hand-written Phase3/5 迁移为正式 EF 链 `Phase6AssessmentAndWorkersSync`
- SQLite 不兼容的 OrderBy(DateTimeOffset/Guid.NewGuid) 改为内存排序

### 实现
- RedisCacheService + StackExchangeRedis DI
- LlmTelemetryProvider（耗时 + ProfileId 日志）
- docker-compose：postgres + redis + api（PostgreSql + Redis 缓存）
- ApplicationDbContextModelSnapshot 补齐 Assessment 实体
- Playwright：reading + assessment E2E（2 用例通过）
- UpgradeCandidateBanner 前端
- Worker SQLite 修复；Host BackgroundServiceExceptionBehavior=Ignore
- CommentService / AssessmentService SQLite 查询修复
- useAssessmentFlow POST 补 `{}` body
- 单元测试 +3（RedisCache、LlmTelemetry）

### 验收
- [x] `dotnet test` 15/15 通过（单元 12 + 集成 3）
- [x] `npm run build` 通过
- [x] `npm run test:e2e` 2/2 通过

---

## 2026-06-19 — Phase 5 集成测试 + 引导 + 后台任务

### 需求
落实 next-steps P0：集成测试、首次测评引导、复习/等级后台 Worker。

### 实现
- UserProgress 增加 PendingReviewCount、IsUpgradeCandidate
- ReviewReminderWorker（6h）、LevelCheckWorker（24h）
- Progress API 返回 hasCompletedInitialAssessment / isUpgradeCandidate / pendingReviewCount
- NextWord.IntegrationTests：Article + Assessment 共 3 用例
- 前端 OnboardingBanner 引导未完成初测用户

### 验收
- [x] `dotnet test` 12/12 通过（单元 9 + 集成 3）
- [x] npm run build 通过

---

## 2026-06-19 — Phase 4 完善与优化

### 需求
缓存层、LLM 重试、单元测试、Docker 部署、HealthChecks、前端错误边界。

### 实现
- ICacheService + MemoryCacheService（开发环境）
- LlmRetryProvider 装饰 ILLMProvider（指数退避 3 次）
- NextWord.UnitTests：Sm2、AssessmentScoring、LevelUpgrade（9 用例通过）
- Dockerfile + docker-compose.yml
- /api/health/details HealthChecks
- ErrorBoundary、LoadingSkeleton 组件

### 验收
- [x] `dotnet test` 9/9 通过
- [x] dotnet build 通过
- [x] npm run build 通过

---

## 2026-06-19 — Phase 3 测评与挑战

### 需求
5 步初测、挑战测评、等级升降、等级历史与前端测评流程。

### 决策
- 测评编排由确定性 AssessmentService 完成，LLM 仅用于造句（复用既有能力）
- 短板定级：overall = min(vocab, sentence, reading)
- 挑战包预生成（ChallengePackGenerator）
- AssessmentRecord 用 JSON 存题目/答案/分数

### 实现
- Assessment / AssessmentRecord / ChallengeRecord / LevelHistory 实体与迁移
- AssessmentScoringService、LevelUpgradeEngine、ChallengePackGenerator
- API：/api/assessment、/api/challenge、/api/level
- 前端：InitialAssessment、ChallengeMode、LevelDashboard

### 验收
- [x] dotnet build 通过
- [x] npm run build 通过

---

## 2026-06-19 — Phase 2 阅读模块

### 需求
实现短文阅读器、点击查词、LLM 重点词汇提取、段落评论、阅读日志与阅读辅助 Agent。

### 决策
- 短文逐词 React 渲染以支持 onClick 查词
- 查词优先读 ArticleVocabMappings 缓存
- 阅读辅助 Agent 通过 ReadingSkillRegistry + ReadingAssistantAgent 组合 skills
- LLM 调用统一扩展 ILLMProvider（ExtractVocab、ReplyToComment）

### 实现
- 新增 Article / ReadingLog / ArticleComment / ArticleVocabMapping 实体与迁移
- 内置 21 篇分级短文种子数据
- API：articles CRUD、vocab-extract、lookup、comments、reading-logs、reading/agent
- 前端：ArticleLibrary、ArticleReader、查词弹层、词汇面板、评论线程

### 验收
- [x] dotnet build 通过
- [x] npm run build 通过
- [x] 21 篇内置短文
- [x] 阅读主流程不依赖真实 LLM（Mock 降级）
