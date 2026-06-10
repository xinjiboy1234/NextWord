# Phase 4: 完善与优化 — 生产就绪 + 缓存策略 + 监控

## 目标

将所有模块推向生产就绪状态：完善缓存策略、优化 LLM 调用成本、建立监控告警、补充测试覆盖、完成生产部署。

## 包含模块

- 缓存策略深化（分级结果、释义、词汇提取全面缓存）
- LLM 调用优化（基于 Microsoft.Extensions.AI 的批量处理、重试、降级、遥测）
- 模型配置档治理（多 Provider 切换、扩展参数白名单、效果/成本对比）
- 数据迁移方案（SQLite → PostgreSQL）
- 测试覆盖（单元测试 + 集成测试）
- 生产监控与日志
- Agent skills/plugins 治理（注册、权限、审计、成本统计）
- MAUI 桌面端适配准备

## 关键交付物

1. Redis 缓存层集成（生产环境）
2. MemoryCache 缓存层（开发环境）
3. LLM 调用批量处理（基于 Microsoft.Extensions.AI 的批量分级减少 API 调用）
4. ModelProfile 配置治理（多模型 API 切换、ProviderOptions 白名单、调用效果/成本统计）
5. 数据迁移脚本（SQLite dump → PostgreSQL import）
6. 单元测试覆盖 Domain 层核心逻辑（SM-2、等级引擎、测评评分）
7. 集成测试覆盖关键 API 端点
8. Agent skills/plugins 注册表、权限边界、审计日志和成本统计
9. 生产部署配置（Dockerfile + docker-compose）

## 技术层面需要创建的文件/模块

### 后端新增文件

```
Backend/
├── NextWord.Domain/
│   ├── Interfaces/
│   │   ├── ICacheService.cs              # 新增：缓存服务接口
│   │   ├── ISkillRegistry.cs             # 新增：Agent skills/plugins 注册表
│   │   ├── IAgentAuditService.cs         # 新增：Agent 调用审计
│   │   ├── IModelProfileStore.cs         # 新增：模型配置档读取与校验
│   │   └── IEmailService.cs             # 可选：通知服务
│   └── Services/
│       ├── CacheServiceBase.cs           # 新增：缓存基类
│       ├── LlmBatchProvider.cs           # 新增：批量 LLM 调用（优化）
│       ├── LlmRetryDecorator.cs          # 新增：LLM 调用重试装饰器
│       ├── LlmTelemetryDecorator.cs      # 新增：LLM 调用指标与追踪
│       ├── ModelProfileValidator.cs      # 新增：模型配置与扩展参数白名单校验
│       └── SkillRegistry.cs              # 新增：skills/plugins 元数据与权限注册
├── NextWord.Infrastructure/
│   ├── Caching/
│   │   ├── MemoryCacheService.cs         # 新增：开发环境缓存
│   │   └── RedisCacheService.cs          # 新增：生产环境缓存
│   ├── Migrations/
│   │   └── PostgresInitializer.cs        # 新增：PostgreSQL 初始化
│   └── DependencyInjection.cs            # 更新：按环境注册缓存
├── NextWord.IntegrationTests/           # 新增：集成测试项目
│   ├── TestFixture.cs
│   ├── AssessmentTests.cs
│   ├── Sm2Tests.cs
│   └── LevelEngineTests.cs
├── NextWord.UnitTests/                  # 新增：单元测试项目
│   ├── Sm2ServiceTests.cs
│   ├── LevelUpgradeEngineTests.cs
│   ├── ChallengePackGeneratorTests.cs
│   └── AssessmentScoringServiceTests.cs
├── NextWord.Api/
│   ├── Dockerfile                       # 新增：Docker 构建
│   ├── docker-compose.yml               # 新增：含 Redis + PostgreSQL
│   └── HealthChecks/
│       ├── LlmHealthCheck.cs            # 新增：LLM 可用性检查
│       └── DbHealthCheck.cs             # 新增：数据库连接检查
└── NextWord.BackgroundWorkers/           # 新增：后台任务项目
    ├── ReviewReminderWorker.cs           # 新增：复习提醒定时任务
    ├── LevelCheckWorker.cs               # 新增：等级检查定时任务
    └── CleanupWorker.cs                  # 可选：旧数据清理
```

### 前端新增文件

```
Frontend/
├── src/
│   ├── components/
│   │   ├── ErrorBoundary.tsx             # 新增：全局错误边界
│   │   ├── LoadingSkeleton.tsx           # 新增：加载骨架屏
│   │   └── OfflineBanner.tsx             # 新增：离线提示
│   ├── hooks/
│   │   ├── useOfflineSync.ts             # 新增：离线数据同步
│   │   └── useDebounce.ts                # 新增：防抖 Hook
│   └── utils/
│       └── performance.ts                # 新增：性能监控
```

