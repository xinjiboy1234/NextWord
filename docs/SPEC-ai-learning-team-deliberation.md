# NextWord AI 学习系统 — 多 Agent 评审与收敛纪要

> **文档版本**：2026-06-30 v2  
> **用途**：记录 RA / ARCH / AI-ENG / QA-RISK 四方互评、分歧与最终一致结论  
> **原则**：Agent 必须被证据和约束说服，不得死磕立场；收敛后写入产品 spec 与实现 spec

---

## 1. 团队设定与讨论规则

| Agent | 角色 | 评审立场 |
|-------|------|----------|
| **RA** | 需求分析 | 用户信任、叙事-行动一致、边界场景 |
| **ARCH** | 系统架构 | 单一事实源、写路径、迁移、幂等 |
| **AI-ENG** | LLM 工程 | 可靠性、成本、结构化输出、测试分层 |
| **QA-RISK** | 质量与风险 | 风险登记、验证矩阵、发布阻断项 |
| **PM**（主持） | 产品 | 协调收敛，无否决权 |

**讨论规则**

1. 任何提案必须标注架构层（Perception / Score / Rule / Presentation）及是否改写定级。
2. 提出反对时必须附带：失败场景 + 可接受替代方案。
3. 三轮仍无法收敛 → PM 召集「证据投票」：ARCH 对数据完整性、RA 对用户信任、AI-ENG 对 LLM 边界各有一票加权。
4. **本团队为 AI 开发团队**：不以「周/迭代/MVP 对外发布」组织交付；以 **完整 v1 原子发布 + 内部并行工作流 + 详尽风险管控** 组织。

---

## 2. 第一轮：对 v1 初稿的核心批评

### 2.1 RA 对「P0–P3 分阶段发布」的否决

**论点**：分阶段对外发布在 AI 团队语境下是错误的优化轴——代码可以并行写，但 **用户故事不能拆开**。

| 阶段矛盾 | 后果 |
|----------|------|
| P0 查词展示 Personal Difficulty，P2 才写 UserWordState | **虚假精度**，比不展示更伤信任 |
| P1 评价报告推荐「复习薄弱词」，P2 才有 review 工具 | 叙事与行动断裂 |
| P1 演示脚本含「今日词单」，FR-3 在 P2 | 对外演示即谎言 |
| P1 报告依赖 ToolRegistry，工具层在 P2 | 双份数据获取或违反 FR-4 验收 |

**RA 结论**：FR-1~5 + 基础修复 **必须同一 v1 _cut 对外可见**；阶段仅用于 **内部工作流依赖排序**，不是用户发布节奏。

### 2.2 ARCH 对「双写 + 分散读切换」的批评

**论点**：方向对，执行规格不足。

- ~20 处仍读 `CefrLevel` / `OverallLevel`，M3 读切换未与任何交付边界绑定 → **split-brain 是默认态**。
- 无 `ProfileWriter` 单一写入口 → 双写地狱。
- 新造 `UserWordState` 与现有 `UserWordRelationship` 并行 → 实体碰撞。
- M2 backfill 全维同赋 CEFR 中心值 → 抹平真实短板。

**ARCH 结论**：M1 扩展 `UserProgress` + `UserWordRelationship`；立即双写；**同一 v1 发布内** 所有用户可见读路径切 Score；backfill 保留维度不确定性（null 优于假精度）。

### 2.3 AI-ENG 对「Prompt 即安全边界」的批评

**论点**：评价报告「禁止改分」写进 prompt 不够；须在 **合并层** 强制 server snapshot 覆盖 LLM 输出。

- `ExtractJson`  brace-slicing 静默失败率高 → 须 schema 约束或严格 validator。
- 每次 cache miss 同步 annotate 会打穿 ¥0.15/DAU 成本目标。
- ToolRegistry 够用；Evaluation 应用 **C# 预取工具 + 单次 LLM**，非多轮 Agent 循环。

### 2.4 QA-RISK 对「测试与风险厚度」的批评

**论点**：当前 ~12 个后端测试无法支撑 Score 内核迁移；需 30 项风险登记 + FR 级验证包 + 发布阻断清单。

