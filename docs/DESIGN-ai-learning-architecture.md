# NextWord AI 学习架构叙事与设计原则

> 文档版本：2026-06-27（2026-07-20 更新状态）  
> 状态：**已实现** — Score 内核 v1 已于 2026-06-30 落地（见 [development-log.md](../development-log.md)），本文 §10 待决问题已全部决议。本文保留为架构决策的「为什么」（ADR 性质）。  
> 用途：记录从「AI 驱动的学习体验需求」到「Score 内核 + CEFR 映射层」的完整来龙去脉。  
> 相关文档：[CURRENT-STATE.md](./CURRENT-STATE.md)（实现现状的权威参考）

---

## 1. 背景：为什么要重新思考

NextWord 已完成 Phase 0–6 的基础交付：背单词、拼写、造句、阅读、初测与挑战、等级体系、LLM 抽象层（`ILLMProvider`）、阅读辅助 Agent 等。系统在**功能覆盖**上已具备「AI 学习应用」的骨架，但在**架构叙事**上仍偏向传统分级产品：

- 业务逻辑大量绑定 **CEFR 离散档位**（A1–C2）
- 测评定级依赖**固定阈值表**，展示机械（五个字母）
- LLM 能力分布不均：造句评分部分接入 OpenAI，阅读查词仍回落 Mock
- Agent 能力（`ReadingAssistantAgent`）为硬编码 intent 分支，缺少统一工具层

产品方向希望升级为 **AI-native 个性化学习系统**。由此引发一系列问题：AI 应该管什么？CEFR 还应该管什么？定级与评价是否冲突？词汇难度是否还需要 CEFR？

本文档记录上述讨论的结论与原则，**不考虑改动成本**，仅描述理想架构应然状态。

---

## 2. 起点：五项 AI 学习体验需求

2026-06-27 提出的增强方向（与现有功能结合后的细化）：

| # | 需求 | 核心诉求 | 与现状差距 |
|---|------|----------|------------|
| 1 | **评价化等级** | 初测/等级页输出段落式评价（优势、薄弱、建议），而非仅 CEFR 字母 | `FinalLevelResult` 只有五维 CEFR；`LevelDashboard` 用静态 `cefrMeta` 占位文案 |
| 2 | **AI 工具层** | AI 可调用工具查看学习状态（进度、复习队列、薄弱词、测评历史等） | 数据已存在于 `UserProgress`、各类 Log，但无统一 tool registry；`ReadingAssistantAgent` 非 function calling |
| 3 | **DuckDuckGo 搜索** | 必要时联网核实释义/用法可信度 | 未集成；Mock 释义无法验证 |
| 4 | **AI 每日新词** | 按当前水平生成/选取每日词单 | `GetDailyWordsAsync` 仅按 `DifficultyLevel` 排序取未学词，不读用户 Profile |
| 5 | **阅读查词 AI 化** | 点击查词返回上下文相关 AI 释义 | `LlmChatClientProvider.GetDefinitionAsync` 仍全量回落 Mock；查词未走用户 LLM 配置 |

### 2.1 关键结论：定级与评价不冲突

五项需求梳理后得出重要分层：

| 层 | 职责 | 负责方 |
|----|------|--------|
| **定级（Grading）** | 产出权威等级/分数结论 | 确定性规则引擎 |
| **评价（Evaluation）** | 产出文字解读、建议、依据 | LLM 读已定结果 + 原始数据 |
| **辅导（Coaching）** | 日常释义、词单、阅读辅助 | LLM + 工具 |

**定级回答：「你现在是几级 / 几分？」**  
**评价回答：「为什么？强在哪、弱在哪、接下来怎么练？」**

二者数据源相同、结论不互相覆盖。即使引入 AI 评价，也不应让 LLM 改写正式定级结果。

这与原 `plans/PLAN-Overview.md`（已完成使命，2026-07 文档整理时删除，见 git 历史）已有原则一致：

> 确定性规则负责学习结果与边界，Agent 负责开放式辅导、工具组合和个性化建议。

---