## 缓存策略设计

### 缓存分层架构

```
┌──────────────────────────────────────────┐
│           应用层 (NextWord.Api)           │
├──────────────────────────────────────────┤
│         缓存层 (ICacheService)            │
│    ┌─────────────┬──────────────────┐    │
│    │ MemoryCache  │    Redis        │    │
│    │ (开发环境)   │  (生产环境)      │    │
│    └─────────────┴──────────────────┘    │
├──────────────────────────────────────────┤
│           持久层 (数据库)                  │
└──────────────────────────────────────────┘
```

### 缓存键命名规则

```
llm:annotation:{item_type}:{sha256_hash}     → 分级结果（24h/7d）
llm:definition:{word_id}:{context_hash}       → 释义（7d）
llm:extraction:{article_id}                   → 词汇提取（30d）
review:queue:{user_id}:{date}                 → 复习队列（当日有效）
level:candidate:{user_id}                     → 升级候选（1h）
```

### 缓存命中策略

| 数据 | 缓存键 | 过期时间(开发) | 过期时间(生产) | 说明 |
|------|--------|---------------|---------------|------|
| 词汇分级 | llm:annotation:word:{hash} | 24h | 7d | 同一词不会反复分级 |
| 句子分级 | llm:annotation:sentence:{hash} | 24h | 7d | 同内容重用 |
| 文章分级 | llm:annotation:article:{id} | 永久 | 永久 | 文章一旦分级不变 |
| 单词释义 | llm:definition:{word_id}:{ctx} | 7d | 30d | 上下文相关的释义 |
| 词汇提取 | llm:extraction:{article_id} | 30d | 90d | 提取结果长期有效 |
| 拼写评分 | llm:sentence-rating:{log_id} | 永久 | 永久 | 已记录的不重复调用 |

### LLM 调用优化

1. **统一调用入口**：所有真实模型调用都经过 `ILLMProvider`，底层使用 Microsoft.Extensions.AI 的 `IChatClient`，避免业务代码直接绑定厂商 SDK
2. **模型配置档切换**：按场景选择 `ModelProfile`，支持 OpenAI、Azure OpenAI、Ollama、本地模型或未来 Anthropic Adapter。
3. **ProviderOptions 白名单**：每个 Provider Adapter 定义允许的扩展参数、类型、默认值和范围，拒绝未知参数。
4. **批量分级**：导入自定义词表时，一次性传入 20-50 个词，用单个 LLM 调用返回批量分级结果
5. **重试装饰器**：LLM 调用失败时自动重试 3 次（指数退避：1s, 2s, 4s）
6. **超时保护**：单次 LLM 调用超时设为 30 秒，超时后返回降级评分
7. **熔断器**：连续 5 次 LLM 调用失败后，熔断 5 分钟，期间所有 LLM 请求返回默认值
8. **遥测与成本统计**：记录 ModelProfileId、Provider、模型名、扩展参数摘要、调用耗时、Token 用量、缓存命中率和失败原因，优先复用 Microsoft.Extensions.AI/OpenTelemetry 管道能力
9. **Agent Framework 预留边界**：生产优化阶段仍不默认引入 Microsoft Agent Framework；只有当学习规划、阅读评论、挑战测评演进为多 Agent 协作或长运行工作流时，再作为独立技术决策评估

### 模型配置档治理

```
ModelProfileStore:
  - 从 appsettings / 环境变量 / 密钥管理中读取配置
  - 合并全局默认值、场景默认值、Provider 默认值
  - 校验 ProviderOptions 白名单
  - 输出可供 ILLMProvider 使用的 LlmRequestOptions
```

治理规则：
- 生产环境只允许启用通过校验的模型配置档。
- `ProviderOptions` 不能由前端直接覆盖，只能通过后端配置或受控实验开关调整。
- 敏感配置只保存引用名，例如 `ApiKeyName`，真实密钥由配置系统或密钥管理提供。
- 每个模型配置档需要标记用途、成本等级、是否允许 tool calling、是否允许流式输出、是否允许结构化输出。
- 支持灰度切换：同一用途可配置主模型和备用模型，失败或成本超限时自动降级。

### Agent Skills / Plugins 治理

Phase 4 补齐 Agent 能力的生产治理：

