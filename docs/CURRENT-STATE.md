# NextWord 当前功能文档

> 文档版本：2026-06-23  
> 用途：记录已实现功能、架构与业务规则，供后续迭代参考。  
> 相关计划文档见 [`plans/PLAN-Overview.md`](../plans/PLAN-Overview.md)。

---

## 1. 项目概要

| 项目 | 说明 |
|------|------|
| 名称 | NextWord — 英语学习应用 |
| 后端 | ASP.NET Core Minimal API（.NET 10） |
| 前端 | React 19 + Vite 8 + TypeScript + Tailwind CSS 4 |
| 数据库 | SQLite（本地开发）/ PostgreSQL（生产 Docker） |
| 缓存 | MemoryCache（开发）/ Redis（生产 Docker） |
| LLM | Microsoft.Extensions.AI + `ILLMProvider` 门面；默认 Mock，可切换 OpenAI |
| 核心算法 | SM-2 间隔重复（自实现） |
| 分级体系 | `DifficultyLevel`（Basic/Intermediate/Advanced）× CEFR（A1–C2） |

### 1.1 解决方案结构

```
Backend/
  NextWord.Api              # 宿主：Program.cs、HealthChecks、CORS、迁移与种子
  NextWord.Api.Endpoints    # Minimal API 路由与请求/响应 DTO
  NextWord.Domain           # 实体、枚举、接口、领域服务（SM-2、评分、Mock LLM）
  NextWord.Infrastructure   # EF Core、仓储、应用服务、后台 Worker、缓存
  NextWord.UnitTests        # 单元测试（12 用例）
  NextWord.IntegrationTests # 集成测试（3 用例，WebApplicationFactory）
Frontend/
  src/                      # React SPA（无路由库，view 状态切换）
  e2e/                      # Playwright E2E（2 用例）
docs/                       # 本文档
plans/                      # 分阶段计划与 next-steps
```

### 1.2 实现阶段对照

| Phase | 状态 | 核心交付 |
|-------|------|----------|
| 0 | ✅ | 项目骨架、背单词 MVP、LLM 分级接口 |
| 1 | ✅ | 造句、拼写、SM-2、学习日志 |
| 2 | ✅ | 阅读模块、词汇提取、评论、阅读 Agent |
| 3 | ✅ | 初测 + 挑战、等级升降 |
| 4 | ✅ | 缓存装饰、重试、Docker、HealthChecks、单元测试 |
| 5 | ✅ | 集成测试、引导横幅、后台 Worker |
| 6 | ✅ | Redis、LLM 遥测、Playwright E2E、升级候选横幅 |

---

## 2. 功能模块总览

应用包含 **五大学习模块** 与 **测评/等级体系**，前端通过顶部导航切换（无 URL 路由）。

| 导航 | 模块 | 后端服务 | 说明 |
|------|------|----------|------|
| 学习 | M1 背单词 | `UserRepository`, `Sm2Service` | 每日新词 + 主观评分（记得/模糊/忘了） |
| 拼写 | M1 拼写 | `SpellingService` | 中文释义 → 英文拼写，错误位置高亮 |
| 造句 | M2 造句 | `SentenceService`, `FreeExpressionService` | 指定词造句 + 自由表达 |
| 阅读 | M3 阅读 | `ArticleService`, `ArticleVocabService`, `CommentService` | 短文库、点击查词、词汇提取、评论 |
| 测评 | M4 初测 | `AssessmentService` | 5 步定级（词汇/拼写/造句/阅读/汇总） |
| 挑战 | M4 挑战 | `ChallengeService`, `ChallengePackGenerator` | 等级挑战与确认挑战 |
| 等级 | M4 等级 | `LevelDashboardService`, `LevelUpgradeEngine` | CEFR 看板与历史 |
| 复习 | SM-2 复习 | `ReviewQueueService`, `ReviewReminderWorker` | 待复习队列与活动摘要 |
| 词库 | 词库浏览 | `WordRepository` | 全量词条列表 |
| 进度 | 用户进度 | `UserRepository` | 等级、连续天数、准确率等 |
| 我的 | 个人主页 | `AuthService`, `ProfileEndpoints` | 登录、等级、进度、LLM 配置 |