**与 RA 的分歧**：QA 初稿倾向「分阶段上线降 blast radius」。

---

## 3. 第二轮：关键分歧辩论与收敛

### 辩论 A — 一次性发布 vs 分阶段发布

| 立场 | 代表 | 论据 |
|------|------|------|
| **一次性 v1** | RA, ARCH, PM | AI 团队开发周期无意义；部分功能上线 = 产品叙事谎言；整合测试应针对完整闭环 |
| **分阶段降风险** | QA-RISK | blast radius、迁移+LLM 同日落点难排障 |

**收敛（PM 主持，证据投票 3:1）**

- **对外**：**单一 v1 原子发布**（Feature Flag 仅用于预发/内测，不对用户长期暴露半套系统）。
- **对内**：并行工作流按 **依赖 DAG** 排序（见实现 spec §7），非按「可演示 slice」排序。
- **风险补偿**：staging 全量 soak ≥72h；新用户 Score-native 与 legacy 迁移 **分 cohort 开关**（QA 妥协：降低 R-UX-01，不延长双轨读路径）。
- **回滚**：DB rollback script + `LegacyCefrJson` 30 天；非「回退到 P0 产品态」。

### 辩论 B — Personal Difficulty 何时展示

| 立场 | 代表 | 论据 |
|------|------|------|
| P0 可先展示 heuristic | ARCH（初稿） | 查词可先上线 |
| 无真实 state 则禁止展示 | RA, LX | 虚假精度 |

**收敛**：UI 规则写死——

- `UserWordRelationship.EstimatedKnownRate` 无学习记录 → **隐藏 Personal 条**，仅展示 intrinsic（标注「词库难度」）。
- 有记录 → 展示 Personal 条。
- heuristic 仅作 pending 态，须标注 `[估算中]`，不得与 Profile 数值样式相同。

### 辩论 C — 初测造句 LLM 是否阻塞 complete

| 立场 | 代表 | 论据 |
|------|------|------|
| 同步 LLM 阻塞 complete | ARCH 初稿 | 衡量准确 |
| 异步 provisional + 报告等待 | AI-ENG | P95 延迟；complete 应 ≤5s |

**收敛**：

1. `complete` 同步返回：词汇/拼写/阅读确定性分数 + 造句 **启发式 provisional**。
2. 异步 Job：造句 LLM 评分 → 更新 `WritingScore` → 若 overall 变化则 **追加 LearningEvent**（不 retro 改已展示 complete 结果，除非 delta ≥5 分则推送通知）。
3. 评价报告 Job **等待** 造句 LLM 完成或超时 30s 后带 `provisional: true` 证据生成。

### 辩论 D — Evaluation 用 Tool 循环还是 C# 预取

| 立场 | 代表 | 论据 |
|------|------|------|
| ToolRegistry 多轮 | PM 初稿 | 「Coach 智能化」 |
| C# 预取 + 单轮 LLM | AI-ENG, ARCH | 更快、可测、无 tool 幻觉 |

**收敛**：

- **EvaluationReport**：`EvaluationDataAssembler`（C#）顺序调用 tool handlers → 组装 payload → **一次** structured LLM 调用。
- **ReadingAssistant**：P2 起复用同一 handlers；仍为固定 intent，非自由 chat。
- ToolRegistry 保留：统一 handler 注册与测试；CoachRunLoop（max 3 rounds）**仅**留给未来 chat，v1 不启用。

### 辩论 E — DuckDuckGo 是否 v1 必须

| 立场 | 代表 | 论据 |
|------|------|------|
| P3 可省 | ARCH | 非内核 |
| v1 必须 | RA | 无核实机制则 AI 释义信任不足 |

**收敛**：

- **v1 包含 FR-5**，但 **CN 环境默认关闭**。
- v1 必做：**confidence 展示 + 「标注置信度较低」文案 + 反馈入口（FR-7）**。
- search_web  handler **代码级禁止** 调用 annotation upsert（ARCH 硬约束）。

### 辩论 F — UserWordState 新表 vs 扩展 Relationship