## 3. 现状快照：当前定级如何工作

（记录讨论时的实现状态，便于对比「应然」与「实然」。）

### 3.1 首次初测

四步测评 → `POST /api/assessment/{id}/complete` → `AssessmentScoringService`：

```
Step 1 词汇识别 → vocabLevel      （12 题，正确率 → CEFR）
Step 2 拼写     → spellingLevel    （10 题，单独记录）
Step 3 造句     → sentenceLevel    （3 题）
Step 4 阅读     → readingLevel     （1 篇 + 1 题）

总体：overall = min(vocab, sentence, reading)   ← 短板定级
拼写不参与 overall
```

CEFR 映射为固定阈值表（例：词汇正确率 ≤9%→A1，≤29%→A2，…）。

### 3.2 已知实现偏差

| 问题 | 说明 |
|------|------|
| 初测造句未用 LLM | `ScoreSentence` 用句子词数启发式（≥6 词→3.5 分），非 `SentenceService` 的 LLM 评分 |
| 阅读粒度过粗 | 仅 1 道题，答对=100%/答错=0% |
| 查词惩罚未生效 | 前端提交 `lookupCount: 0`，阅读降级规则未接入 |
| 题目非自适应 | 从种子词库随机抽题，不按用户水平 |

### 3.3 挑战升级

- `LevelCheckWorker` 每日标记升级候选（`StreakDays ≥ 3` 且 `daysAtLevel ≥ 3`，或近 7 天挑战通过）
- **确认挑战**通过条件：词汇 ≥60%、造句 ≥3.5、阅读 =100%
- 通过后 `OverallLevel` 及三维 CEFR 同步 +1

### 3.4 为什么说「机械」

- 定级 = 阈值表 + 短板 min，无叙事
- AI 未参与定级解释
- CEFR 既是展示标签，又是业务驱动字段（选题、升级、Profile 全绑 CEFR）

---

## 4. 转折点：CEFR 的角色重估

### 4.1 问题来源

若所有内容都交给 AI 实时判断，**CEFR 作为核心分级标准的价值会降低，但不会消失**。

关键洞察：

1. **CEFR 本质是 User Level（能力任务等级），不是 Word Level（词汇标签）**  
   CEFR 定义的是「学习者能完成什么语言任务」（A1 问候、B1 简单新闻、B2 讨论观点…），而非「apple = A1」。

2. **AI 能做的事，CEFR 做不到**  
   同一词对不同用户、不同语篇、不同领域，难度不同。例如 `apple` 对高中生 vs 医疗专业；或「A2 词出现在医学论文里 → 语篇难度 B2」。

3. **很多 AI Learning 产品的弯路**  
   - 弯路 A：一切绑 CEFR 枚举，AI 只打补丁 → 丢失上下文与个性化  
   - 弯路 B：一切实时交给 AI，无持久化与规则边界 → 不一致、不可审计、不可测试

### 4.2 提议：CEFR 降级为「参考坐标系」

**内部核心**：连续 **DifficultyScore（0–100）** + 多维 **User Profile**  
**对外展示**：CEFR 作为 **Mapping（映射）**， nullable、可配置、可替换

```
AI 标注 / 测评采集
        │
        ▼
DifficultyScore (0~100)  ← 唯一业务度量（内核）
        │
   ┌────┼────┐
   ▼    ▼    ▼
学习路径  SM-2  推荐/选题/升级规则
        │
        ▼
DifficultyLevel (Basic / Intermediate / Advanced / Expert)  ← UI 粗分桶（可选）
        │
        ▼
CEFR Mapping (A1–C2, nullable)  ← 展示与互操作
        │
        ▼
AI 叙事评价  ← 解读，不改分数
```

**一句话原则（产品架构锚点）**：

> **NextWord 的内部世界只有 Score 和 Profile；CEFR 是外向翻译；AI 负责感知与解释；规则负责学习后果。**

或更短：

> **AI 是判官，CEFR 是翻译官。**

---

## 5. 理想领域模型（设想阶段）

