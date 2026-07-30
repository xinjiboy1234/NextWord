# T-033 验收证据（周密，2026-07-31）

- 对象：worktree `.worktrees/t-033-insight-signals`，分支 feat/t-033-insight-signals，commit a593c42
- 环境：API :5196（Development）+ 独立库 nextword_qa_t033（已 DROP）+ DashScope qwen-plus 真实 LLM
- 仿真器：report/sim-month/sim.py 复制件（report/qa-t033/sim/sim.py，仅改库名指向 QA 库），30 天完整跑完

## 仿真洞察触发（验收标准 1）
- 第 10 天：{"triggered": true, "nature": "GrammarErrors", "statement": "未来计划场景中频繁出现时态错误，如用一般现在时表达将来意图，且过去时、主谓一致、冠词等基础语法错误密集出现。", "replanTriggered": true, "signals": "plateau"}
- 第 17 天：{"triggered": false}
- 第 24 天：{"triggered": true, "nature": "GrammarErrors", "statement": "时态、主谓一致、介词、不定式标记等基础语法错误高频出现，如 'He drop', 'I be worth it', 'go out play football', 'arrive Beijing', 'she have three company'。", "replanTriggered": false, "signals": "safe_word"}

## 分数曲线（联动红利：T-022 分数回写）

| 日期 | Writing | Overall | CEFR 显示 |
|---|---|---|---|
| 2026-07-02 | 72 | 72 | B2 |
| 2026-07-03 | 75 | 72 | B2 |
| 2026-07-04 | 71 | 71 | B2 |
| 2026-07-05 | 71 | 71 | B2 |
| 2026-07-06 | 74 | 72 | B2 |
| 2026-07-07 | 76 | 72 | B2 |
| 2026-07-08 | 69 | 69 | B1 |
| 2026-07-09 | 69 | 69 | B1 |
| 2026-07-10 | 71 | 71 | B2 |
| 2026-07-11 | 66 | 66 | B1 |
| 2026-07-12 | 64 | 64 | B1 |
| 2026-07-13 | 66 | 66 | B1 |
| 2026-07-14 | 66 | 66 | B1 |
| 2026-07-15 | 70 | 70 | B2 |
| 2026-07-16 | 71 | 71 | B2 |
| 2026-07-17 | 70 | 70 | B2 |
| 2026-07-18 | 68 | 68 | B1 |
| 2026-07-19 | 71 | 71 | B2 |
| 2026-07-20 | 74 | 72 | B2 |
| 2026-07-21 | 76 | 72 | B2 |
| 2026-07-22 | 76 | 72 | B2 |
| 2026-07-23 | 76 | 72 | B2 |
| 2026-07-24 | 76 | 72 | B2 |
| 2026-07-25 | 76 | 72 | B2 |
| 2026-07-26 | 70 | 70 | B2 |
| 2026-07-27 | 74 | 72 | B2 |
| 2026-07-28 | 71 | 71 | B2 |
| 2026-07-29 | 74 | 72 | B2 |
| 2026-07-30 | 71 | 71 | B2 |
| 2026-07-31 | 73 | 72 | B2 |

## 评分分布（联动红利：T-027 评分收紧——注意 T-027 不在本分支）

- 造句 81 条等级分布：{'A': 25, 'C': 33, 'B': 20, 'D': 3}；其中菜鸟句拿 A 25 次、四维满分 17 次（anomalies.log）
- 自由表达 12 篇等级分布：{'C': 5, 'D': 7}

## 生命周期（T-034 未做，预期仍低）

- {"lifecycleDist": {"Recognized": 245}, "dueReviews": 222, "masteryDist": {"25": 245}, "totalRelationships": 245}；PromptedUse 0、毕业 0

## 其他

- 定级：综合 72 → B2（T-023 新分带 B1 35–70／B2 70–85，72→B2 口径正确；边界 70）
- 等级历史：仅 Initial A1→B2 一条；challenge 见 final-state.json
- 林晓回归：report/qa-t033/linxiao-regression.py，signals=["avoidance"]、nature=AvoidancePattern、replan=true
