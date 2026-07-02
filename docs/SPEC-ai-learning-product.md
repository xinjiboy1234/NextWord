# NextWord AI 学习系统 — 产品规格书

> **文档版本**：2026-06-30 v2  
> **状态**：完整 v1 确认稿  
> **上游**：[DESIGN-ai-learning-architecture.md](./DESIGN-ai-learning-architecture.md) · [SPEC-ai-learning-team-deliberation.md](./SPEC-ai-learning-team-deliberation.md)  
> **下游**：[SPEC-ai-learning-implementation.md](./SPEC-ai-learning-implementation.md) · [SPEC-ai-learning-risk-register.md](./SPEC-ai-learning-risk-register.md)

---

## 0. 交付哲学（AI 团队）

本规格 **不以 MVP 或分阶段对外发布** 组织。开发周期对 AI 团队不是约束；约束是 **叙事完整、数据一致、风险可验证**。

- **v1 = 一次原子发布**：用户可见时，Score 内核 + FR-1~7 + 基础修复 F-1~5 同时成立。
- **内部**按依赖 DAG 并行实现（见实现 spec §7），不用「P0 先上线查词」对用户暴露半套系统。
- **风险**由 [SPEC-ai-learning-risk-register.md](./SPEC-ai-learning-risk-register.md) 管控；未关闭 Release Blocker 不发布。

---

## 1. 背景叙事

### 1.1 现状与痛点

NextWord 已有背单词、拼写、造句、阅读、初测、挑战、等级与 LLM 抽象层，但体验仍像 **CEFR 字母驱动** 的传统产品：

- 初测结束只有字母与条形图，**不知为何、如何练**。
- 阅读查词常落 Mock，**与段落无关**。
- 每日新词不读 Profile / 薄弱项。
- AI 能力分散，无统一 Coach 数据面。

### 1.2 升级目标

用户要三件事：**被准确衡量 · 被解释 · 被个性化推送**。

架构锚点（不可违背）：

> **内部只有 Score 和 Profile；CEFR 是外向翻译；AI 感知与解释；规则决定学习后果。**

| 问题 | 负责方 |
|------|--------|
| 几分 / 几级？ | 确定性规则 → Score + CEFR 投影 |
| 为什么？怎么练？ | LLM 读已定 snapshot → 叙事 |
| 今天练什么？ | 规则读 effectiveDifficulty |

### 1.3 行业弯路与反制

| 弯路 | 反制 |
|------|------|
| 一切绑 CEFR | Score + Personal Difficulty |
| 一切 live AI | 标注持久化 + 规则引擎 |
| AI 改定级 | 合并层 server snapshot；无 write tool |

---

## 2. 用户与旅程

### 2.1 目标用户

| 人群 | 诉求 |
|------|------|
| U1 自学者 | 体检报告式诊断 |
| U2 备考者 | 稳定 CEFR 对标 + **可展开依据** |
| U3 坚持者 | 每日任务被安排好 |

### 2.2 v1 完整旅程

```
注册 → 初测(四步,造句LLM) → 立即见 Score → 异步叙事报告
     → 首页(今日词+复习+推荐难度阅读)
     → 阅读查词(上下文释义+confidence)
     → 学习反馈更新 Profile
     → 挑战(真实流程) → 新报告
     → 低置信时可核实(DDG,可关)
```

### 2.3 非目标（v1）

语音陪练 · 自由 Coach 聊天 · 用户自定义 CEFR 阈值 · 全库批量重标 · 多 Agent 编排 · 社交排行

---

## 3. 产品原则

| # | 原则 |
|---|------|
| P1 | Score 唯一精度 |
| P2 | AI 解释，规则决策 |
| P3 | 标注一次，持久化复用 |
| P4 | Personal > Global；**无数据不装精准** |
| P5 | CEFR 可选展示 |
| P6 | LLM 失败可完成旅程（模板/离线） |
| P7 | **叙事推荐必须可点击且后端已 Score 化** |
| P8 | 用户可反馈 AI 错误，但不直接改 Score |

---

## 4. 功能规格

### 4.0 基础修复 F-1 ~ F-5（与 FR 同级）

| ID | 描述 | 验收 |
|----|------|------|
| F-1 | 初测造句接 LLM（含 provisional→final） | schemaVersion 记录；集成测试 |
| F-2 | 阅读 lookupCount FE→BE | E2E 非零惩罚 |
| F-3 | 唯一 ScoreProfileService 写 Profile | 代码审计 |
| F-4 | 挑战/升级 Score 阈值 | 与初测公式族一致 |
| F-5 | 用户可见读路径零 CEFR 业务读 | grep + smoke |

---

### 4.1 FR-1 评价化等级报告

**触发**：初测完成 · 挑战通过/失败 · 手动刷新（1/24h）

**输入**：enqueue 时 **冻结** profileSnapshot + evidence（非 LLM 组装）

**输出**：

```text
【总评】
【优势】×2–3
【薄弱】×2–3
【建议】×3（module 已审计路由）
【依据】首次查看默认展开；含 provisional 标记
```

**UX**：

- complete 后 **立即** 展示 Score 雷达（不等 LLM）
- 叙事异步；8s 内 skeleton；15s P95 Ready
- 失败 → 模板报告（含数字）+ 重试

