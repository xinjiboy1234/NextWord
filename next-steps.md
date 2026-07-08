# NextWord — 待办清单

> 更新：2026-07-06（全环境 PostgreSQL）

## Score 内核 v1

### 已完成
- [x] Layer 0–5 核心 + ToolRegistry + DuckDuckGo
- [x] T-043 日快照 worker + scores/history API
- [x] ReAnnotation worker + FR-7 FeedbackButton
- [x] EvaluationDataAssembler 工具预取
- [x] CEFR 展示 toggle（Profile 设置）
- [x] E2E `challenge.spec.ts`（API 级）
- [x] CEFR read-path audit 文档
- [x] Backfill drill 操作说明 `Backend/Scripts/README_BackfillDrill.md`

### 待收尾（发布前）
- [ ] T-005 staging 环境实际执行 backfill drill 并填记录表
- [ ] 评估报告 LLM 结构化（当前模板 + toolPrefetch）
- [ ] Annotation lookup singleflight
- [ ] Release Blockers B1–B8 sign-off
- [ ] `npm run test:e2e` 全绿（需本地 API :5108）

## 验收

- [x] `dotnet test` 45 通过（PostgreSQL，`docker compose up -d postgres`）
- [x] `npm run build` 通过
- [ ] `npm run test:e2e`
