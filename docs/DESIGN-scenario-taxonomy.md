# 设计方案：生活表达场景 taxonomy 与内容建设（T-001）

> 状态：已定稿（2026-07-22，顾言）
> 依据：VISION-expression-first.md §2.1 / §4.5 / §6-1
> 后续任务：T-002（dev 实现）、T-003（qa 验证）

## 1. 背景

愿景落地路径第 1 项（P0，所有后续工作的地基）：当前 `Word` 实体无场景字段，「场景 X 词汇弱」级画像连数据源都不存在；种子库仅 6 词，无批量导入机制。本方案定义场景 taxonomy、词级标注模型与选词标准。

## 2. 场景 Taxonomy（两层：7 大类 × 20 子场景）

| 大类 | 子场景 |
|---|---|
| 居家生活 daily_life | 日常起居 daily_routine、下厨饮食 home_cooking、居住与家务 housing_chores |
| 出门在外 getting_around | 问路导航 directions、交通出行 transport、旅行住宿 travel_lodging |
| 消费交易 shopping_money | 购物 shopping、点餐就餐 dining_out、付款与办事 payment_services |
| 社交表达 social | 寒暄闲聊 small_talk、邀约安排 making_plans、求助与致谢 requests_gratitude |
| 情感观点 feelings_opinions | 表达情绪 emotions、表达观点 opinions、同意与反对 agree_disagree |
| 描述叙述 describing_narrating | 描述人事物 describing、讲述经历 past_experiences、计划打算 future_plans |
| 学习与工作（生活化） study_work | 谈论学习 study_talk、日常工作沟通 work_smalltalk |

边界约定：

- 场景指**生活表达场景**，不含医疗/商务等专业领域（VISION §2.1）；
- 「学习与工作」只收生活化话题（聊聊在学什么、上班日常），不收职场专业术语；
- 大类由子场景推出，不做独立标注实体层级之外的冗余标注。

## 3. 词级标注模型（拍板：多标签）

每词标注三个字段：

| 字段 | 取值 | 说明 |
|---|---|---|
| `scenarios` | 0–3 个子场景 | 多对多关联；跨场景高频通用词（be/have/get、连接词、高频动词短语）可标 0 个场景，进 **core 通用桶**，避免强塞场景导致画像失真 |
| `utility` | high / medium / low | 表达效用 = 日常口语使用频率 × 表达不可替代性；low 不入库 |
| `role` | core_verb / connector / scene_noun / phrase_pattern | 表达角色：核心动词、连接过渡、场景名词、句型短语 |

不标注的：

- **难度**：已有 `WordDifficultyAnnotations`，不重复；
- **接触词（exposure_only）**：是规划器按用户水平**运行时**决定的，不是词级静态属性。

## 4. 选词标准（表达效用优先）

- 优先级：① 高频动词与短语动词 → ② 连接/过渡词 → ③ 场景核心短语与句型骨架 → ④ 高频名词形容词；
- **规模目标**：每个子场景 ≥ 60 个有效词（utility=high/medium）+ core 通用桶 ≥ 500 词，总量约 1500–1800；
- **词性约束**：`core_verb` + `connector` 占比 ≥ 40%——防止词库全是「认识词」而非「表达词」。

## 5. 实现方式（给程实的要求）

- LLM 批量标注复用 ReAnnotation worker 模式（`Backend/NextWord.Infrastructure/Services/ReAnnotationWorker.cs`）：后台任务、分批调用、失败可续、幂等可重跑；
- 词表扩充由 LLM 按本方案 §4 标准生成候选词表 → 标注 → 入库；
- 本轮迭代按 AGENTS.md §5 例外：不做 EF 迁移，开发完成后删库重建（Development 启动自动 Migrate + 种子）。

## 6. 验收标准（给周密）

1. taxonomy 与 Word↔场景关联落库，接口可查；
2. 随机抽 100 词人工核对场景标注，准确率 ≥ 90%；
3. 每个子场景 ≥ 60 有效词、core 桶 ≥ 500 词、core_verb+connector 占比 ≥ 40%；
4. 标注任务幂等可重跑、断点可续；
5. `dotnet test` 通过、`npm run build` 不受影响。

## 7. 成本

一次性 LLM 跑批（生成词表 + 标注），日级运行成本为零，符合 VISION §2-6。
