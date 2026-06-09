# NextWord 英语学习应用 — 分阶段落地计划总览

## 项目概要

| 项目 | 详情 |
|------|------|
| 名称 | NextWord |
| 后端 | ASP.NET Core Web API (.NET 8) |
| 前端 | React (Vite + TypeScript + Tailwind CSS) |
| 数据库 | SQLite (开发/本地) / PostgreSQL (生产) |
| LLM Provider | 抽象接口层，运行时可插拔（预留 OpenAI/Anthropic/本地模型） |
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
| LLM抽象 | 策略模式 + ILLMProvider接口 | 运行时可切换provider |
| 缓存 | Redis (生产) / MemoryCache (开发) | 分级结果复用，减轻LLM调用 |
| 间隔重复 | SM-2 自实现 | 需求明确，无需第三方库 |

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