**收敛（ARCH 全胜，BE 无异议）**：扩展 `UserWordRelationship` 增加 `EstimatedKnownRate`、`PersonalDifficulty`；不新建平行表。

---

## 4. 第三轮：一致通过的 v1 范围

### 4.1 基础修复（与 FR 同级，非「预备阶段」）

| ID | 项 | 验收 |
|----|-----|------|
| F-1 | 初测 Step3 造句接 LLM（含 provisional 流程） | 集成测试 |
| F-2 | 阅读 `lookupCount` FE→BE 全链路 | E2E |
| F-3 | `ScoreProfileService` 唯一写 Profile | 静态分析 + 集成 |
| F-4 | 挑战/升级规则 Score 空间重写 | 与初测同一公式族 |
| F-5 | 所有用户可见读路径切 Score（R1–R3 同发布） | grep 审计 + flag |

### 4.2 功能包（FR-1 ~ FR-7）

| FR | 名称 | v1 |
|----|------|-----|
| FR-1 | 评价化等级报告 | ✅ 全触发 + 首次强制展开依据 |
| FR-2 | 阅读查词 AI 化 | ✅ 含 confidence / pending UX |
| FR-3 | 每日新词 v2 | ✅ 含 cold-start fallback |
| FR-4 | Coach 工具层 | ✅ 全工具集含 get_review_queue |
| FR-5 | DuckDuckGo 证据 | ✅ 可配置关闭 |
| FR-6 | 挑战流程完整性 | ✅ **选项 A**：完整交互式挑战 UI + Score 阈值（2026-06-30 确认） |
| FR-7 | 用户反馈闭环 | ✅ 释义有误 / 不再推荐 → 队列，不改 Score |

### 4.3 明确不做（v1）

- 自由对话 Coach 窗口
- 用户自定义 CEFR 阈值
- Agent Framework / 多 Agent 编排
- 全库离线批量重标注（仅按需 + 频率队列 + 文章 Top-N 预热）
- 语音陪练

---

## 5. 一致通过的设计约束（写入实现 spec 的「宪法」）

```text
1. Score 是唯一业务精度；CEFR/bucket 仅为 IScoreMappingService 投影。
2. 只有 ScoreProfileService.ApplyUpdate() 可写 Profile 分数。
3. OverallScore 在读取时计算：min(vocab, reading, writing)；不持久化或双写。
4. LLM 输出不得包含可写入 Profile 的字段；Evaluation 合并层 server-side 覆盖。
5. search_web 不得 mutate intrinsicScore。
6. Annotation 采用 append-only 版本；业务读 Current 指针。
7. 每个 LLM 持久化 JSON 含 schemaVersion。
8. Evaluation/Annotation Job 必须 DB 队列 + 幂等键。
9. 无 UserWordRelationship 交互记录时，UI 不得展示 Personal Difficulty 为确定值。
10. 推荐模块 deep link 发布前必须 route 审计通过。
```

---

## 6. 残余分歧与监控项

| 项 | 少数意见 | 处理 |
|----|----------|------|
| 造句 provisional 是否向用户明示 | LX 希望隐藏 | 报告 evidence 折叠显示 `writingScoreProvisional` |
| 新用户 big-bang vs legacy 分 cohort | QA 倾向 cohort | **采纳**：`ScoreNativeRegistration` flag |
| Expert 第四档 | PM 曾提 | **否决**，三档 + Score 足够 |
| SSE 流式报告 | FE 偏好 | **v1 轮询**；SSE 列 v1.1 |

---

## 7. 文档索引

| 文档 | 内容 |
|------|------|
| [SPEC-ai-learning-product.md](./SPEC-ai-learning-product.md) | 背景、完整 v1 功能、验收 |
| [SPEC-ai-learning-implementation.md](./SPEC-ai-learning-implementation.md) | 领域模型、API、工作流 DAG、任务清单 |
| [SPEC-ai-learning-risk-register.md](./SPEC-ai-learning-risk-register.md) | 30+ 风险、验证矩阵、发布阻断 |

---

*本纪要替代初版 spec 中的「P0–P3 用户发布计划」与「MVP 铁律」。*