### 5.1 内容标注：Intrinsic Score

每个可学习单元（词、句、文章）有 **内在难度**，由 AI 首次标注并持久化：

```json
{
  "intrinsicScore": 37,
  "dimensions": {
    "vocabulary": 31,
    "grammar": 44,
    "register": 28
  },
  "category": "Intermediate",
  "confidence": 0.86,
  "reason": "high-frequency everyday noun",
  "estimatedKnownRate": 0.82,
  "sources": []
}
```

AI 可综合：词频、多义、拼写长度、语义抽象度、构词复杂度、语域（口语/书面/专业）、使用场景等——比静态 CEFR 词表更准确。

**持久化实体**（概念名）：`WordDifficultyAnnotation` / `ArticleDifficultyAnnotation`  
字段方向：`DifficultyScore`、`DimensionsJson`、`Reason`、`Confidence`、`ModelProfileId`、`Version`

**不是**每次查词/打开文章都 live 调 LLM。理想态是 **AI 标注 → 持久化 → 业务读缓存**，必要时版本更新。

### 5.2 用户能力：Profile Score

用户不是单一 `B1`，而是多维 Profile：

```json
{
  "vocabulary": 74,
  "reading": 61,
  "writing": 53,
  "spelling": 82,
  "overall": 67
}
```

展示示例：`Intermediate 3` 或 `Overall 67 (≈ B1)`，而非裸 `B1`。

**短板定级**在 Score 空间自然成立：`overall = min(vocab, reading, writing)` 或加权公式——仍是确定性规则，只是度量从 CEFR 换成 Score。

### 5.3 个性化：Personal / Effective Difficulty

仅有全局 `apple → Score 12` 不够。真正驱动推荐的是 **对该用户的有效难度**：

```
EffectiveDifficulty(item, user, context) =
  f(intrinsicScore, userProfile, domain, register, priorExposure, knownRate)
```

需要 **UserItemState**（如 `UserWordRelationship` 扩展）：

- `estimatedKnownRate` (0~1)
- `personalDifficulty` (0~100)
- SM-2 字段（间隔、EaseFactor）——**SM-2 仍由确定性算法驱动，输入可以是 personalDifficulty**

推荐区间示例：`userVocabScore ± margin`（i+1 略难材料）。

### 5.4 测评与造句：多维 AI 分 → 规则汇总

造句 AI 返回（示例）：

```json
{
  "grammar": 82,
  "fluency": 74,
  "naturalness": 69,
  "vocabulary": 55,
  "overall": 72
}
```

阅读 AI 返回（示例）：

```json
{
  "vocabularyDifficulty": 31,
  "grammarDifficulty": 44,
  "readingDifficulty": 39,
  "overall": 38
}
```

**汇总为 Profile Score 更新** → 确定性规则决定是否升级 → **CEFR 由映射函数生成，仅供展示**。

### 5.5 CEFR 映射层

 configurable mapping，例如：

| Score 区间 | CEFR（展示用） |
|------------|----------------|
| 0–20 | A1 |
| 20–35 | A2 |
| 35–50 | B1 |
| 50–70 | B2 |
| 70–85 | C1 |
| 85–100 | C2 |

CEFR 字段：**nullable、可调整、可替换**（未来可增雅思/课标/JLPT 等映射，不改内核）。

### 5.6 外部搜索（DuckDuckGo）的位置

搜索不属于难度体系本身，属于 **置信度与证据层**：

```
AI 标注 → confidence 低 / 用户请求核实 → search_web → 调整 score 或附 sources
```

服务「标注可信度」与「用户信任」，不替代 Score 体系。

---

## 6. 架构分层（理想态）

