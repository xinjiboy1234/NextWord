# NextWord AI 学习系统 — 风险登记册与验证策略

> **文档版本**：2026-06-30 v2  
> **维护**：QA-RISK（主）· ARCH · AI-ENG 会签  
> **关联**：[SPEC-ai-learning-product.md](./SPEC-ai-learning-product.md) · [SPEC-ai-learning-implementation.md](./SPEC-ai-learning-implementation.md) · [SPEC-ai-learning-team-deliberation.md](./SPEC-ai-learning-team-deliberation.md)

**风险分** = 严重度 (1–5) × 可能性 (1–5)，范围 1–25。

---

## 1. 风险登记册（32 项）

| ID | 类别 | 描述 | S | L | 分 | 检测 | 缓解 | 负责人 | 残余 |
|----|------|------|---|---|-----|------|------|--------|------|
| R-DI-01 | 数据 | CEFR→Score backfill 用区间中心值，带内用户失去粒度 | 4 | 4 | 16 | 迁移前后 500 用户抽样审计 | 保留 LegacyCefrJson；维度未知则 null 非填中心值；迁移说明弹窗 | BE+DBA | 中 |
| R-DI-02 | 数据 | 双写 CEFR/Score 漂移 | 5 | 3 | 15 |  nightly reconciliation | ScoreProfileService 唯一写者；禁止直写 OverallLevel | BE | 低 |
| R-DI-03 | 数据 | Overall=min 三维，用户不解 spelling 高 overall 低 | 3 | 4 | 12 | 支持工单分类 | 报告解释短板；UI 分列 spelling | PM+LX | 中 |
| R-DI-04 | 数据 | PersonalDifficulty EMA 冷启动震荡 | 3 | 4 | 12 | 属性测试 | knownRate 默认 0.5；≥3 次交互前 weak 分类；clamp 0–100 | BE | 中 |
| R-DI-05 | 数据 | 同形异义词全局 annotation 误用 | 4 | 3 | 12 | 50 对 polyseme 人工评测 | lookup 带 context override；低 confidence 重标 | BE+AI | 中 |
| R-DI-06 | 数据 | 报告 snapshot 与 live Profile 不一致 | 3 | 3 | 9 | 并发集成测试 | enqueue 时冻结 snapshot；UI 标注「截至 {t}」 | BE | 低 |
| R-DI-07 | 数据 | 词库缺 annotation 导致每日词单空 | 4 | 4 | 16 | 监控 daily list size | legacy DifficultyLevel→Score 启发式 fallback；预热 Top-N | BE | 中 |
| R-DI-08 | 数据 | 挑战仍读 CEFR 阈值 | 5 | 3 | 15 | 跨模块矩阵测试 | v1 同发布迁移 LevelUpgradeEngine | BE | 低 |
| R-DI-09 | 数据 | SM-2 输入改 personalDifficulty 行为突变 | 3 | 3 | 9 | 历史模拟 | 可配置 α；每日复习上限 | BE+LX | 中 |
| R-DI-10 | 数据 | 造句评分 heuristic→LLM 历史不可比 | 4 | 4 | 16 | 分布对比 | AssessmentRecord.schemaVersion；不 retro 改分 | BE+PM | 中 |
| R-DI-11 | 数据 | 造句 provisional 与最终 WritingScore 差 ≥5 | 3 | 3 | 9 | 监控 delta | 通知用户「写作分已更新」；报告 evidence 标记 | BE+FE | 低 |
| R-LLM-01 | LLM | 评价报告与 Profile 矛盾 | 5 | 3 | 15 | 自动 validator 100 报告集 | server merge；schema 无分数字段；矛盾则 template | AI+BE | 低 |
| R-LLM-02 | LLM | 查词非上下文释义 | 4 | 4 | 16 | 200 对人工/LLM-judge | sentence 片段入 prompt；contextual 标志校验 | AI | 中 |
| R-LLM-03 | LLM | 结构化 JSON 解析失败 | 4 | 4 | 16 | parse_failure_rate 指标 | JSON schema mode；repair pass；不 persist 垃圾 annotation | BE+AI | 中 |
| R-LLM-04 | LLM | 模型升级 annotation 漂移 | 3 | 4 | 12 | 版本 diff 报告 | append-only 版本；lazy re-touch | AI | 中 |
| R-LLM-05 | LLM | Tool 多轮循环成本/延迟 | 4 | 3 | 12 | tool_call_count span | v1 Evaluation 禁用多轮；max 3 rounds 预留 | BE | 低 |
| R-LLM-06 | LLM | Tool 失败仍编造数据 | 5 | 3 | 15 | 注入失败集成测试 | 系统 prompt + 空数据 template | AI | 低 |
| R-LLM-07 | LLM | BYOK 无效静默 Mock | 3 | 4 | 12 | E2E 无效 key | `[离线模式]` 强制展示 | FE+BE | 低 |
| R-LLM-08 | LLM | concurrent cold annotate 重复调用 | 4 | 4 | 16 | 100 并发同词 load test | singleflight + pending row | BE | 中 |
| R-MIG-01 | 迁移 | 读切 Score 后 FE/BE 版本不一致 | 4 | 3 | 12 | 字段使用审计 | API version header；同发布 FE+BE | FE+BE | 中 |
| R-MIG-02 | 迁移 | Rollback 丢 Score-only 事件 | 5 | 2 | 10 | staging rollback drill | LegacyCefrJson 30d；rollback 脚本 | DBA | 低 |
| R-MIG-03 | 迁移 | 旧 WordDifficultyAnnotation schema 冲突 | 4 | 5 | 20 | schema diff | 新列+迁移；不原地改 enum 语义 | BE | 中 |
| R-MIG-04 | 迁移 | backfill 锁表 | 3 | 3 | 9 | staging 压测 | 分批；nullable 先行 | DBA | 低 |
| R-MIG-05 | 迁移 | legacy cohort 等级标签剧变 | 5 | 4 | 20 | 迁移后 NPS/工单 | 一次性说明弹窗；old≈new 对照 | PM | 中 |
| R-UX-01 | 体验 | 报告 15s 等待流失 | 4 | 4 | 16 | 漏斗 complete→report viewed | 立即展示 Score；叙事异步 | FE | 中 |
| R-UX-02 | 体验 | 建议 deep link 404 | 3 | 3 | 9 | route 审计 | module enum 服务端校验 | FE+PM | 低 |
| R-UX-03 | 体验 | Personal 条误解为 CEFR | 2 | 4 | 8 | 5 人可用性 | tooltip「对你而言的难度」 | LX | 中 |
| R-UX-04 | 体验 | 叙事推荐模块仍 legacy 逻辑 | 4 | 3 | 12 | FR-8 一致性 smoke | 发布前 grep 审计 CEFR 读路径 | QA | 低 |
| R-UX-05 | 体验 | FR-5 CN 不可用仍显示核实 | 3 | 3 | 9 | geo 测试 | 配置关闭时隐藏按钮 | PM | 低 |
| R-SEC-01 | 安全 | 评价 prompt 注入 | 4 | 3 | 12 | red team 套件 | 字段 sanitize+长度限制 | BE | 低 |
| R-SEC-02 | 安全 | internal API 未鉴权 | 5 | 2 | 10 | 渗透/集成 401 | 启动时缺 key fail fast | BE | 低 |
| R-SEC-03 | 安全 | API Key 进日志/trace | 5 | 2 | 10 | log grep CI | header redaction | DevOps | 低 |
| R-SEC-04 | 安全 | search_web SSRF | 4 | 2 | 8 | 安全 review | query 长度 cap；无任意 URL fetch | BE | 低 |
| R-PERF-01 | 性能 | lookup P95 >3s | 4 | 3 | 12 | load test | cache；async pending | BE | 中 |
| R-PERF-02 | 性能 | 内存 Job 队列丢任务 | 5 | 3 | 15 | chaos restart | **v1 必须 DB 队列** | DevOps | 低 |
| R-PERF-03 | 性能 | daily words 全表扫 | 4 | 3 | 12 | EXPLAIN | intrinsicScore 索引 | BE | 中 |
| R-COST-01 | 成本 | 无 cache annotate 打穿预算 | 5 | 3 | 15 | cost/DAU 仪表盘 | singleflight；annotate-on-lookup only | ARCH | 中 |
| R-COST-02 | 成本 | 报告刷新限流绕过 | 3 | 3 | 9 | 429 测试 | 服务端 idempotency key | BE | 低 |
| R-OPS-01 | 运维 | 指标未上线 | 4 | 4 | 16 | 发布 checklist | OTel 全量 §实现 spec | DevOps | 低 |
| R-OPS-02 | 运维 | Worker 静默失败 | 4 | 3 | 12 | heartbeat | DLQ + job age alert | DevOps | 中 |
| R-LEGAL-01 | 合规 | 阅读内容发第三方 LLM | 3 | 4 | 12 | 法务 review | 隐私政策；最小 payload | PM | 中 |
| R-TC-01 | 测试 | 覆盖率不足 | 5 | 5 | 25 | coverage report | FR 级测试包 §2 | QA | 高→发布前关闭 |