### 2.1 全局引导

- **OnboardingBanner**：`hasCompletedInitialAssessment === false` 时提示完成初测；可关闭（`localStorage: nextword.onboarding.dismissed`）。
- **UpgradeCandidateBanner**：已完成初测且 `isUpgradeCandidate === true` 时提示查看等级页；可关闭（`nextword.upgrade.dismissed`）。

### 2.2 用户与认证

- **默认用户**（开发种子，无邮箱）：ID `11111111-1111-1111-1111-111111111111`，显示名 `MVP Learner`
- **注册用户**：邮箱 + 密码（PBKDF2-SHA256），JWT 有效期 7 天
- **用户解析顺序**：`Authorization: Bearer` JWT → 可选 `userId` 查询参数 → 默认种子用户
- 未登录时学习功能仍可用（绑定默认用户）；个人主页与 LLM 配置需登录

---

## 3. 模块详细说明

### 3.1 背单词（学习）

**用户流程**

1. 加载每日新词（默认 8 个，`GET /api/words/daily?count=8`）
2. 展示英文单词（词性、音标）；用户输入中文释义
3. 选择主观评分：Remembered / Fuzzy / Forgot
4. 提交 `POST /api/learning/submit` → 返回释义、掌握度、下次复习时间
5. 顺序学完队列后显示「今日新词完成」

**业务规则**

- 每日词从未学习或低掌握度的核心词中选取（`WordRepository.GetDailyWordsAsync`）
- 每次提交写入 `WordLearningLog`，更新 `UserWordRelationship`（SM-2）
- `IsCorrect` 由答案匹配逻辑判定；间隔由 **评分** 驱动，非仅对错

**前端**：`WordCard` + `useWordSession` + `useLearningLog`

---

### 3.2 拼写

**用户流程**

1. 加载拼写队列（默认 8 个，`GET /api/spelling/queue`）— 优先 `NextReviewDue <= now` 的复习词，否则回落到每日词
2. 展示中文释义 + TTS 播放（Web Speech API）
3. 用户输入拼写；可「再想想」增加尝试次数
4. 提交 `POST /api/spelling/submit` → 错误字符位置高亮
5. 「下一个」进入下一词

**业务规则**

- 拼写结果同样走 SM-2 更新 `UserWordRelationship`
- 记录 `SpellingLog`（含 `ErrorPositions[]`、`Attempts`）

**前端**：`SpellingMode` + `useSpellingSession`

---

### 3.3 造句

#### 3.3.1 指定词造句

**用户流程**

1. 加载造句提示（10 条，`GET /api/sentences/prompts`）
2. 展示目标词 + 例句场景；用户选择场景并写句子
3. `POST /api/sentences/rate` → LLM 评分（语法/自然度/词汇/相关性）
4. 展示 AI 改写、错误标签、建议；评分标签：稳定 / 可用 / 需打磨 / 需重写

**业务规则**

- 使用 Model Profile `grading-stable`
- 持久化 `SentenceLog`

#### 3.3.2 自由表达

**用户流程**

1. 用户写 2–5 句英文
2. `POST /api/free-expression/rate` → 综合评分与改写建议
3. 持久化 `FreeExpressionLog`

**前端**：`SentenceStudio`（Tab：指定词 `SentenceCard` / 自由表达 `FreeExpression`）

**注意**：前端硬编码 `userLevel: 'A2'` 传入评分请求。

---

### 3.4 阅读

**用户流程**