```
┌─────────────────────────────────────────────────────────┐
│              Perception Layer (AI)                       │
│  词/句/文标注 · 上下文释义 · 造句评分 · 评价叙事 · 搜索核实  │
└────────────────────────────┬────────────────────────────┘
                             │ produces (persisted)
                             ▼
┌─────────────────────────────────────────────────────────┐
│           Canonical Model (Score-based)                  │
│  ContentAnnotation.intrinsicScore + dimensions             │
│  UserProfile multi-dim scores                            │
│  UserItemState knownRate / personalDifficulty / SM-2     │
│  EffectiveDifficulty = f(intrinsic, user, context)       │
└────────────────────────────┬────────────────────────────┘
                             │ consumed by
                             ▼
┌─────────────────────────────────────────────────────────┐
│        Deterministic Learning Engine                     │
│  推荐 · 每日词单 · 阅读匹配 · 复习队列 · 测评汇总 · 升级  │
└────────────────────────────┬────────────────────────────┘
                             │ projected to
                             ▼
┌─────────────────────────────────────────────────────────┐
│       Presentation & Interop Layer                       │
│  DifficultyLevel buckets · CEFR (mapped, nullable)       │
│  Narrative evaluation · milestone labels                 │
└─────────────────────────────────────────────────────────┘
```

### 6.1 各层职责边界

| 层 | 做什么 | 不做什么 |
|----|--------|----------|
| **AI Perception** | 标注、释义、评分、评价、搜索证据 | 直接改写升级/回退/SM-2 间隔 |
| **Score Model** | 存储与计算有效难度、Profile | UI 展示格式 |
| **Rule Engine** | 复习、选题、测评汇总、升级条件 | 自然语言解释 |
| **Presentation** | CEFR、档位、评价文案 | 业务决策 |

### 6.2 Learning Coach Agent 与工具

统一 Agent 入口，LLM 通过 **function calling** 调用工具（设想）：

| 工具 | 用途 |
|------|------|
| `get_user_progress` | Profile Score、连续天数 |
| `get_recent_learning_stats` | 近 N 天各模块表现 |
| `get_review_queue` | 待复习词 |
| `get_assessment_history` | 测评/挑战轨迹 |
| `get_weak_words` | 薄弱项 |
| `get_reading_stats` | 查词率等 |
| `search_web` | DuckDuckGo 核实 |
| `lookup_word_in_context` | 阅读查词 |

Agent 服务 **评价生成、每日词、阅读辅导**；定级/升级仍读 Rule Engine 结果。

---

## 7. 讨论中达成的共识

### 7.1 已确认

1. **定级与评价分离**——不冲突，应并行建设  
2. **CEFR 从「核心业务字段」降为「映射与展示层」**——方向正确  
3. **DifficultyScore 作为内部唯一精度**——优于离散 CEFR 驱动算法  
4. **多维 User Profile**——优于单一 Overall CEFR  
5. **AI 判官 + 规则引擎 + CEFR 翻译官**——职责清晰  
6. **CEFR 仍有生态价值**——用户认知、教材对齐、第三方词库、营销表述  

### 7.2 讨论中收紧的原则（避免新弯路）

| 原设想 | 收紧后 |
|--------|--------|
| 所有内容实时 AI 判断 | **AI 标注 + 持久化**；业务读 Score，非全链路 live |
| 仅全局 Word Score | 增加 **Personal / Effective Difficulty** |
| 四级/L1–L4 为核心 | **Score 是核心**；档位仅为 UI 投影 |
| AI 唯一判官 | AI 判表现与难度；**规则判学习后果**（升级/复习） |
| CEFR 完全废弃 | **保留为 nullable 映射**，可扩展其他标准 |

### 7.3 与最初五项需求的关系

| 原需求 | 在 Score 架构下的位置 |
|--------|----------------------|
| 评价化等级 | Presentation 层：读 Profile + 测评数据 → AI 叙事 |
| AI 工具 | Perception 层：Agent 调工具取 Score/Profile/历史 |
| DuckDuckGo | 证据层：提高标注与释义 confidence |
| AI 每日新词 | Rule Engine 输入：`userProfile ± margin`，非 CEFR 查表 |
| 阅读查词 AI | Perception 层：`lookup_word_in_context` + intrinsic/personal score |

五项需求不是 CEFR 体系上的补丁，而是 **Score + Profile 架构的自然延伸**。

---

## 8. CEFR 保留价值总结

