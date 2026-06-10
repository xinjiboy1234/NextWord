# Phase 2: 阅读模块 — 短文阅读器 + 重点词汇提取 + 评论

## 目标

实现可操作的文本阅读器，支持点击查词、LLM 重点词汇提取、段落评论功能。这是产品"工具感"的核心模块。

## 包含模块

- 阅读模块完整实现
- 短文管理（内置题库 + LLM 生成）
- 点击单词查释义（缓存 + LLM 按需生成）
- LLM 重点词汇提取
- 段落评论系统
- 阅读辅助 Agent（首个 skills/plugins 组合能力）

## 关键交付物

1. 短文阅读器 UI（点击查词 + 重点词汇提取）
2. LLM 词汇提取 API（结合用户等级筛选"值得学"的词）
3. 评论系统（段落评论 + AI 回复）
4. 内置短题库（至少 20 篇不同等级的短文）
5. 阅读日志（用时、查词次数、评论数）
6. 阅读辅助 Agent：可动态组合查词、释义、重点词提取、例句生成、评论回复等工具

## 技术层面需要创建的文件/模块

### 后端新增文件

```
Backend/
├── NextWord.Domain/
│   ├── Entities/
│   │   ├── Article.cs                  # 新增：短文实体
│   │   ├── ReadingLogs.cs              # 新增：阅读日志
│   │   ├── ArticleComments.cs          # 新增：段落评论
│   │   └── ArticleVocabMapping.cs      # 新增：短文-词汇映射
│   ├── Interfaces/
│   │   ├── IArticleService.cs          # 新增：短文服务
│   │   ├── IArticleVocabService.cs     # 新增：重点词汇提取服务
│   │   ├── ICommentService.cs          # 新增：评论服务
│   │   └── IReadingAgentService.cs     # 新增：阅读辅助 Agent 服务
│   └── Services/
│       ├── LlmVocabExtractor.cs        # 新增：基于 ILLMProvider 的重点词汇提取
│       ├── CommentAiResponder.cs       # 可选：基于 ILLMProvider 的 AI 回复评论
│       ├── ReadingAssistantAgent.cs    # 新增：阅读辅助 Agent 编排
│       └── ReadingSkillRegistry.cs     # 新增：阅读 skills/plugins 注册表
├── NextWord.Infrastructure/
│   └── Data/
│       └── ApplicationDbContext.cs     # 新增 Article 等集合
└── NextWord.Api.Endpoints/
    ├── ArticleEndpoints.cs             # 新增：短文 CRUD、阅读管理
    ├── VocabExtractEndpoints.cs        # 新增：重点词汇提取
    ├── ReadingAgentEndpoints.cs        # 新增：阅读辅助 Agent 入口
    ├── CommentEndpoints.cs             # 新增：评论 CRUD + AI 回复
    └── ReadingLogEndpoints.cs          # 新增：阅读日志
```

### 前端新增文件

```
Frontend/
├── src/
│   ├── pages/
│   │   ├── ArticleReader.tsx           # 新增：短文阅读器主页面
│   │   ├── ArticleLibrary.tsx          # 新增：短文库（按等级筛选）
│   │   └── CommentThread.tsx           # 新增：评论列表
│   ├── components/
│   │   ├── ArticleText.tsx             # 新增：可点击短文渲染
│   │   ├── WordPopover.tsx             # 新增：点击单词弹出释义卡片
│   │   ├── VocabExtractPanel.tsx       # 新增：重点词汇提取面板
│   │   ├── VocabTable.tsx              # 新增：词汇表格展示
│   │   ├── ParagraphHighlight.tsx      # 新增：段落选择高亮
│   │   ├── CommentInput.tsx            # 新增：评论输入框
│   │   └── AiCommentReply.tsx          # 新增：AI 回复展示
│   ├── hooks/
│   │   ├── useArticleReader.ts         # 新增：阅读会话管理
│   │   ├── useWordLookup.ts            # 新增：查词逻辑
│   │   └── useVocabExtract.ts          # 新增：词汇提取调用
│   └── types/
│       └── article.ts                  # 新增：文章相关类型
```

## 本 Phase 数据库 Schema

### 新增表

| 表名 | 说明 | 关键字段 |
|------|------|----------|
| Articles | 短文库 | Id, Title, Content, DifficultyLevel, CefrLevel, WordCount, Source (builtin/llm), AnnotationId, CreatedAt |
| ReadingLogs | 阅读日志 | Id, UserId, ArticleId, StartTime, EndTime, DurationSeconds, LookupCount, CommentsCount, Timestamp |
| ArticleComments | 段落评论 | Id, UserId, ArticleId, ParagraphIndex, ParagraphText, CommentText, AiReply, Timestamp |
| ArticleVocabMappings | 短文-词汇映射 | Id, ArticleId, WordId, ContextMeaning, SpecialUsage, DifficultyInContext, IsKeyVocab |

### 实体关系扩展

```
Articles (1) ──< (N) ReadingLogs        # 阅读记录
Articles (1) ──< (N) ArticleComments    # 评论
Articles (1) ──< (N) ArticleVocabMappings   # 词汇映射
Articles ── (0..1) DifficultyAnnotations  # 文章 LLM 分级
Words ──< (N) ArticleVocabMappings ──> (N) Articles  # 词-文多对多
```

## LLM 重点词汇提取

## 阅读辅助 Agent

阅读模块是第一批引入 Agent/skills/plugins 的落点。它面对的是开放式学习场景：用户可能点击查词、追问文中含义、要求例句、让系统提取重点词、评论某一段，也可能让系统判断某个词是否值得加入生词本。相比写死多个 `switch case`，这里更适合让 LLM 在受控工具集合中动态选择。