1. **文库**：`GET /api/articles`（可按 `level` / `cefr` 筛选），内置 **21 篇** 分级短文
2. **打开文章**：并行加载详情、评论、开始阅读会话 `POST .../reading/start`
3. **阅读器**：逐词可点击查词 → `POST .../lookup`；记录 lookup 次数
4. **词汇提取**：加载已有映射或 `POST .../vocab-extract`（LLM）
5. **评论**：按段落发帖，可选 AI 回复 `POST .../comments`
6. **完成**：`POST /api/reading-logs/{logId}/finish`（上报 lookup/comment 计数）

**种子数据分布**

| 难度 | 篇数 | CEFR 范围 |
|------|------|-----------|
| Basic | 10 | A1–A2 |
| Intermediate | 7 | B1–B2 |
| Advanced | 4 | C1–C2 |

**阅读辅助 Agent**

- 后端：`ReadingAssistantAgent` + `ReadingSkillRegistry`（组合查词、词汇提取、评论回复等 skills）
- API：`POST /api/reading/agent`
- **前端尚未接入**该端点

**前端**：`ArticleLibrary` → `ArticleReader` + `useArticleReader` / `useWordLookup` / `useVocabExtract`

---

### 3.5 测评（初测）

**5 步流程**（确定性编排，LLM 仅用于造句步骤评分）

| 步骤 | 类型 | GET 返回 | POST 提交 `answersJson` |
|------|------|----------|-------------------------|
| 1 | 词汇识别 | 选择题数组 | `int[]` 选项索引 |
| 2 | 拼写 | 中→英拼写题 | `string[]` |
| 3 | 造句 | 目标词 + 场景 | `string[]` 用户句子 |
| 4 | 阅读 | 短文 + 选择题 | `{ selectedIndex, lookupCount }` |
| 5 | 定级 | — | `POST .../complete` |

**API 序列**

```
POST /api/assessment/initial/start
GET  /api/assessment/{id}/step/{1-4}
POST /api/assessment/{id}/step/{1-4}
POST /api/assessment/{id}/complete  → FinalLevelResult
```

**评分与定级规则**（`AssessmentScoringService`）

| 维度 | 映射依据 | 阈值（≤ 则对应等级） |
|------|----------|----------------------|
| 词汇 | 正确率 % | 9→A1, 29→A2, 49→B1, 69→B2, 否则 C1 |
| 拼写 | 正确率 % | 0→A1, 19→A2, 39→B1, 59→B2, 否则 C1 |
| 造句 | 平均分 0–5 | 0.9→A1, 1.9→A2, 2.9→B1, 3.9→B2, 否则 C1 |
| 阅读 | 正确率 % + 查词密度 | 19/39/59/79；查词 > 15% 词数则降一级 |

**总等级（短板定级）**

```
overall = min(vocabLevel, sentenceLevel, readingLevel)
```

拼写等级单独记录，**不参与** overall 计算。

完成后更新 `UserProgress` 各维度 CEFR、`HasCompletedInitialAssessment = true`，写入 `LevelHistory`。

**前端**：`InitialAssessment` + `useAssessmentFlow`（步骤 1–4 交互完整；E2E 仅验证启动）

---

### 3.6 挑战

**类型**

- `Daily`：日常挑战
- `LevelConfirmation`：等级确认挑战（升级/锁定相关）

**流程**

1. `POST /api/challenge/start` → `ChallengePack`（词汇题集、造句题、阅读 MCQ、目标等级）
2. 用户完成并提交 `POST /api/challenge/submit`（词汇分、造句分、阅读分）
3. 服务端判定 `Passed`、更新等级或回退

**前端现状**

- `ChallengeMode` 会拉取 `ChallengePack`，但 **UI 为自评模式**（手动填词汇正确数、造句 0–5 分、阅读勾选），非完整交互答题
- `GET /api/challenge/recent` **未接入前端**

---

### 3.7 等级体系

**数据字段**（`UserProgress`）