**验收**：

- [ ] 0% 与 snapshot 矛盾的等级陈述（自动 validator）
- [ ] evidence 与 DB 一致
- [ ] 语言 = ExplanationLanguage

---

### 4.2 FR-2 阅读查词 AI 化

**展示规则（P4）**：

| 状态 | UI |
|------|-----|
| 无 UserWordRelationship 记录 | 仅 intrinsic + 文案「词库难度」 |
| 有记录 | + Personal 条 |
| 标注 pending | `[估算中]` heuristic，样式区分 |
| LLM 不可用 | `[离线模式]` |

**内容**：上下文释义 · confidence · 加入生词本 · 可选例句

**验收**：

- [ ] 释义引用句段
- [ ] cache P95 ≤500ms；miss P95 ≤3s
- [ ] BYOK 无效显式离线

---

### 4.3 FR-3 每日新词 v2

**算法（确定性）**：

```
band = [vocabScore, vocabScore + 12]
pool = effectiveDifficulty ∈ band ∪ weak(knownRate<0.4)
sort: weak > SM-2 due > novelty
count = 10
```

**Cold start**：legacy DifficultyLevel→Score 映射 + 种子词库；空池 copy 说明

**验收**：

- [ ] ≥80% 在 band（有 annotation 时）
- [ ] ≥2 weak（库存有时）
- [ ] 时区按用户 Profile 日界

---

### 4.4 FR-4 Learning Coach 工具层

**v1 形态**：无聊天窗；Evaluation 预取 + ReadingAssistant 复用 handlers

**工具（全部 v1）**：

| 工具 | 用途 |
|------|------|
| get_user_progress | Profile |
| get_weak_words | 薄弱词 |
| get_recent_learning_stats | 7 天统计 |
| get_assessment_history | 测评轨迹 |
| get_review_queue | 复习队列 |
| lookup_word_in_context | 查词 |
| search_web | FR-5 |

**验收**：

- [ ] 报告引用的数据可追溯到 tool payload
- [ ] tool 失败 → 明确错误，不编造

---

### 4.5 FR-5 DuckDuckGo 证据层

**触发**：confidence < 0.6 · 用户点「核实释义」

**约束**：sources 附件 only；**禁止**自动改 intrinsicScore（代码 enforce）

**配置**：`Search:Enabled` 默认 true；CN 部署 false

---

### 4.6 FR-6 挑战流程完整性（**已确认：选项 A**）

**v1 交付**：完整交互式挑战 UI，后端 Score 阈值判定，与初测同一公式族。

| 模块 | 要求 |
|------|------|
| UI | 词汇 / 造句 / 阅读三步真实作答（非自报） |
| 判定 | `ChallengeThresholds`：词汇准确率、WritingScore、ReadingScore |
| 通过 | Profile 各维 +UpgradeDelta → 触发 FR-1 ChallengePass 报告 |
| 失败 | ChallengeFail 报告 + 可重试 |

**禁止**：自报 stub + 触发 challenge 报告（R-UX 信任）

---

### 4.7 FR-7 用户反馈闭环

| 动作 | 效果 |
|------|------|
| 释义有误 | 入 re-annotation 队列；不改 Score |
| 不再推荐此词 | 用户侧 exclude list |
| 标记已掌握 | 提高 knownRate（规则更新，非 LLM） |

---

### 4.8 FR-8 一致性宪章（产品验收）

发布前 smoke：

- [ ] 推荐链接模块无 CEFR-only 选题
- [ ] 等级页数字 = API `/api/profile/scores`
- [ ] 报告 snapshot = 当时 enqueue 的 Profile

---

## 5. 展示规范

**默认**：`Overall 67 · Intermediate · ≈ B1`

**设置**：CEFR 显示开/关 · 解释语言

**等级页**：Score 雷达 → AI 评价 → 依据（首次展开）→ 升级/挑战

---

## 6. 产品决议（原 §10，已锁定）

| 问题 | 决议 |
|------|------|
| Score 刻度 | 统一 0–100 |
| Profile 更新 | 事件驱动 + 日快照 |
| CEFR 配置 | v1 appsettings；UI 仅开关 |
| 评价语言 | ExplanationLanguage |
| 标注版本 | append-only + Current |
| 档位 | 三档 + Score |

---

## 7. 成功指标

| 指标 | 目标 |
|------|------|
| 初测完成率 | +5% |
| 报告阅读完成率 | ≥70% |
| 查词 LLM 成功率 | ≥95% |
| 每日词完成率 | +10% |
| cost/DAU/day | ≤¥0.15 |
| 报告矛盾率 | **0%**（自动） |
| migration 工单率 | <2% legacy cohort |

---

## 9. 需求追溯

| 架构需求 | FR |
|----------|-----|
| 评价化 | FR-1 |
| 工具层 | FR-4 |
| 搜索 | FR-5 |
| 每日词 | FR-3 |
| 阅读 AI | FR-2 |
| 挑战一致 | FR-6 |
| 信任反馈 | FR-7 |

---

*API/表/任务 DAG 见实现 spec；风险见 risk register。*
