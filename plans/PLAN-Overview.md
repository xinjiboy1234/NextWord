# NextWord 英语学习应用 — 分阶段落地计划总览

## 项目概要

| 项目 | 详情 |
|------|------|
| 名称 | NextWord |
| 后端 | ASP.NET Core Web API (.NET 8) |
| 前端 | React (Vite + TypeScript + Tailwind CSS) |
| 数据库 | SQLite (开发/本地) / PostgreSQL (生产) |
| LLM Provider | 基于 Microsoft.Extensions.AI 的统一抽象层，运行时可插拔（预留 OpenAI/Anthropic/本地模型） |
| 核心算法 | SM-2 间隔重复算法 |
| 分级体系 | basic/intermediate/advanced × CEFR A1-C2 |

## 五大核心模块

| 编号 | 模块 | Phase |
|------|------|-------|
| M1 | 背单词模块（翻译识别 + 拼写） | Phase 0, 1 |
| M2 | 造句模块（指定词 + 自由表达） | Phase 1 |
| M3 | 阅读模块（短文阅读器 + 查词 + 评论） | Phase 2 |
| M4 | 测评系统（初测 + 挑战 + 等级升降） | Phase 3 |
| M5 | LLM分级系统（四级分级 + 缓存） | Phase 0, 1 |

## Phase 路线图

```
Phase 0 (Week 1-2)  ──┐
                      ├── MVP: 背单词 + LLM分级基础
Phase 1 (Week 3-4)  ──┘
                      ├── 造句模块 + LLM评分
Phase 2 (Week 5-6)  ──┐
                      ├── 阅读模块 + 词汇提取
Phase 3 (Week 7-8)  ──┘
                      ├── 测评系统 + 等级体系
Phase 4 (Week 9-10) ──┘
                      ├── 优化、缓存、迁移、监控
```

## 总交付时间线

| Phase | 周数 | 核心交付 |
|-------|------|----------|
| 0 | 1-2 | 项目骨架、数据库、背单词MVP、LLM分级接口 |
| 1 | 3-4 | 造句模块、LLM评分、学习日志、SM-2集成 |
| 2 | 5-6 | 阅读模块、短文本阅读器、重点词汇提取、评论 |
| 3 | 7-8 | 测评系统（初测+挑战）、等级升降、等级确认 |
| 4 | 9-10 | 生产部署、缓存优化、监控告警、测试覆盖 |

## 技术栈决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 后端框架 | ASP.NET Core Web API (.NET 8) | 成熟、跨平台、自带EF Core |
| ORM | EF Core (Code First) | 数据库无关，支持SQLite/PostgreSQL切换 |
| 前端构建 | Vite + React + TypeScript | 快速开发、类型安全 |
| 样式方案 | Tailwind CSS | 原子化CSS，快速原型 |
| 数据库 | SQLite (开发) / PostgreSQL (生产) | 轻量开发，生产级可靠 |
| LLM 调度与抽象 | Microsoft.Extensions.AI + 应用层 ILLMProvider 门面 + 模型配置档 | 复用 .NET 官方 AI 抽象、DI、中间件、缓存和遥测能力，同时支持运行时切换模型 API 与 Provider 扩展参数 |
| 缓存 | Redis (生产) / MemoryCache (开发) | 分级结果复用，减轻LLM调用 |
| 间隔重复 | SM-2 自实现 | 需求明确，无需第三方库 |

### AI 调度库选型

当前阶段采用 **Microsoft.Extensions.AI** 作为 LLM 调度与 Provider 抽象基础，而不是直接引入 Microsoft Agent Framework。

选择理由：
- **更贴合当前需求**：NextWord 的 LLM 使用主要是词汇分级、释义生成、造句评分、阅读词汇提取等明确的单次或批量调用，不需要多 Agent 自主规划。
- **更轻量易用**：Microsoft.Extensions.AI 提供 `IChatClient`、结构化响应、流式响应、DI、中间件、缓存、OpenTelemetry 等能力，适合嵌入 ASP.NET Core 服务层。
- **Provider 切换成本低**：业务层保留 `ILLMProvider`/`ILlmService` 门面，底层通过 Microsoft.Extensions.AI 接入 OpenAI、Azure OpenAI、Ollama 或其他兼容 Provider。
- **模型 API 可切换**：通过模型配置档选择 Provider、模型名、Endpoint、API Key 引用、默认温度、最大输出长度、超时等通用参数。
- **Provider 特定参数可扩展**：对 OpenAI、Azure OpenAI、Ollama、Anthropic 等不同 API 的专属参数，统一放入 `ProviderOptions` 扩展字典，并由对应 Provider Adapter 校验和转换。
- **测试和降级更简单**：可以用 Mock `IChatClient`、装饰器、缓存和熔断逻辑覆盖大部分 MVP 到生产优化需求。

