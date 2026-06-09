# Phase 1: 造句模块 + SM-2 深化 + 学习日志体系

## 目标

在 Phase 0 的背单词 MVP 基础上，完成造句模块（指定词造句 + 自由表达造句），集成真实的 LLM 评分能力，深化 SM-2 复习调度，完善学习日志体系。

**本 Phase 新增：**
- 造句模块完整实现（模式 A + 模式 B）
- 真实 LLM Provider 接入（替代 Mock）
- 拼写模式(B)加入背单词模块
- 学习日志体系统一

## 包含模块

- 背单词模块：增加拼写模式(B)
- 造句模块：模式 A（指定词造句）+ 模式 B（自由表达造句）
- LLM 评分集成
- SM-2 复习队列与定时提醒
- 学习日志统一查询与统计

## 关键交付物

1. 背单词模块双模式（翻译识别 + 拼写）完整可用
2. 造句模块双模式（指定词 + 自由表达）完整可用
3. 真实 LLM Provider 接入（OpenAI SDK 或 Anthropic SDK）
4. 拼写日志表 + 造句记录表 + 自由表达训练记录表
5. SM-2 复习队列自动计算 + 前端"今日复习"入口

## 技术层面需要创建的文件/模块

### 后端新增文件

```
Backend/
├── NextWord.Domain/
│   ├── Entities/
│   │   ├── Sentence.cs                   # 新增：句子实体
│   │   ├── SentenceLog.cs                # 新增：造句记录
│   │   ├── FreeExpressionLog.cs           # 新增：自由表达训练记录
│   │   └── SpellingLog.cs                # 新增：拼写日志
│   ├── Interfaces/
│   │   ├── ISentenceService.cs           # 新增：造句服务接口
│   │   ├── IFreeExpressionService.cs     # 新增：自由表达服务接口
│   │   └── ISpellingService.cs           # 新增：拼写服务接口
│   └── Services/
│       ├── LlmOpenAiProvider.cs          # 新增：OpenAI Provider 实现
│       ├── LlmAnthropicProvider.cs       # 可选：Anthropic Provider 实现
│       └── SentenceRatingService.cs      # 新增：LLM 评分聚合服务
├── NextWord.Infrastructure/
│   └── Data/
│       └── ApplicationDbContext.cs       # 新增 DbContext 集合
└── NextWord.Api.Endpoints/
    ├── SentenceEndpoints.cs              # 新增：造句相关端点
    ├── FreeExpressionEndpoints.cs        # 新增：自由表达端点
    ├── SpellingEndpoints.cs              # 新增：拼写模式端点
    └── LogEndpoints.cs                   # 新增：日志查询端点
```

### 前端新增文件

```
Frontend/
├── src/
│   ├── pages/
│   │   ├── SentenceStudio.tsx            # 新增：造句页面入口
│   │   ├── SentenceCard.tsx              # 新增：指定词造句卡片
│   │   ├── FreeExpression.tsx            # 新增：自由表达页面
│   │   ├── SpellingMode.tsx              # 新增：拼写模式页面
│   │   └── ReviewQueue.tsx               # 新增：复习队列页面
│   ├── components/
│   │   ├── ScoreCard.tsx                 # 新增：评分卡片（三维度评分）
│   │   ├── AiRevision.tsx                # 新增：AI 修改版本展示
│   │   ├── ErrorAnalysis.tsx             # 新增：问题分析卡片
│   │   ├── AudioPlayer.tsx               # 新增：单词发音播放
│   │   ├── SpellingInput.tsx             # 新增：拼写输入框
│   │   ├── ErrorHighlight.tsx            # 新增：拼写错误高亮
│   │   └── SceneSelector.tsx             # 新增：场景选择器（生活/职场/学术）
│   ├── hooks/
│   │   ├── useSentenceSession.ts         # 新增：造句会话管理
│   │   ├── useSpellingSession.ts         # 新增：拼写会话管理
│   │   └── useScoreDisplay.ts            # 新增：评分数据格式化
│   └── types/
│       └── sentence.ts                   # 新增：造句相关类型
```

## 本 Phase 新增数据库 Schema

### 新增表

