# NextWord — 待办清单

> 更新：2026-06-30（Score 内核 v1 批量落地）

## Score 内核 v1（见 `docs/SPEC-ai-learning-implementation.md`）

### 已完成（核心路径）
- [x] Layer 0：M1 迁移、ScoreMapping、ScoreProfile、EffectiveDifficulty
- [x] Layer 1：初测 complete → Profile；挑战 FR-6 A 服务端计分
- [x] Layer 2：ReadingLookup、DailyWordSelection（Score 驱动）
- [x] Layer 3：BackgroundJob、EvaluationReport（模板）、SentenceLlmScoring
- [x] FR-5 DuckDuckGo + ToolRegistry（7 handlers，`/api/tools`）
- [x] 前端：Score 类型/hooks、LevelDashboard、ChallengeMode、查词 AI、AppShell Score

### 待收尾（非阻塞开发，发布前需关）
- [ ] T-005 Backfill staging 验证 + rollback drill
- [ ] T-043 ProfileScoreSnapshot 日批 worker
- [ ] T-055 阅读/词卡 FeedbackButton + CEFR 展示 toggle
- [ ] 评估报告 LLM 结构化 + EvaluationDataAssembler 工具预取
- [ ] Annotation singleflight + ReAnnotationWorker
- [ ] Release Blockers B1–B8 sign-off（`SPEC-ai-learning-risk-register.md`）
- [ ] E2E：挑战新 submit API；`npm run test:e2e` CI 接入
- [ ] CEFR read-path grep audit（F-5）

---

## 前端 UX

### 已完成
- [x] App Shell + React Router
- [x] 挑战历史 `ChallengeRecentList`
- [x] Score 内核前端接线

### 待做
- [ ] 全流程手动验证（初测 → 等级面板 → 挑战 → 阅读查词）
- [ ] `UserAvatar` / `ErrorBoundary` 旧色类清理（低优先级）

## 验收

- [x] `dotnet test` 45 通过
- [x] `npm run build` 通过
- [ ] `npm run test:e2e`（需本地 API + 挑战流用例更新）
