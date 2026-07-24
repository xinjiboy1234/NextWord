# NextWord — 待办清单

> 更新：2026-07-20（文档集整理，合并原 SPEC/审计文档的未闭环事项）

## 阅读模块

### 已完成
- [x] 查词/重点词结构化例句 + 文章级 DB 缓存
- [x] 重点词汇音标与用法例句持久化
- [x] 存量词汇 lazy backfill（缺 phonetics/examples 时补全）

### 可选后续
- [ ] 强制重新提取重点词汇（`force=true`）
- [ ] 生产部署前审查 `20260708161532` 迁移 SQL（EF 生成了较大 Align 块）
- [ ] 阅读推荐按 EffectiveDifficulty 选文（原 CEFR 审计 follow-up）

## Score 内核 v1

### 已完成
- [x] Layer 0–5 核心 + ToolRegistry + DuckDuckGo
- [x] 日快照 worker + scores/history API
- [x] ReAnnotation worker + FeedbackButton
- [x] EvaluationDataAssembler 工具预取
- [x] CEFR 展示 toggle（Profile 设置）
- [x] E2E `challenge.spec.ts`（API 级）
- [x] CEFR read-path audit（读路径已切 Score；原审计文档随 v1 落地归档删除）
- [x] Backfill drill 操作说明 `Backend/Scripts/README_BackfillDrill.md`
- [x] PG Score 内核 schema 补丁（含 WordDifficultyAnnotations 缺列修复）

### 待收尾（发布前）
- [ ] staging 环境实际执行 backfill drill 并填记录表（原 T-005）
- [x] 评估报告 LLM 结构化叙事（当前模板 + toolPrefetch）→ 已并入 T-005 完成：报告切换为 WeaknessProfile 已验证 Finding 列表
- [ ] Annotation lookup singleflight
- [ ] SentenceStudio 评分入参改传难度 bucket（原审计 follow-up）
- [ ] 测试补齐：集成/E2E 覆盖率、负载测试、人工评测抽样（原 T-060/061/063/064）
- [ ] E2E 纳入 CI（原 T-062）
- [ ] OpenTelemetry 可观测性（原 T-065）
- [ ] Release Blockers B1–B8 sign-off + v1 正式发布（原 T-067；原风险登记册已随文档整理归档删除，发布前从 git 历史 `docs/SPEC-ai-learning-risk-register.md` 恢复核对）

## 验收

- [x] `dotnet test` 54 通过（PostgreSQL，`docker compose up -d postgres`）
- [x] `npm run build` 通过
- [x] 本地全栈实测（2026-07-20）：注册/登录/每日词/文章/profile/progress、前端页面与 /api 代理
- [ ] `npm run test:e2e` 全绿（自动拉起 API :5108 + 前端 :5173）
