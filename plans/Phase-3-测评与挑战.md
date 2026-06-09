# Phase 3: 测评与挑战 — 初测 + 挑战测评 + 等级升降体系

## 目标

实现完整的测评系统，包括首次进入的 5 步初始水平测评，以及日常的挑战式测评和等级升降机制。这是产品的"智能感"核心。

## 包含模块

- 首次 5 步测评（词汇识别 → 拼写 → 造句 → 阅读 → 定级）
- 日常挑战测评
- 等级升降规则
- 等级确认挑战（防误升）
- 等级历史追踪

## 关键交付物

1. 首次测评 5 步端到端流程
2. 挑战测评触发条件 + 挑战包生成
3. 等级升降引擎（满足条件自动升级 + 确认挑战）
4. 等级历史追踪（升降记录）
5. 测评数据面板（各维度正确率、趋势图）

## 技术层面需要创建的文件/模块

### 后端新增文件

```
Backend/
├── NextWord.Domain/
│   ├── Entities/
│   │   ├── Assessments.cs              # 新增：测评记录
│   │   ├── AssessmentRecords.cs        # 新增：各步骤测评明细
│   │   ├── ChallengeRecords.cs         # 新增：挑战记录
│   │   └── LevelHistories.cs           # 新增：等级历史
│   ├── Interfaces/
│   │   ├── IAssessmentService.cs       # 新增：测评服务
│   │   ├── IChallengeService.cs        # 新增：挑战服务
│   │   ├── ILevelEngine.cs             # 新增：等级升降引擎
│   │   └── IChallengePackGenerator.cs  # 新增：挑战包生成器
│   └── Services/
│       ├── AssessmentEngine.cs         # 新增：测评流程引擎
│       ├── LevelUpgradeEngine.cs       # 新增：等级升降引擎实现
│       ├── ChallengePackGenerator.cs   # 新增：挑战包生成
│       └── AssessmentScoringService.cs # 新增：测评评分聚合
├── NextWord.Infrastructure/
│   └── Data/
│       └── ApplicationDbContext.cs     # 新增测评相关集合
└── NextWord.Api.Endpoints/
    ├── AssessmentEndpoints.cs          # 新增：测评流程端点
    ├── ChallengeEndpoints.cs           # 新增：挑战相关端点
    └── LevelEndpoints.cs               # 新增：等级查询/确认
```

### 前端新增文件

```
Frontend/
├── src/
│   ├── pages/
│   │   ├── InitialAssessment.tsx       # 新增：首次测评流程页面
│   │   ├── ChallengeMode.tsx           # 新增：挑战测评页面
│   │   ├── LevelDashboard.tsx          # 新增：等级面板（4维度展示）
│   │   └── LevelHistory.tsx            # 新增：等级历史
│   ├── components/
│   │   ├── AssessmentStepIndicator.tsx # 新增：测评步骤指示器
│   │   ├── VocabularyQuiz.tsx          # 新增：词汇选择题组件
│   │   ├── SpellingQuiz.tsx            # 新增：拼写测评题组件
│   │   ├── SentenceQuiz.tsx            # 新增：造句测评题组件
│   │   ├── ReadingQuiz.tsx             # 新增：阅读理解选择题
│   │   ├── LevelResultCard.tsx         # 新增：定级结果卡片
│   │   └── ChallengeResultCard.tsx     # 新增：挑战结果卡片
│   ├── hooks/
│   │   ├── useAssessmentFlow.ts        # 新增：测评流程管理
│   │   ├── useChallengeFlow.ts         # 新增：挑战流程管理
│   │   └── useLevelCalculation.ts      # 新增：等级计算
│   └── types/
│       └── assessment.ts               # 新增：测评相关类型
```

## 本 Phase 数据库 Schema

### 新增表

| 表名 | 说明 | 关键字段 |
|------|------|----------|
| Assessments | 测评记录 | Id, UserId, Type (initial/challenge), StartAt, EndAt, Status (in_progress/completed) |
| AssessmentRecords | 测评步骤明细 | Id, AssessmentId, Step (1-5), QuestionType, Questions, Answers, Scores, Timestamp |
| ChallengeRecords | 挑战记录 | Id, UserId, ChallengeType, VocabularyScore, SentenceScore, ReadingScore, TotalScore, Passed, AttemptedLevel, Timestamp |
| LevelHistories | 等级历史 | Id, UserId, FromLevel, ToLevel, Reason (upgrade/rollback/initial), Timestamp |

### 测评步骤明细结构