| 字段 | 说明 |
|------|------|
| `OverallLevel` | 综合 CEFR（短板） |
| `VocabLevel` / `SpellingLevel` / `SentenceLevel` / `ReadingLevel` | 分维度等级 |
| `StreakDays` | 连续学习天数 |
| `HasCompletedInitialAssessment` | 是否完成初测 |
| `IsUpgradeCandidate` | 是否满足升级候选（Worker 写入） |
| `PendingReviewCount` | 待复习词数（Worker 写入） |
| `IsLevelLocked` | 确认挑战进行中 |

**升级候选规则**（`LevelUpgradeEngine`）

满足其一即标记候选：

- 连续学习 ≥ 3 天 **且** 当前等级停留 ≥ 3 天
- 近 7 天内有通过的挑战记录

**API**

- `GET /api/level/dashboard` — 各维度等级、候选标志、近期 `LevelHistory`
- `GET /api/level/history` — 完整历史（前端未单独调用，dashboard 已含 `recentHistory`）

---

### 3.8 复习与日志

**复习队列**

- `ReviewQueueService`：按 `NextReviewDue` 与 `EaseFactor` 排序
- `ReviewReminderWorker`（每 6h）：汇总每用户待复习数 → `PendingReviewCount`

**活动日志 API**

| 端点 | 说明 |
|------|------|
| `GET /api/logs/summary` | 造句/自由表达/拼写次数与拼写准确率 |
| `GET /api/logs/recent` | 近期 sentence / spelling 活动 |

**前端**：`ReviewQueue` 展示待复习词、摘要指标、近期动态

---

### 3.9 LLM 分级（M5）

**端点**：`POST /api/llm/rate-difficulty`

- 输入：文本 + `ItemType`（Word/Sentence/Article）+ 可选 `ModelProfileId`
- 输出：`DifficultyLevel`、`CefrLevel`、`Reason`、`RecommendedAction`、`Confidence`
- 缓存：`IMemoryCache`，键 `llm:{itemType}:{sha256(text)}`，TTL 24h

**前端**：端点已定义于 `endpoints.ts`，**UI 未使用**

**实体**

- `WordDifficultyAnnotation` — 词条级 LLM 注释
- `DifficultyAnnotation` — 通用 `(ItemType, ItemHash)` 注释

---

### 3.10 用户认证与个人主页

**登录 / 注册**

1. 前端「我的」页或内嵌 `LoginPage`：邮箱 + 密码
2. `POST /api/auth/register` 或 `POST /api/auth/login` → JWT + 用户信息
3. Token 存 `localStorage: nextword.auth.token`；Axios 自动附加 `Authorization` 头

**个人主页**（需登录）

1. `GET /api/profile` — 聚合进度、五维等级、等级历史、LLM 设置（Key 掩码）
2. `PUT /api/profile/llm` — 保存 LLM 配置；支持预设 `openai` / `deepseek` / `qwen`
3. 展示：昵称、邮箱、总体等级、已学词/待复习/正确率、连续天数、等级历史

**个人 LLM 配置**

| 预设 | Provider | BaseUrl | 默认 Model |
|------|----------|---------|------------|
| openai | OpenAI | `https://api.openai.com/v1` | gpt-4o-mini |
| deepseek | DeepSeek | `https://api.deepseek.com` | deepseek-chat |
| qwen | Qwen | `https://dashscope.aliyuncs.com/compatible-mode/v1` | qwen-plus |

- 实体：`UserLlmSettings`（1:1 User），API Key 仅存服务端
- 调度：`IUserLlmProviderFactory.GetForUserAsync` — 有 Key 则用用户 OpenAI 兼容端点，否则回落全局 `ILLMProvider`
- 影响：造句评分、自由表达、阅读词汇提取、评论 AI 回复、阅读 Agent

**前端**：`AuthContext`、`LoginPage`、`ProfilePage`；导航「我的」

设计文档：[`docs/DESIGN-auth-profile.md`](DESIGN-auth-profile.md)

---

## 4. SM-2 间隔重复

实现：`Sm2Service`（`ISm2Service`）