Microsoft Agent Framework 暂作为后续候选：当系统出现多 Agent 协作、长时间运行工作流、人机协同审批、复杂工具调用编排时，再在独立模块中评估引入，避免 MVP 阶段过度复杂化。

### 模型切换与扩展参数原则

不同 LLM API 的参数能力不同，例如某些模型支持 JSON schema、tool choice、reasoning effort、seed、top-p、presence penalty、response format、thinking budget 或本地模型特有参数。系统需要支持这些差异，但不能把业务代码写成大量 Provider `switch case`。

设计原则：
- **模型配置档**：用 `ModelProfile` 描述一个可选模型后端，例如 `default-fast`、`grading-stable`、`reading-agent`、`local-dev`。
- **通用参数优先**：温度、最大输出长度、超时、流式输出、结构化输出、工具调用等跨 Provider 常见能力使用强类型字段。
- **扩展参数隔离**：Provider 特有值放入 `ProviderOptions: Dictionary<string, JsonElement>` 或等价结构，由 Provider Adapter 负责白名单校验、类型转换和默认值合并。
- **按场景选择模型**：分级、造句评分、阅读 Agent、批量导入、评论回复可以使用不同 `ModelProfile`，避免一个模型配置覆盖所有场景。
- **安全配置来源**：API Key、Endpoint、部署名等敏感值只从配置或密钥管理读取，不能由前端请求直接传入。
- **可观测性**：每次调用记录 `ModelProfileId`、Provider、模型名、扩展参数摘要、耗时、Token 用量和失败原因，便于比较不同模型效果和成本。

示例配置结构：

```json
{
  "ModelProfiles": {
    "grading-stable": {
      "Provider": "OpenAI",
      "Model": "gpt-4.1-mini",
      "Temperature": 0.1,
      "MaxOutputTokens": 800,
      "TimeoutSeconds": 30,
      "ProviderOptions": {
        "response_format": "json_schema",
        "seed": 42
      }
    },
    "local-dev": {
      "Provider": "Ollama",
      "Model": "qwen2.5:7b",
      "Temperature": 0.2,
      "ProviderOptions": {
        "num_ctx": 8192
      }
    }
  }
}
```

### Agent / Skills / Plugins 能力原则

NextWord 需要保留一定的 Agent 能力，但不让 Agent 接管核心学习规则。总体原则是：

> 确定性规则负责学习结果与边界，Agent 负责开放式辅导、工具组合和个性化建议。

适合 Agent 化的场景：
- **阅读辅助**：动态组合查词、上下文释义、重点词提取、例句生成、评论回复、加入生词本建议。
- **造句辅导**：动态组合评分、改写、错误归因、补充练习、目标词推荐。
- **词表导入处理**：动态组合去重、词频查询、CEFR 分级、LLM 分级、学习批次拆分。
- **个性化学习建议**：根据用户历史表现，在复习、新词、造句、阅读之间给出建议。

不适合交给 Agent 决定的场景：
- SM-2 复习间隔、NextReviewDue、EaseFactor 更新。
- 初测/挑战测评的最终定级、升级、回退判定。
- 正式测评题目的通过标准和安全边界。

落地顺序：
1. **Phase 0-1**：保留 `ILLMProvider` 与 tool/function calling 扩展点，不实现完整 Agent。
2. **Phase 2**：优先实现阅读辅助 Agent，作为第一批 skills/plugins 组合能力。
3. **Phase 3**：测评模块只允许 Agent 辅助解释和候选题建议，最终规则由确定性服务执行。
4. **Phase 4**：补充 skills/plugins 注册、权限、审计、成本统计和降级治理；如出现长流程或多 Agent 协作，再评估 Microsoft Agent Framework。

## 数据库核心实体关系

```
User ──< UserProgress ──< WordLearningLog
                   ──< SpellingLog
                   ──< SentenceLog
                   ──< ReadingLog
                   ──< AssessmentRecord
                   ──< LevelHistory

Word ──< WordLearningLog
      ──< SpellingLog
      ──< WordDifficultyAnnotation  (LLM分级)
      ──< UserWordRelationship

Sentence ──< SentenceLog
        ──< SentenceDifficultyAnnotation (LLM分级)

Article ──< ReadingLog
       ──< ArticleComment
       ──< ArticleDifficultyAnnotation (LLM分级)
       ──< ArticleVocabMapping

Assessment ──< AssessmentRecord
          ──< ChallengeRecord

ReviewQueue ── (SM-2计算出的待复习队列，按DueDate查询)
```

关键关系说明：
- **User** 是核心，关联所有进度和日志
- **Word/Sentence/Article** 各自有 DifficultyAnnotation 表存储 LLM 分级结果
- **WordLearningLog / SpellingLog / SentenceLog / ReadingLog / AssessmentRecord** 分别记录各模块的学习行为
- **UserProgress** 聚合用户当前等级、各子等级、连续学习天数等
- **ReviewQueue** 是派生数据，通过 SM-2 算法实时计算，不需要物理持久化（但复习结果写入日志表）