1. **注册表**：所有 skills/plugins 必须声明名称、描述、输入/输出 schema、读写权限、可用模块、成本等级。
2. **权限边界**：默认只读；涉及写入生词本、评论回复、学习建议保存等操作必须走受控服务，并记录用户确认状态。
3. **审计日志**：记录 Agent 请求、调用工具、调用顺序、耗时、失败原因、Token 用量和最终结构化输出摘要。
4. **降级策略**：Agent 不可用时，阅读、查词、测评、复习等主流程必须继续可用。
5. **测试替身**：为每个 skill/plugin 提供 Mock 实现，集成测试覆盖 Agent 选择工具、工具失败、权限拒绝和降级路径。
6. **框架升级点**：如果单一 Agent + 本地 skills 注册表无法覆盖长流程、多 Agent、人机协同需求，再引入 Microsoft Agent Framework 的 Workflow 能力。

## 数据迁移方案

### SQLite → PostgreSQL

```
迁移步骤：
1. 安装 Npgsql.EntityFrameworkCore.PostgreSQL
2. 修改 appsettings.Production.json 连接字符串
3. 执行 ef migrations add ToPostgres
4. 手动修改迁移脚本中的 SQLite 特有语法
5. 在 Docker compose 中启动 PostgreSQL 服务
6. 执行 ef database update
7. 导入初始数据（内置短题库、核心词表）

数据导入脚本：
- ImportCoreWords.cs: 导入 5000 个核心词汇及预分级
- ImportArticles.cs: 导入 30 篇内置短文
- ImportDefaultAnnotations.cs: 导入权威词库的分级数据
```

## 测试策略

### 单元测试覆盖

| 测试类 | 测试内容 | 覆盖率目标 |
|--------|----------|-----------|
| Sm2ServiceTests | SM-2 计算、间隔更新、EaseFactor 边界 | 100% |
| LevelUpgradeEngineTests | 升级条件判断、降级回退、确认挑战 | 100% |
| ChallengePackGeneratorTests | 挑战包生成逻辑、难度递增 | 100% |
| AssessmentScoringServiceTests | 5步评分聚合、定级算法 | 100% |
| LlmMockProviderTests | Mock 分级返回、边界值 | 100% |

### 集成测试覆盖

| 测试类 | 测试端点 | 说明 |
|--------|----------|------|
| AssessmentTests | POST /api/assessment/step{1-5} | 完整测评流程 |
| WordTests | POST /api/words/learn | 翻译识别 + 日志写入 |
| SentenceTests | POST /api/sentences/score | 造句评分 |
| ArticleTests | POST /api/articles/{id}/vocab-extract | 词汇提取 |

## 后台任务

### ReviewReminderWorker

```
执行频率：每 6 小时
职责：
1. 查询 NextReviewDue <= 当前时间的待复习记录
2. 按用户分组，生成"今日复习"列表
3. 写入 UserProgress 的 pending_review_count
4. 前端首页读取 pending_review_count 展示
```

### LevelCheckWorker

```
执行频率：每天凌晨 2:00
职责：
1. 查询连续学习天数 >= 3 天的用户
2. 检查各维度指标是否满足升级条件
3. 标记升级候选用户
4. 触发确认挑战（自动发送通知）
```

## Phase 4 技术决策理由

1. **Redis 用于生产缓存**：多实例部署时需要共享缓存，MemoryCache 不行
2. **MemoryCache 用于开发环境**：零配置，不需要额外启动 Redis
3. **ICacheService 统一接口**：开发和生产通过 DI 切换实现，应用代码无感知
4. **Microsoft.Extensions.AI 统一 LLM 管道**：使用官方 .NET AI 抽象承载 Provider 切换、缓存、遥测和测试替身，避免在生产阶段被单一模型 SDK 绑定
5. **ModelProfile + ProviderOptions 控制模型差异**：通用参数强类型化，Provider 特有参数通过白名单扩展字段进入 Adapter，既灵活又可治理
6. **批量 LLM 调用**：导入词表时逐词调用成本极高，批量调用可以 10 倍减少 API 成本
7. **重试 + 超时 + 熔断三级防护**：LLM 服务不可用时不能阻断整个应用，三级防护确保可用性
8. **后台任务独立项目**：BackgroundWorkers 独立出来，便于后续扩展为消息队列（如 Hangfire/RabbitMQ）
9. **Agent skills/plugins 需要生产治理**：动态工具组合提升灵活性，但必须通过注册、权限、审计、降级和测试替身控制风险
10. **Docker Compose 一键启动**：开发环境一条命令启动 API + PostgreSQL + Redis，降低环境配置成本