| 评分 | RepeatCount | IntervalDays | EaseFactor |
|------|-------------|--------------|------------|
| Forgot | 重置为 0 | 1 | -0.2（下限 1.3） |
| Fuzzy | 不变 | 1 | 不变 |
| Remembered | +1 | 第 1 次→1，第 2 次→6，之后→`interval × ease` | +0.15 |

- 最大间隔：3650 天
- `NextReviewDue = reviewedAt + IntervalDays`
- 关系表：`UserWordRelationship`（每用户每词唯一）

---

## 5. API 参考（完整列表）

### 5.0 认证与个人主页

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/auth/register` | 注册 `{ email, password, displayName? }` |
| POST | `/api/auth/login` | 登录 |
| GET | `/api/auth/me` | 当前用户（需 Bearer） |
| GET | `/api/profile` | 个人主页数据（需 Bearer） |
| PUT | `/api/profile` | 更新昵称 |
| PUT | `/api/profile/llm` | 更新 LLM 设置 |
| GET | `/api/profile/llm/presets` | LLM 预设列表 |

### 5.1 词汇

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/words` | 全量词列表 |
| GET | `/api/words/daily?count=&userId=` | 每日新词（1–20，默认 5） |
| GET | `/api/words/{id}` | 单词详情 |
| POST | `/api/words` | 创建词条（lemma 唯一） |

### 5.2 学习 / 拼写 / 造句

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/learning/submit` | 背单词提交 |
| GET | `/api/spelling/queue` | 拼写队列 |
| POST | `/api/spelling/submit` | 拼写提交 |
| GET | `/api/sentences/prompts` | 造句提示 |
| POST | `/api/sentences/rate` | 造句评分 |
| POST | `/api/free-expression/rate` | 自由表达评分 |

### 5.3 进度与日志

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/progress` | 用户进度摘要 |
| GET | `/api/logs/summary` | 活动汇总 |
| GET | `/api/logs/recent` | 近期活动 |

### 5.4 阅读

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/articles` | 文章列表 |
| GET | `/api/articles/{id}` | 文章详情 + 词汇映射 |
| POST | `/api/articles/{id}/reading/start` | 开始阅读日志 |
| POST | `/api/articles/{id}/vocab-extract` | LLM 提取词汇 |
| GET | `/api/articles/{id}/vocab` | 已有词汇映射 |
| POST | `/api/articles/{id}/lookup` | 上下文查词 |
| GET/POST | `/api/articles/{id}/comments` | 评论列表 / 发帖 |
| POST | `/api/reading-logs/{logId}/finish` | 完成阅读 |
| POST | `/api/reading-logs/{logId}/lookup` | 记录查词事件 |
| POST | `/api/reading/agent` | 阅读辅助 Agent |

### 5.5 测评 / 挑战 / 等级

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/assessment/initial/start` | 开始初测 |
| GET | `/api/assessment/{id}/step/{step}` | 获取步骤题目（1–5） |
| POST | `/api/assessment/{id}/step/{step}` | 提交步骤答案 |
| POST | `/api/assessment/{id}/complete` | 完成定级 |
| GET | `/api/assessment/{id}` | 测评详情 |
| POST | `/api/challenge/start` | 开始挑战 |
| POST | `/api/challenge/submit` | 提交挑战 |
| GET | `/api/challenge/recent` | 近期挑战记录 |
| GET | `/api/level/dashboard` | 等级看板 |
| GET | `/api/level/history` | 等级历史 |

### 5.6 LLM / 健康检查

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/llm/rate-difficulty` | 文本难度分级 |
| GET | `/api/health` | 存活探测 |
| GET | `/api/health/details` | DB + LLM 健康详情 |

---

## 6. 数据模型

### 6.1 实体关系

```
User ── UserLlmSettings (1:1)
     ──< UserProgress
     ──< UserWordRelationship >── Word ──< WordDifficultyAnnotation
     ──< WordLearningLog / SpellingLog / SentenceLog / FreeExpressionLog
     ──< ReadingLog >── Article ──< ArticleComment
                              ──< ArticleVocabMapping
     ──< Assessment ──< AssessmentRecord
     ──< ChallengeRecord
     ──< LevelHistory

