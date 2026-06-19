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
