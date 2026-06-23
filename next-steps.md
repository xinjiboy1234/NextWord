# NextWord 下一步计划

按优先级排序，可执行、可验证。

## P0 — 生产部署验证
1. docker-compose up 全栈验证（PostgreSQL + Redis + API 迁移）
2. 生产环境 Cache:Provider=Redis 压测与缓存命中率观测
3. CI 流水线加入 `npm run test:e2e`（需预装 Playwright Chromium）

## P1 — 质量与运维
1. 修复 SQLitePCLRaw NU1903 安全告警
2. ChallengeService / SpellingService 等剩余 SQLite DateTimeOffset OrderBy 统一内存排序
3. ReviewReminderWorker 推送/邮件（可选）
4. PostgreSQL 专用集成测试（当前集成测试仍用 EnsureCreated + SQLite 内存库）

## P2 — 功能迭代
1. 初测 E2E 扩展至完整 5 步提交
2. LLM 遥测接入 OpenTelemetry / 结构化日志聚合
3. Redis 缓存失效策略与词库/文章列表缓存接入
