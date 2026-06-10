# Phase 0: MVP — 背单词 + LLM分级基础

## 目标

搭建项目骨架、数据库、核心实体模型，实现背单词模块的最简可用版本（翻译识别模式），并建立 LLM 分级抽象层。

**MVP 范围裁剪：**
- 只实现背单词模块的**翻译识别模式(A)**，拼写模式(B)延后
- LLM分级接口抽象完成，但使用**模拟 Provider（返回硬编码分级）**作为占位
- 测评系统和阅读模块不在本 Phase

## 包含模块

- 项目初始化与架构搭建
- 数据库设计与迁移
- 背单词模块（翻译识别模式）
- 基于 Microsoft.Extensions.AI 的 LLM 分级抽象接口（模拟实现）
- 基础学习日志

## 关键交付物

1. 可运行的 ASP.NET Core Web API + React 前后端项目
2. 数据库迁移脚本（Code First 自动生成）
3. 背单词翻译识别模式端到端可用
4. Microsoft.Extensions.AI 接入骨架 + LLM Provider 抽象层 + 模拟实现
5. 学习日志记录（熟练度、反应时间、错误次数）

## 技术层面需要创建的文件/模块

### 后端 (ASP.NET Core Web API)

```
Backend/
├── NextWord.Api/
│   ├── Program.cs                    # 入口、依赖注入配置
│   ├── appsettings.json              # 基础配置
│   ├── appsettings.Development.json  # 开发配置（SQLite）
│   ├── appsettings.Production.json   # 生产配置（PostgreSQL）
│   └── Properties/
│       └── launchSettings.json
├── NextWord.Domain/
│   ├── Entities/
│   │   ├── User.cs                   # 用户实体
│   │   ├── Word.cs                   # 词汇实体
│   │   ├── WordLearningLog.cs        # 翻译识别学习日志
│   │   ├── UserProgress.cs           # 用户进度（等级、连续天数）
│   │   ├── DifficultyAnnotation.cs   # LLM分级结果
│   │   └── UserWordRelationship.cs   # 用户-词关系（收藏、掌握度）
│   ├── Enums/
│   │   ├── DifficultyLevel.cs        # basic/intermediate/advanced
│   │   ├── CefrLevel.cs              # A1-C2
│   │   ├── AssessmentResult.cs       # 记录/模糊/不会
│   │   └── RecommendedAction.cs      # learn_now/review_later/challenge_only
│   ├── Interfaces/
│   │   ├── ILLMProvider.cs           # 应用层 LLM 服务门面
│   │   ├── IModelProfileResolver.cs  # 模型配置档解析
│   │   ├── ISm2Service.cs            # SM-2算法接口
│   │   ├── IWordRepository.cs        # 仓储接口
│   │   ├── IUserRepository.cs        # 仓储接口
│   │   └── IReviewQueueService.cs    # 复习队列服务
│   └── Services/
│       ├── Sm2Service.cs             # SM-2算法实现
│       ├── LlmMockProvider.cs        # 模拟LLM Provider
│       ├── ModelProfileResolver.cs   # 模型配置档选择与默认值合并
│       ├── LlmPromptFactory.cs       # LLM 提示词与结构化请求构建
│       ├── LlmResponseParser.cs      # LLM 结构化响应解析与校验
│       └── ReviewQueueService.cs     # 复习队列计算
├── NextWord.Infrastructure/
│   ├── Data/
│   │   ├── ApplicationDbContext.cs   # EF Core DbContext
│   │   └── Migrations/               # Code First 迁移文件
│   ├── Repositories/
│   │   ├── WordRepository.cs
│   │   └── UserRepository.cs
│   └── DependencyInjection.cs        # 基础设施层 DI 注册
└── NextWord.Api.Endpoints/
    ├── WordEndpoints.cs              # 词汇 CRUD / 每日新词
    ├── LearningEndpoints.cs          # 翻译识别交互 / 日志写入
    ├── ProgressEndpoints.cs          # 用户进度查询
    └── LlmEndpoints.cs               # 分级请求入口（含缓存判断）
```

### 前端 (React + Vite + TypeScript + Tailwind)

```
Frontend/
├── src/
│   ├── main.tsx                      # 入口
│   ├── App.tsx                       # 路由 + 布局
│   ├── api/
│   │   ├── client.ts                 # Axios 实例 + 拦截器
│   │   └── endpoints.ts              # API 路径常量
│   ├── pages/
│   │   ├── Home.tsx                  # 主页（今日新词/复习/收藏入口）
│   │   ├── WordCard.tsx              # 背单词卡片页面
│   │   └── Progress.tsx              # 进度展示页
│   ├── components/
│   │   ├── WordDisplay.tsx           # 单词显示组件
│   │   ├── AnswerInput.tsx           # 中文答案输入
│   │   ├── FeedbackArea.tsx          # 反馈区（正确/错误/释义/用法）
│   │   ├── RatingButtons.tsx         # 三按钮（记住/模糊/不会）
│   │   └── ProgressBar.tsx           # 学习进度条
│   ├── types/
│   │   ├── models.ts                 # TypeScript 类型定义
│   │   └── llm.ts                    # LLM 分级响应类型
│   └── hooks/
│       ├── useWordSession.ts         # 单词会话管理
│       └── useLearningLog.ts         # 学习日志提交
```