**Top 5**：R-TC-01 (25) · R-MIG-03/R-MIG-05 (20) · R-DI-01/R-DI-07/R-LLM-02/R-LLM-08 (16 簇)

---

## 2. 验证矩阵（摘要）

| 类别 | 单元 | 集成 | 属性 | 人工评测集 | E2E | 生产监控 |
|------|------|------|------|------------|-----|----------|
| Score/Profile | ✅ | ✅ | ✅ min/clamp | — | ✅ | reconciliation |
| Migration | ✅ map | ✅ M1–M3+rollback | — | cohort audit | legacy login | 工单率 |
| FR-1 报告 | ✅ validator | ✅ complete→Ready | — | 100 报告 0 矛盾 | ✅ | generation_ms |
| FR-2 查词 | ✅ Effective | ✅ cache | clamp | 200 context pairs | ✅ | P95, cache_hit |
| FR-3 每日词 | ✅ selector | ✅ API | 80% band | spot check | ✅ | list size |
| FR-4 Coach | ✅ handlers | ✅ 预取+失败 | — | tool fail | — | tool latency |
| FR-5 搜索 | ✅ sanitizer | ✅ low-conf | — | CN on/off | optional | error rate |
| LLM 质量 | ✅ merge | ✅ timeout | — | golden corpus | mock server | parse_rate |