Sentence（造句提示库）
DifficultyAnnotation（通用 LLM 分级缓存）
```

### 6.2 种子数据

| 类型 | 数量 | 说明 |
|------|------|------|
| 用户 | 1+ | 种子 MVP Learner + 注册用户 |
| 单词 | 6 | apple, friend, practice, memory, ambiguous, synthesize |
| 造句提示 | 10 | 与核心词及扩展词关联 |
| 短文 | 21 | `ArticleSeedData`，约 120–150 词/篇 |

### 6.3 数据库与迁移

- **Provider**：`Database:Provider` = `Sqlite` | `PostgreSql`
- **迁移**：`20260623125737_InitialCreate`（完整 schema，含用户认证与 `UserLlmSettings`）
- **启动**：非 `Testing` 环境自动 `MigrateAsync` + `SeedData.InitializeAsync`
- **集成测试**：内存 SQLite + `EnsureCreated`，跳过迁移

---

## 7. LLM 架构

### 7.1 Provider 链

```
ILLMProvider =
  LlmTelemetryProvider(          // 记录耗时 + ProfileId
    LlmRetryProvider(            // 最多 3 次，退避 1s/2s
      LlmChatClientProvider     // OpenAI 可用时
      或 LlmMockProvider        // 默认开发
    )
  )
```

**OpenAI 启用条件**：`Llm:OpenAI:Enabled=true` 且配置或环境变量 `OPENAI_API_KEY` 存在。

**用户级 LLM**：`UserLlmSettings` + `IUserLlmProviderFactory`；用户配置优先于全局 Mock/OpenAI。

**`LlmChatClientProvider` 行为**

| 操作 | 实现 |
|------|------|
| `RateDifficultyAsync` | 委托 Mock |
| `GetDefinitionAsync` | 委托 Mock |
| `RateSentenceAsync` | OpenAI（失败回落 Mock） |
| `ExtractVocabAsync` | OpenAI（失败回落 Mock） |
| `ReplyToCommentAsync` | OpenAI（失败回落 Mock） |

### 7.2 Model Profiles

| Profile ID | 用途 | 默认 |
|------------|------|------|
| `local-dev` | 通用默认 | Mock |
| `grading-stable` | 造句评分 | Mock，Temperature=0 |
| `reading-agent` | 阅读相关 | 解析为 local-dev |
| `feedback-rich` | 自由表达 | 解析为 local-dev |

未知 Profile 回落到 `local-dev`。

### 7.3 Mock 能力

- 约 150 词硬编码难度表
- 启发式造句评分
- 基于 token 的词汇提取
- 模板化评论回复

**主流程不依赖真实 LLM** 即可运行与 E2E 测试。

---

## 8. 缓存与基础设施

### 8.1 缓存

| 配置 | 开发默认 | 生产 Docker |
|------|----------|-------------|
| `Cache:Provider` | Memory | Redis |
| 实现 | `MemoryCacheService` | `RedisCacheService` |

`ICacheService` 已注册；**业务服务尚未统一接入**（LLM 分级端点直接用 `IMemoryCache`）。

### 8.2 后台 Worker

| Worker | 周期 | 行为 |
|--------|------|------|
| `ReviewReminderWorker` | 6h | 更新 `PendingReviewCount` |
| `LevelCheckWorker` | 24h | 评估并设置 `IsUpgradeCandidate` |

Worker 异常不终止 API（`BackgroundServiceExceptionBehavior.Ignore`）。集成测试会移除所有 `IHostedService`。

### 8.3 Docker Compose（生产栈）

```yaml
services: postgres:16, redis:7, api (port 8080)
```

API 环境变量：`Database__Provider=PostgreSql`，`Cache__Provider=Redis`，连接串指向 compose 内服务。

### 8.4 健康检查

| 检查项 | 说明 |
|--------|------|
| `database` | `CanConnectAsync` |
| `llm` | 仅验证 Provider 已注册（无真实探测） |

---

## 9. 前端架构

### 9.1 技术栈

- React 19、Axios、Lucide 图标
- 无 React Router — `App.tsx` 内 `view` 状态切换
- `AuthContext` + JWT（`localStorage`）
- API：`VITE_API_BASE_URL` 可选；开发时代理 `/api` → `http://localhost:8080`

