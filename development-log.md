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