| 表名 | 说明 | 关键字段 |
|------|------|----------|
| Sentences | 句子库（系统出题词例句） | Id, Content, TargetWord, DifficultyLevel, CefrLevel, Scene, AnnotationId |
| SentenceLogs | 指定词造句记录 | Id, UserId, WordId, UserSentence, AiRevision, GrammarScore, NaturalScore, VocabularyScore, OverallGrade, ErrorTags, Timestamp |
| FreeExpressionLogs | 自由表达训练记录 | Id, UserId, UserText, AiScore, AiRevision, ErrorSentences, Suggestions, DifficultyLevel, Timestamp |
| SpellingLogs | 拼写日志 | Id, UserId, WordId, UserSpelling, CorrectSpelling, IsCorrect, ErrorPositions, Timestamp, Attempts |

### 实体关系扩展

```
Words (1) ──< (N) Sentences          # 系统出题词关联例句
Words (1) ──< (N) SpellingLogs       # 拼写记录关联词
Sentences ── (0..1) DifficultyAnnotations  # 句子 LLM 分级
Users (1) ──< (N) SentenceLogs       # 造句记录
Users (1) ──< (N) FreeExpressionLogs # 自由表达记录
```

## LLM 评分实现细节

### SentenceRatingRequest / SentenceRatingResponse

```
SentenceRatingRequest:
  UserSentence: string
  TargetWord: string
  Scene: string          // 生活/职场/学术等场景
  UserLevel: string      // 当前用户等级

SentenceRatingResponse:
  GrammarScore: int (0-5)
  NaturalScore: int (0-5)
  VocabularyScore: int (0-5)
  RelevanceScore: int (0-5)    // 词义契合度（评分体系新增）
  OverallGrade: string         // A/B/C/D
  AiRevision: string           // 更自然表达版本
  ErrorAnalysis: string[]      // 问题分析列表
  DifficultyLevel: basic | intermediate | advanced
  Suggestion: string           // 建议学习点
```

### LLM Provider 提示词设计原则

- **一致性**：评分标准固定（0-5分），不因 provider 变化而漂移
- **结构化输出**：强制 JSON 输出，前端直接消费
- **上下文注入**：提示词中包含用户当前等级，让评分贴合阶段
- **降级策略**：LLM 调用失败时，返回默认评分（如 3/5/3/3, grade=C）

### Prompt 示例（OpenAI）

```
You are an English language assessment assistant. Rate this sentence:

User Level: {user_level}
Target Word: {target_word}
Scene: {scene}
User Sentence: {user_sentence}

Return JSON:
{
  "grammar_score": <int 0-5>,
  "natural_score": <int 0-5>,
  "vocabulary_score": <int 0-5>,
  "relevance_score": <int 0-5>,
  "overall_grade": "<A/B/C/D>",
  "ai_revision": "<string>",
  "error_analysis": ["<string>"],
  "suggestion": "<string>"
}

Rules:
- grammar_score: Check for grammatical correctness
- natural_score: How native-sounding the expression is
- vocabulary_score: Correct and natural use of target_word
- relevance_score: Does the sentence correctly use the target word's meaning?
- Be fair but not overly generous — this assesses real ability
```

## SM-2 深化

### 本 Phase SM-2 改进

1. **拼写模式集成 SM-2**：拼写正确率低于阈值时，自动降低 EaseFactor，增加复习频率
2. **复习提醒**：后端增加定时任务（BackgroundService），计算当日待复习词汇，前端首页显示"今日 X 词待复习"
3. **复习队列优先级**：同一天有多个复习词时，按 EaseFactor 升序排列（先复习最难的）
4. **拼写日志与复习队列联动**：连续拼写错误 2 次，自动将该词加入高频复习队列

## Phase 1 技术决策理由

1. **OpenAI SDK 作为首个真实 Provider**：生态最成熟，结构化输出支持最好，便于快速验证
2. **保留 Anthropic Provider 作为可选文件**：需求说 Provider 未定，但 Anthropic 的指令遵循评分可能更稳定，预留实现但不注册为默认
3. **SentenceLogs 持久化评分**：需求要求"评分和解析需要记录"，便于后续分析用户薄弱点
4. **自由表达与指定词造句分开表**：自由表达不绑定特定词汇，数据结构差异大，不宜合并
5. **拼写错误高亮用 Levenshtein 距离**：前端用 diff 算法定位差异位置，无需后端参与
6. **音频播放用 Web Speech API**：浏览器原生支持，无需后端语音合成服务，降低复杂度
7. **场景选择器前置**：指定词造句时让用户选择场景（生活/职场/学术），让 LLM 评分更贴合上下文