### Skills / Plugins 范围

首批阅读 skills/plugins：
- `LookupWordSkill`：读取本地词库、缓存释义、上下文释义。
- `ExplainInContextSkill`：结合段落解释词义、短语、句法或隐含含义。
- `ExtractKeyVocabSkill`：提取当前等级值得学习的重点词。
- `GenerateExamplesSkill`：为选中词生成适合当前等级的例句。
- `CommentReplySkill`：针对用户段落评论生成解释性回复。
- `AddToVocabularySkill`：给出加入生词本建议，并在用户确认后写入关系表。

### Agent 边界

- Agent 可以决定调用哪些 skills/plugins，以及调用顺序。
- Agent 不能直接修改等级、复习间隔、测评结果；涉及写入操作必须通过受控服务接口。
- 默认只允许读取文章、词汇、用户当前等级和近期学习摘要；更敏感的历史数据需要显式传入。
- 所有 Agent 输出都需要结构化记录：调用了哪些工具、输入摘要、输出摘要、耗时、失败原因。
- 如果 Agent 调用失败，阅读主流程保持可用，回退到普通查词、缓存结果或提示稍后重试。

### LLM 调用边界

阅读模块不直接依赖具体模型 SDK，统一通过 Phase 0/1 建立的 `ILLMProvider` 调用底层 Microsoft.Extensions.AI `IChatClient`：

- `ReadingAssistantAgent` 负责在阅读场景中选择和组合 skills/plugins。
- `LlmVocabExtractor` 只负责重点词提取的提示词、结构化输出校验和结果持久化。
- 点击查词、重点词汇提取、短文生成、AI 评论回复都共用统一的缓存、超时、降级和遥测策略。
- 如果 Provider 不可用，阅读主流程仍可使用内置题库和本地词库，LLM 能力降级为“稍后重试/返回缓存结果”。
- 本 Phase 优先用 Microsoft.Extensions.AI 的 function/tool calling 与本地注册表实现；只有当阅读辅助演进为长流程、多 Agent 或人机协同工作流时，再评估 Microsoft Agent Framework。

### API 流程

```
POST /api/articles/{id}/vocab-extract

Request: {}

Steps:
1. 后端读取文章全文
2. 调用 LLM 提取重点词汇，传入用户当前等级
3. LLM 返回结构化结果：
   {
     "keyVocab": [
       {
         "word": "string",
         "contextMeaning": "string",
         "specialUsage": "string",
         "difficulty": "basic|intermediate|advanced",
         "action": "learn_now|review_later|challenge_only"
       }
     ],
     "skippedBasic": ["string[]"],    // 太基础，跳过
     "skippedRare": ["string[]"]      // 太偏，以后挑战
   }
4. 结果持久化到 ArticleVocabMappings
5. 前端展示为可点击表格
```

### LLM 提示词设计

```
You are a vocabulary extraction assistant for English learners.

Article Level: {article_level}
User Level: {user_level}

Article:
{article_content}

Extract vocabulary that is worth learning for a {user_level} level student.
Include words that are:
- Slightly above their current level (learning opportunity)
- Have interesting or unusual usage in context
- Are key to understanding the article

Return JSON with keyVocab (max 10), skippedBasic, and skippedRare arrays.

Rules:
- Do not include very basic words (the, and, is, etc.)
- Do not include highly specialized academic jargon
- Context-specific meanings matter — evaluate each word IN CONTEXT
- Max 10 key vocabulary items
```

## 短文来源管理

### 混合方案实现

| 来源 | 说明 | 优先级 |
|------|------|--------|
| 内置题库 | 预先编写/收录的短文，人工分级 | 高（保底） |
| LLM 生成 | 按需生成适合当前等级的短文 | 中（补充） |

### 内置题库设计

- 每级 10 篇（basic 10 + intermediate 10 + advanced 10 = 30 篇）
- 120-180 词/篇（初测用）或 150 词/篇（挑战用）
- 每篇附：标题、难度标签、核心主题标签
- 来源字段标记为 "builtin"

### LLM 短文生成

- 调用 LLM 生成短文，传入目标等级和主题
- 生成后写入 Articles 表，Source = "llm"
- 同样经过 DifficultyAnnotation 分级
- 生成后存入 ArticleVocabMappings（预提取词汇）

## Phase 2 技术决策理由

1. **短文阅读器用自定义渲染**：不是直接嵌入 HTML，而是用 React 组件逐词渲染，这样才能绑定 onClick 事件做查词
2. **查词优先本地缓存**：同一篇文章中同一单词只需查一次，后续直接读 ArticleVocabMappings
3. **段落评论用 ParagraphIndex**：不存完整段落文本作为键，用段落索引定位，节省存储且支持编辑
4. **AI 回复评论可选**：需求说"AI 可回复解释（可选）"，实现为独立端点，默认不开启，降低 LLM 调用量
5. **阅读日志不存逐题结果**：因为阅读测评（选择题）在 Phase 3，本 Phase 的阅读日志只记录行为数据（用时、查词次数）
6. **ArticleVocabMappings 独立表**：词汇在文章中的用法是上下文相关的，不能复用 Word 表的通用释义
7. **LLM 调用统一走 Microsoft.Extensions.AI 管道**：阅读场景复用全局 `ILLMProvider`，保持 Provider 切换、缓存、超时和遥测策略一致
8. **阅读辅助 Agent 作为首个 Agent 落点**：阅读场景最需要动态工具组合，风险低于测评和等级系统，适合作为 skills/plugins 能力的第一阶段验证