---

## 3. 发布阻断清单（Release Blockers）

| # | 条件 | 负责人 |
|---|------|--------|
| B1 | 所有 FR + F-1~F-5 验收通过 | QA |
| B2 | R-LLM-01：golden 100 报告 0 矛盾 | AI |
| B3 | R-MIG-03 schema 迁移 + rollback drill | BE+DBA |
| B4 | R-MIG-05 迁移文案上线 | PM |
| B5 | R-PERF-02 DB 队列 staging soak ≥72h | DevOps |
| B6 | R-COST-01：soak cost ≤ ¥0.15/DAU/7d | ARCH |
| B7 | R-TC-01：FR 测试包全绿 | QA |
| B8 | CEFR 业务读路径 grep 审计 0 命中（Feature flag 后） | ARCH |

---

## 4. Definition of Done（完整 v1）

### 架构与数据

- [ ] Score 0–100 为唯一决策输入；CEFR 仅投影
- [ ] ScoreProfileService 唯一写者；Overall 读时计算
- [ ] UserWordRelationship 含 knownRate / personalDifficulty
- [ ] WordDifficultyAnnotation append-only + Current 指针
- [ ] Migration Upgrade/Rollback 在 staging prod-size 验证
- [ ] LegacyCefrJson ≥30 天

### 功能

- [ ] FR-1~7 全部验收（见产品 spec §4）
- [ ] F-1~F-5 基础修复验收
- [ ] 新用户 Score-native；legacy cohort 迁移弹窗

### AI 质量

- [ ] FR-2 context 评测 ≥95% 合格（golden 200）
- [ ] FR-1 0% score 矛盾
- [ ] parse failure <2%

### 安全/合规/运维

- [ ] internal API 鉴权；key 脱敏
- [ ] 隐私政策更新
- [ ] OTel 指标 + 告警配置
- [ ] Runbook：迁移、LLM  outage、队列积压、模型升级

---

## 5. AI 团队语境下的发布策略结论

| 策略 | 适用性 |
|------|--------|
| 对用户分阶段发布 FR | **拒绝** — 叙事-行动断裂 |
| 对内 DAG 并行开发 | **采纳** |
| 单一 v1 原子发布 + staging soak | **采纳** |
| legacy / 新注册分 cohort 迁移 | **采纳** — 降 R-MIG-05，不延长双轨读 |
| Feature flag 仅预发 | **采纳** — 不对用户长期半套 |

---

*风险项变更须同步更新本表并在 development-log.md 记录决策。*
