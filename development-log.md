# NextWord 开发日志

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
- [x] dotnet test 9/9 通过
- [x] dotnet build 通过
- [x] npm run build 通过

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
- [x] dotnet test 12/12 通过（单元 9 + 集成 3）
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
- [x] dotnet test 15/15 通过（单元 12 + 集成 3）
- [x] npm run build 通过
- [x] npm run test:e2e 2/2 通过
