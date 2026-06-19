# NextWord 下一步计划

按优先级排序，可执行、可验证。

## P0 — Phase 3 测评与挑战（进行中）
1. 新增 Assessment / AssessmentRecord / ChallengeRecord / LevelHistory 实体与迁移
2. 实现 AssessmentEngine、LevelUpgradeEngine、ChallengePackGenerator
3. 暴露测评与挑战 API（5 步初测 + 挑战包）
4. 前端 InitialAssessment、ChallengeMode、LevelDashboard 页面
5. 验收：完整初测流程可跑通，等级写入 UserProgress 与 LevelHistory

## P1 — Phase 4 完善与优化
1. ICacheService + MemoryCacheService 集成
2. LLM 重试/遥测装饰器（基础版）
3. NextWord.UnitTests：Sm2Service、LevelUpgradeEngine
4. Dockerfile + docker-compose（API + SQLite/PostgreSQL）
5. HealthChecks（DB + LLM Mock）

## P2 — 后续
1. Redis 生产缓存
2. 集成测试项目
3. 后台 LevelCheckWorker / ReviewReminderWorker
