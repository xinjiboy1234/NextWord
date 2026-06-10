# NextWord 下一步执行计划

## 结论

下一步不要继续扩需求，也不要直接跳到 Phase 1/2。现在应该开始落地 **Phase 0: MVP**，目标是先跑通一个最小但完整的背单词学习闭环。

一句话版本：

> 先搭工程骨架，再实现单用户背单词 MVP：取词、答题、反馈、写日志、更新 SM-2 复习状态，并用 Mock LLM 完成分级接口占位。

## 为什么先做这个

当前需求文档已经把产品方向、阶段边界、LLM 抽象、SM-2、阅读、测评和后续 Agent 能力规划清楚了。继续补需求的收益不高，反而容易让范围膨胀。

Phase 0 是最合适的下一步，因为它能验证三个核心假设：

- 学习闭环是否顺：用户能不能快速完成一轮背单词训练。
- 数据模型是否站得住：日志、用户-词关系、复习时间能不能支撑后续模块。
- LLM 边界是否清晰：业务代码是否只依赖 `ILLMProvider`，而不是绑定具体模型 SDK。

## 下一步做什么

### 1. 创建工程骨架

先创建后端和前端基础项目。

后端：
- `Backend/NextWord.sln`
- `Backend/NextWord.Api`
- `Backend/NextWord.Domain`
- `Backend/NextWord.Infrastructure`
- `Backend/NextWord.Api.Endpoints`

前端：
- `Frontend/`
- React + Vite + TypeScript + Tailwind CSS

验收标准：
- 后端 `dotnet build` 通过。
- API 能启动。
- 前端开发服务器能启动。

### 2. 先定 Phase 0 数据模型

优先实现这些实体和枚举：

- `User`
- `Word`
- `UserProgress`
- `UserWordRelationship`
- `WordLearningLog`
- `DifficultyAnnotation`
- `DifficultyLevel`
- `CefrLevel`
- `AssessmentResult`
- `RecommendedAction`

验收标准：
- EF Core `ApplicationDbContext` 能创建 SQLite 数据库。
- 第一版 migration 能正常执行。
- 有少量种子词可用于联调。

### 3. 实现 SM-2 和复习队列

先把确定性学习规则做好。

需要实现：
- `ISm2Service`
- `Sm2Service`
- `IReviewQueueService`
- `ReviewQueueService`

验收标准：
- “不会 / 模糊 / 记住”三种评分能更新 `NextReviewDue`。
- 今日复习队列能查询 `NextReviewDue <= 当前时间` 的词。
- SM-2 逻辑有单元测试。

### 4. 实现 LLM Mock 分级门面

Phase 0 不接真实模型，只做抽象和 Mock。

需要实现：
- `ILLMProvider`
- `IModelProfileResolver`
- `LlmMockProvider`
- `ModelProfileResolver`
- `LlmPromptFactory`
- `LlmResponseParser`

验收标准：
- 分级调用统一走 `ILLMProvider`。
- 预置核心词返回稳定分级。
- 未知词返回默认值。
- 业务层不直接依赖 OpenAI、Azure OpenAI、Ollama 等具体 SDK。

### 5. 实现最小 API

先做能支撑前端学习闭环的端点：

- `GET /api/words/daily`
- `GET /api/reviews/due`
- `POST /api/learning/word-answer`
- `GET /api/progress`
- `POST /api/llm/rate-difficulty`

验收标准：
- 提交一次答案后能写入 `WordLearningLog`。
- 能更新 `UserWordRelationship` 的学习次数、正确次数、复习时间。
- 前端能通过 API 完成一轮背单词。

### 6. 实现前端 MVP

先做简单、可用、闭环的工具型界面。

优先页面和组件：
- `Home.tsx`
- `WordCard.tsx`
- `Progress.tsx`
- `WordDisplay.tsx`
- `AnswerInput.tsx`
- `FeedbackArea.tsx`
- `RatingButtons.tsx`
- `ProgressBar.tsx`
- `useWordSession.ts`
- `useLearningLog.ts`

验收标准：
- 用户能进入今日新词。
- 用户能看到英文单词并输入中文含义。
- 提交后能看到正确释义和反馈。
- 点击“记住 / 模糊 / 不会”后进入下一个词。
- 进度页能看到基础统计。

### 7. 补最小测试

优先测试：
- SM-2 三种评分路径。
- 复习队列 due 查询。
- MockProvider 分级默认值和预置词。
- 学习提交后的日志写入和关系表更新。

验收标准：
- Domain 核心测试可独立运行。
- 至少有一条 API 学习闭环的集成测试或可重复手动验收脚本。

## Phase 0 暂时不要做

- 不做拼写模式。
- 不做造句评分。
- 不做阅读器。
- 不做初始测评和挑战测评。
- 不接真实 LLM Provider。
- 不做 Agent 编排。
- 不做 Redis、PostgreSQL 生产部署和完整监控。

## 开工前需要拍板的 3 个小决定

1. Phase 0 先采用单用户模式，认证后置。
2. 初始核心词表先内置 50-100 个演示词。
3. 前端先做简洁工具型布局，等学习流程稳定后再做视觉打磨。

推荐默认答案：以上三项都选“是”。这样可以最快进入可运行 MVP。
