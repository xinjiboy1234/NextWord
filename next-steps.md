# NextWord 下一步计划

按优先级排序，可执行、可验证。

## P0 — 生产增强
1. RedisCacheService + Cache:Provider 配置切换
2. docker-compose 增加 PostgreSQL + Redis 服务
3. LLM 遥测装饰器（耗时/ProfileId 日志）
4. 完善 EF snapshot（Assessment 实体与 Phase3 迁移对齐）
5. Playwright E2E：阅读 + 初测主路径

## P1 — 质量与运维
1. 修复 SQLitePCLRaw NU1903 安全告警
2. 升级候选用户前端通知（IsUpgradeCandidate 横幅）
3. ReviewReminderWorker 推送/邮件（可选）
