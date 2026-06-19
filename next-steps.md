# NextWord 下一步计划

按优先级排序，可执行、可验证。

## P0 — 可选后续增强
1. RedisCacheService 生产缓存（替换 MemoryCache）
2. PostgreSQL 迁移与 docker-compose 扩展
3. 集成测试项目（Assessment / Article API）
4. LLM 遥测与成本统计装饰器
5. 后台 LevelCheckWorker / ReviewReminderWorker
6. 首次登录引导至 InitialAssessment

## P1 — 质量与运维
1. 修复 SQLitePCLRaw 安全告警（升级依赖）
2. 完善 EF snapshot 与 Assessment 实体快照一致性
3. E2E 测试（Playwright）覆盖阅读与测评主路径