## 本 Phase 数据库 Schema 设计

### 核心表

| 表名 | 说明 | 关键字段 |
|------|------|----------|
| Users | 用户 | Id, DisplayName, CreatedAt |
| WordDifficultyAnnotations | LLM词汇分级 | Id, WordId, DifficultyLevel, CefrLevel, Reason, RecommendedAction, Confidence, CreatedAt |
| Words | 词汇库 | Id, Lemma, PartOfSpeech, Phonetics, Meanings, ExampleSentences, DifficultyLevel, CefrLevel, LlmAnnotationId, IsCore |
| UserProgress | 用户进度 | Id, UserId, OverallLevel, VocabLevel, SpellingLevel, SentenceLevel, ReadingLevel, StreakDays, LastStudyDate |
| UserWordRelationships | 用户与词的关系 | Id, UserId, WordId, MasteryScore, TimesLearned, TimesCorrect, LastReviewDate, NextReviewDue, Source (new/review/challenge) |
| WordLearningLogs | 翻译识别日志 | Id, UserId, WordId, Answer, IsCorrect, Rating(记住/模糊/不会), ResponseTimeMs, Timestamp |
| DifficultyAnnotations | 通用LLM分级(句子/文章) | Id, ItemType, ItemHash, DifficultyLevel, CefrLevel, Reason, RecommendedAction, Confidence, CreatedAt |

### 实体关系概要

```
Users (1) ──< (N) UserProgress
Users (1) ──< (N) UserWordRelationships ──> (N) Words
Users (1) ──< (N) WordLearningLogs ──> (N) Words
Words (1) ── (0..1) WordDifficultyAnnotations
```

## SM-2 算法实现要点

本 Phase 实现 SM-2 核心逻辑，为后续背单词模块提供复习调度。

### 算法核心变量

| 变量 | 说明 |
|------|------|
| Interval | 距上次复习的间隔天数 |
| EaseFactor | 难度系数，初始 2.5，回答正确会增加，错误会重置为 1.3 |
| RepeatCount | 连续答对的次数 |
| NextDueDate | 下次复习日期 |

### SM-2 计算流程

```
对于每个用户-词关系：
  如果当前日期 >= NextReviewDue:
    从复习队列中取出
    用户评分 -> 0(不会) / 1(模糊) / 2(记住)
    
    IF 评分 == 0:
      RepeatCount = 0
      Interval = 1天
      EaseFactor = max(1.3, EaseFactor - 0.2)
    ELSE IF 评分 == 1:
      Interval = 1天 (降级为当天复习)
      EaseFactor = EaseFactor (不变)
    ELSE (评分 == 2):
      IF RepeatCount == 0: Interval = 1天
      ELSE IF RepeatCount == 1: Interval = 6天
      ELSE: Interval = Interval * EaseFactor (取整)
      EaseFactor = EaseFactor + 0.15
      RepeatCount = RepeatCount + 1
    
    更新 NextReviewDue = 当前日期 + Interval
    更新 EaseFactor, RepeatCount
```

### 关键设计决策

1. **复习队列不持久化**：通过 `WHERE NextReviewDue <= GETDATE()` 实时查询，避免同步开销
2. **Interval 上限**：限制为 3650 天（10年），避免无限增长
3. **EaseFactor 下限**：1.3，确保最难的词至少每天复习一次
4. **新词首次复习间隔为 1 天**：新词进入系统后第二天首次出现
5. **与学习日志解耦**：SM-2 计算的是"何时出现"，日志表记录的是"如何回答"

## LLM 服务接口抽象设计

### AI 调度库选择

本项目在 MVP 阶段选择 **Microsoft.Extensions.AI** 作为 LLM 调度基础库，业务层继续保留 `ILLMProvider` 门面接口。

架构原则：
- `ILLMProvider` 面向业务用例，暴露分级、释义、造句评分等稳定方法。
- 底层真实模型调用通过 Microsoft.Extensions.AI 的 `IChatClient` 完成，便于统一接入 OpenAI、Azure OpenAI、Ollama 或其他 Provider。
- MockProvider 不依赖真实模型，保证 Phase 0 可以端到端运行；Phase 1 再替换为真实 `IChatClient` 实现。
- LLM 请求携带 `ModelProfileId`，由 `IModelProfileResolver` 解析 Provider、模型名、通用参数和 Provider 特有扩展参数。
- 缓存、重试、超时、日志、OpenTelemetry 等横切能力优先用 Microsoft.Extensions.AI 和 ASP.NET Core DI 装饰器组合实现。

Phase 0 不实现完整 Agent，但需要预留 skills/plugins 扩展边界：
- 当前 MVP 的 LLM 任务边界清晰，属于“服务调用 + 结构化输出”，不需要 Agent 自主规划或多 Agent 协作。
- `ILLMProvider` 的实现应避免写死具体模型 SDK，为后续 tool/function calling、skills/plugins 注册表留出扩展点。
- Agent 只适合开放式辅导和工具组合，不接管 SM-2、测评定级、升级判定等确定性规则。
- 首个 Agent 化落点放到 Phase 2 阅读辅助；Microsoft Agent Framework 暂作为长流程、多 Agent、人机协同场景的后续候选。