```
AssessmentRecords 表中 Step 字段对应的内容：

Step 1: 词汇识别测评
  Questions: [{"word": "abandon", "options": ["放弃", "维持", "存在", "出现"], "correct": 0}]
  Answers: [0, 2, 1, ...]
  Scores: {"basic_correct": 4, "mid_correct": 3, "adv_correct": 2}

Step 2: 拼写测评
  Questions: [{"chinese": "放弃", "correct": "abandon"}]
  Answers: ["abandn", "exist", ...]
  Scores: {"total": 8, "correct": 5, "spelling_error": 3}

Step 3: 造句测评
  Questions: [{"word": "maintain", "scene": "life"}]
  Answers: ["I maintain my bike weekly."]
  Scores: {"grammar": 4, "natural": 3, "relevance": 5, "difficulty": "intermediate"}

Step 4: 阅读理解测评
  ArticleId: <外键>
  Questions: [{"type": "main_idea", "options": [...], "correct": 0}]
  Scores: {"correct": 1, "duration_seconds": 120, "lookups": 3}

Step 5: 定级计算
  Results: {"vocab_level": "B1", "spelling_score": "B1", "sentence_level": "B2", "reading_level": "B1"}
  Final: "B1"
```

## 等级升降引擎

### 初始定级算法

```
输入：5 个步骤的得分

计算子等级：
  vocab_level = 根据 Step1 各难度段正确率映射到 CEFR
  spelling_level = 根据 Step2 正确率映射到 CEFR
  sentence_level = 根据 Step3 平均分映射到 CEFR
  reading_level = 根据 Step4 正确率 + 查词密度映射到 CEFR

最终等级：
  overall_level = min(vocab_level, sentence_level, reading_level)
  
  理由：短板决定整体水平，避免高估用户能力
```

### CEFR 映射表（按整体正确率映射）

| CEFR | 说明 | 词汇正确率 | 拼写正确率 | 造句平均分 | 阅读正确率 |
|------|------|-----------|-----------|-----------|-----------|
| A1 | 入门 | 0-9% | 0% | 0-0.9 | 0-19% |
| A2 | 基础 | 10-29% | 0-19% | 1.0-1.9 | 20-39% |
| B1 | 中级 | 30-49% | 20-39% | 2.0-2.9 | 40-59% |
| B2 | 中高级 | 50-69% | 40-59% | 3.0-3.9 | 60-79% |
| C1 | 高级 | 70-100% | 60-100% | 4.0-5.0 | 80-100% |
| C2 | 母语级 | — | — | — | — (需人工) |

### 挑战测评触发条件

任一满足即触发：
- 连续 2 天单词正确率 >= 85%
- 造句平均分 >= 4.0
- 阅读查词率下降（与上次测评对比下降 20%+）
- 当前等级学习天数 >= 5 天

### 挑战包生成规则

```
挑战包 = {
  vocabulary: 5题，难度 = 当前等级高一级的词
  sentence: 1题，抽象或学术词，LLM评分
  reading: 1篇，略高难度短文(150词)，1道主旨题
}
```

### 挑战判定规则

```
挑战成功 = 同时满足：
  词汇正确 >= 60%  (5题中 >= 3题)
  造句评分 >= 3.5
  阅读题答对 (1/1)

否则挑战失败，维持原等级
```

### 等级升级规则

```
连续 3 天满足：
  单词正确率 >= 80%
  拼写正确率 >= 70%
  造句平均分 >= 3.8
  阅读查词率下降或稳定低水平

→ 触发"升级预备"
→ 自动进入下一等级内容池
→ 发起"新等级确认挑战"（结构同挑战测评，难度按新等级）

确认挑战成功 → 正式升级，记录 LevelHistory
确认挑战失败 → 回退原等级，记录 LevelHistory (reason=rollback)
```

### 等级体系数据结构

```
Level:
  code: "basic" | "intermediate" | "advanced"
  cefr_range: { min: "A1", max: "A2" }  // basic 对应 A1-A2
  
  code: "intermediate"
  cefr_range: { min: "B1", max: "B2" }  // intermediate 对应 B1-B2

  code: "advanced"
  cefr_range: { min: "C1", max: "C2" }  // advanced 对应 C1-C2

UserProgress:
  overall_level: "intermediate"
  vocab_level: "B1"        // CEFR 细粒度
  spelling_level: "A2"     // CEFR 细粒度
  sentence_level: "B2"     // CEFR 细粒度
  reading_level: "B1"      // CEFR 细粒度
  streak_days: 7
  level_start_date: Date
  is_level_locked: false   // 升级确认中锁定
```

## Phase 3 技术决策理由

1. **短板定级原则**：最终等级取 min()，避免用户进入远超实际水平的内容，造成挫败
2. **挑战包预生成**：挑战测评的题目在开始前一次性生成（ChallengePackGenerator），而不是边做边出，保证测评结构一致性
3. **LevelHistory 完整记录**：每次等级变化（升级/降级/初始定级）都写记录，便于数据分析用户成长曲线
4. **连续 3 天升级规则**：防止单日表现波动导致的误升级，需要持续稳定表现才升级
5. **确认挑战防误升**：需求明确指出"不是考过一次就升级"，确认挑战是最后的门槛
6. **测评步骤明细用 JSON 存**：每步的题目和答案结构差异大，用 JSON 字段存比拆多表更灵活，EF Core 支持 JSON 列映射