| 价值 | 说明 |
|------|------|
| 用户认知 | 「B1」比「Difficulty 58」更易理解 |
| 教材/考试对齐 | 雅思、剑桥、国内课标等常引用 CEFR |
| 第三方数据 | 公开词库、阅读材料常带 CEFR 标签，便于导入 |
| 营销 | 「推荐给 B1 学习者」比内部分数更易传播 |

**定位**：对外翻译与互操作，不是内部算法依据。

---

## 9. 与原 PLAN-Overview 的关系（历史记录）

> 注：`plans/PLAN-Overview.md` 已于 2026-07 文档整理时删除（全 Phase 完成，见 git 历史）；本节建议的原则更新已由 Score 内核 v1 实现，不再需要回写。

`PLAN-Overview.md` 当时写法：

- 分级体系：`basic/intermediate/advanced × CEFR A1-C2`
- Agent 原则：确定性规则 vs Agent 辅导（已对齐）

**建议后续更新 PLAN-Overview 的原则段为**：

> - **内核**：DifficultyScore (0–100) + User Profile 多维分数  
> - **算法**：SM-2、推荐、升级、测评汇总均读 Score  
> - **展示**：DifficultyLevel 粗分桶 + CEFR 映射（nullable）+ AI 评价叙事  
> - **AI**：标注、解释、辅导、搜索核实；不绕过规则引擎做升级/回退  
> - **Agent**：工具化读取 Profile/Score/历史，非硬编码 intent  

本文档为上述更新的**叙事依据**，具体 API/表结构变更留待独立 implementation spec。

---

## 10. 待决问题（已全部决议）

> 注：以下问题已在后续产品规格中锁定（统一 0–100 刻度、事件驱动 + 日快照、CEFR 映射可开关、评价默认中文、标注持久化 + 重标 worker、三档 + Score），并由 Score 内核 v1 实现。

以下在进入 spec 前需产品确认：

1. **Score 粒度**：全局 0–100 统一刻度，还是词汇/阅读/写作分空间？  
2. **Profile 更新频率**：每次学习提交增量更新 vs 每日批处理？  
3. **映射配置**：CEFR 阈值是否用户可见/可切换（如「显示雅思对标」）？  
4. **评价语言**：中文默认 vs 跟随 `ExplanationLanguage`？  
5. **标注版本策略**：模型升级后是否批量重标？如何保留历史版本？  
6. **Expert 档位**：是否需要第四档，还是三档 + Score 足够？

---

## 11. 文档 lineage（来龙去脉索引）

| 阶段 | 内容 | 结论 |
|------|------|------|
| **2026-06-27 需求提出** | 评价化、AI 工具、搜索、每日词、阅读 AI | 细化 FR-1~5，识别现状差距 |
| **定级机制梳理** | `AssessmentScoringService`、挑战升级 | 记录「机械」根因：阈值表 + 无叙事 |
| **定级 vs 评价** | 是否冲突 | **不冲突**；定级=规则，评价=AI 解读 |
| **CEFR 重估** | 用户架构提案 | CEFR 降为映射；Score 为内核 |
| **架构评审** | 不考虑改动成本 | 方向成立；收紧实时 AI、Personal Score、规则边界 |
| **本文档** | 叙事归档 | 供 PLAN/spec 引用 |

---

## 12. 下一步（已完成闭环）

> 注：以下事项均已在 2026-06-30 的 Score 内核 v1 落地中完成或决议，本节保留作历史记录。

1. 产品确认 §10 待决问题（已决议）  
2. 撰写 implementation spec（数据模型、API 字段、迁移阶段）（已完成并落地，spec 于 2026-07 文档整理时归档删除）  
3. 更新分级原则段（已由 Score 内核 v1 实现，PLAN-Overview 已删除）  
4. 按 P0→P4 拆分：阅读查词真实 LLM → 评价报告 → 工具层 → 每日词 → 搜索（已落地）  

---

*本文档仅记录架构叙事与原则，不包含数据库 Migration 或代码变更。*