### 9.2 页面与 Hooks 映射

| 页面 | Hooks |
|------|-------|
| WordCard | useWordSession, useLearningLog |
| SpellingMode | useSpellingSession |
| SentenceCard | useSentenceSession, useScoreDisplay |
| FreeExpression | — |
| ArticleReader | useArticleReader, useWordLookup, useVocabExtract |
| InitialAssessment | useAssessmentFlow |
| ChallengeMode | useChallengeFlow |
| LevelDashboard | — |
| ReviewQueue | — |
| ProfilePage | `AuthContext` |
| LoginPage | `AuthContext` |

### 9.3 共享组件（要点）

`WordDisplay`、`RatingButtons`、`FeedbackArea`、`ErrorHighlight`、`ArticleText`、`WordPopover`、`VocabExtractPanel`、`CommentThread`、`OnboardingBanner`、`UpgradeCandidateBanner`、`ErrorBoundary`

### 9.4 未接入 API

- `/api/llm/rate-difficulty`
- `/api/reading/agent`
- `/api/challenge/recent`
- `/api/level/history`（dashboard 已含近期历史）

---

## 10. 测试与质量

### 10.1 单元测试（12）

| 类 | 覆盖 |
|----|------|
| Sm2ServiceTests | Forgot 重置、Remembered 增间隔 |
| AssessmentScoringServiceTests | 词汇映射、短板定级 |
| LevelUpgradeEngineTests | 锁定、候选条件、等级上限 C1 |
| Phase6InfrastructureTests | RedisCache、LlmTelemetry |

### 10.2 集成测试（3）

| 类 | 覆盖 |
|----|------|
| AssessmentIntegrationTests | 初测启动、progress 字段 |
| ArticleIntegrationTests | 文章列表非空 |

### 10.3 E2E（Playwright，2）

| 用例 | 验证 |
|------|------|
| assessment.spec | 进入测评并开始步骤 1 |
| reading.spec | 文库 21 篇、打开首篇 |

**运行**：`npm run test:e2e`（自动启动 API `:5108` + Vite `:5173`）

**注意**：`vite.config.ts` 代理指向 `:8080`，与 `launchSettings` 的 `:5108` 不一致；本地单独 `npm run dev` 时需 API 在 8080 或调整代理。

### 10.4 测试缺口

- 大部分 API 端点无集成覆盖
- OpenAI 路径、Worker 逻辑、PostgreSQL/Redis 路径
- 前端：拼写、造句、挑战完整流程、初测 5 步提交

---

## 11. 本地开发

### 11.1 后端

```bash
cd Backend
dotnet run --project NextWord.Api
# 默认 http://localhost:5108
```

- SQLite 文件：`nextword-dev.db`
- 开发 OpenAPI：`/openapi/v1.json`（Development 环境）

### 11.2 前端

```bash
cd Frontend
npm install
npm run dev
# http://localhost:5173
```

确保 API 端口与 `vite.config.ts` 代理一致。

### 11.3 全栈 Docker

```bash
docker compose up --build
# API http://localhost:8080
```

### 11.4 测试命令

```bash
cd Backend && dotnet test
cd Frontend && npm run build
cd Frontend && npm run test:e2e
```

---

## 12. 配置参考

### 12.1 appsettings.json（开发）