### ILLMProvider 接口定义

```
接口: ILLMProvider

方法:
  Task<DifficultyRating> RateDifficultyAsync(
    ItemRatingRequest request,
    CancellationToken ct
  )
  // 返回词/句/文章的难度分级

  Task<DefinitionResponse> GetDefinitionAsync(
    DefinitionRequest request,
    CancellationToken ct
  )
  // 返回单词释义、文中含义、搭配、例句

  Task<SentenceRatingResponse> RateSentenceAsync(
    SentenceRatingRequest request,
    CancellationToken ct
  )
  // 返回句子语法/自然度/词汇评分及修改建议
```

### 模型配置档与扩展参数

Phase 0 先定义模型配置结构，不接入真实模型：

```
ModelProfile:
  Id: string                         // grading-stable / reading-agent / local-dev
  Provider: string                   // OpenAI / AzureOpenAI / Ollama / Anthropic / Mock
  Model: string                      // 模型名或部署名
  Endpoint: string                   // 可选，真实值从配置读取
  ApiKeyName: string                 // 密钥引用名，不直接保存密钥
  Temperature: float?
  MaxOutputTokens: int?
  TimeoutSeconds: int?
  EnableToolCalling: bool
  EnableStructuredOutput: bool
  ProviderOptions: object            // Provider 特有扩展参数

LlmRequestOptions:
  ModelProfileId: string
  Purpose: difficulty_rating | definition | sentence_rating | reading_agent
  OverrideCommonOptions: object      // 仅允许后端服务传入
  ProviderOptionsOverride: object    // 仅允许白名单字段
```

约束：
- 业务服务只选择 `ModelProfileId`，不直接拼 Provider 参数。
- 通用参数用强类型字段表达，Provider 特有参数进入 `ProviderOptions`。
- 每个 Provider Adapter 必须维护允许的扩展参数白名单，拒绝未知或类型错误的字段。
- 前端请求不能直接传 `ProviderOptions`，避免用户绕过成本、模型和安全限制。
- MockProvider 忽略真实 API 参数，但要记录收到的 `ModelProfileId`，方便后续测试切换逻辑。

### 请求/响应模型

```
DifficultyRating:
  ItemType: word | sentence | article
  DifficultyLevel: basic | intermediate | advanced
  CefrLevel: A1 | A2 | B1 | B2 | C1 | C2
  Reason: string
  RecommendedAction: learn_now | review_later | challenge_only
  Confidence: float (0.0 - 1.0)

DefinitionResponse:
  Word: string
  Phonetics: string
  Meanings: Meaning[]        // 包含文中含义
  Collocations: string[]
  ExampleSentences: string[]
  SpecialUsage: string
  DifficultyLevel: basic | intermediate | advanced
  CefrLevel: A1 | A2 | B1 | B2 | C1 | C2

Meaning:
  Definition: string
  IsContextual: bool         // 是否是文中的特殊含义
  Context: string
```

### 缓存策略（本 Phase 基础版本）

- 缓存键：`llm:{item_type}:{sha256_hash}`
- 过期时间：24 小时（开发）/ 7 天（生产）
- 缓存前置判断：分级请求先查缓存，命中则直接返回
- 未命中则调用 ILLMProvider，写入缓存 + 持久化到数据库

### MockProvider 实现

- 返回硬编码的分级数据
- 按词频字典预定义 100 个常见词的分级
- 超出的词返回 basic 默认值
- 用于 MVP 端到端验证，后续替换为真实 Provider

## Phase 0 技术决策理由

1. **Code First EF Core**：需求文档没有指定数据库 schema，Code First 允许先设计领域模型，快速迭代
2. **SQLite 开发 / PostgreSQL 生产**：零配置开发体验，生产环境切换只需改连接字符串和 NuGet 包
3. **Microsoft.Extensions.AI + 应用层门面做 LLM 抽象**：底层复用 .NET 官方 `IChatClient`、DI、中间件、缓存和遥测能力；业务层通过 `ILLMProvider` 保持分级、释义、评分接口稳定，让后续切换 OpenAI/Azure OpenAI/Ollama/本地模型的成本保持可控
4. **模型配置档 + ProviderOptions 承载模型差异**：通用参数强类型化，Provider 特有参数通过白名单扩展字段控制，避免业务层出现大量 Provider 分支
5. **MVP 只保留翻译识别模式**：拼写模式涉及音频播放和差异比对，复杂度更高，可作为 Phase 1 的增量
6. **SM-2 自实现而非第三方库**：需求算法明确（SM-2），自实现便于与用户评分（记住/模糊/不会）定制对齐
7. **Repository 模式**：为未来单元测试隔离 EF Core 依赖，也方便后续替换存储后端
8. **MAUI 桌面端预留**：后端设计为纯 API，无 UI 绑定，后续 MAUI 桌面端直接复用