```json
{
  "Database": { "Provider": "Sqlite" },
  "Cache": { "Provider": "Memory" },
  "Llm": { "OpenAI": { "Enabled": false, "Model": "gpt-4o-mini" } },
  "Auth": {
    "JwtSecret": "...",
    "Issuer": "NextWord",
    "Audience": "NextWord",
    "ExpirationDays": 7
  }
}
```

### 12.2 生产（Docker / appsettings.Production.json）

- PostgreSQL + Redis
- `Llm:OpenAI:Enabled: true`（需 API Key）

---

## 13. 已知限制与迭代 backlog

摘自 [`next-steps.md`](../next-steps.md) 与代码现状：

### P0 — 生产验证

- [ ] docker-compose 全栈迁移与冒烟
- [ ] Redis 缓存命中率观测
- [ ] CI 接入 `npm run test:e2e`

### P1 — 质量与运维

- [ ] SQLitePCLRaw 安全告警
- [ ] 剩余 SQLite `DateTimeOffset` 查询改内存排序
- [ ] ReviewReminderWorker 推送/邮件（可选）
- [ ] PostgreSQL 专用集成测试

### P2 — 功能

- [ ] 初测 E2E 覆盖完整 5 步
- [ ] LLM 遥测接入 OpenTelemetry
- [ ] `ICacheService` 接入词库/文章列表；Redis 失效策略
- [ ] 挑战模式改为真实交互答题
- [ ] 前端接入阅读 Agent、LLM 分级 API
- [x] 用户认证与个人主页（JWT + LLM 配置）
- [ ] URL 路由与深链接
- [ ] `userLevel` 从 progress 动态读取

### 架构演进（见 PLAN-Overview）

- Agent Framework 仅在多 Agent 长流程场景再评估
- 确定性规则（SM-2、定级、升降级）保持不由 Agent 决策

---

## 14. 文档维护说明

| 变更类型 | 建议更新 |
|----------|----------|
| 新 API | 第 5 节 + 对应模块第 3 节 |
| 新业务规则 | 第 3–4 节 |
| 新实体/迁移 | 第 6 节 |
| 前端页面/流程 | 第 9 节 |
| Phase 完成 | 第 1.2 节 + [`development-log.md`](../development-log.md) |
| 迭代优先级 | 第 13 节 ↔ [`next-steps.md`](../next-steps.md) |

---

## 附录 A：枚举速查

| 枚举 | 值 |
|------|-----|
| `CefrLevel` | A1, A2, B1, B2, C1, C2 |
| `DifficultyLevel` | Basic, Intermediate, Advanced |
| `AssessmentResult` | Remembered, Fuzzy, Forgot |
| `AssessmentStepType` | Vocabulary=1, Spelling=2, Sentence=3, Reading=4, FinalLevel=5 |
| `ChallengeType` | Daily, LevelConfirmation |
| `LevelChangeReason` | Initial, Upgrade, Rollback |
| `RecommendedAction` | Learn, Review, Skip, … |
| `ItemType` | Word, Sentence, Article |

## 附录 B：项目文件索引

| 路径 | 职责 |
|------|------|
| `Backend/NextWord.Api/Program.cs` | 宿主、CORS、迁移、Health |
| `Backend/NextWord.Api.Endpoints/AuthEndpoints.cs` | 认证路由 |
| `Backend/NextWord.Api.Endpoints/ProfileEndpoints.cs` | 个人主页路由 |
| `docs/DESIGN-auth-profile.md` | 认证与个人主页设计 |
| `Backend/NextWord.Infrastructure/DependencyInjection.cs` | DI 注册 |
| `Backend/NextWord.Infrastructure/Data/SeedData.cs` | 种子数据 |
| `Backend/NextWord.Domain/Services/*.cs` | 领域算法 |
| `Frontend/src/App.tsx` | 导航与页面壳 |
| `Frontend/src/api/endpoints.ts` | API 路径常量 |
| `docker-compose.yml` | 生产栈 |
| `plans/PLAN-Overview.md` | 原始分阶段计划 |
