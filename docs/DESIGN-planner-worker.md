# 设计方案：PlannerWorker + 每日内容来源切换（T-006）

> 状态：已定稿（2026-07-24，顾言）
> 依据：VISION-expression-first.md §5 ③④ / §5.2 / §6-4
> 前置：T-002 场景词库、T-004 表达力定级、T-005 已验证画像；T-010 画像去重随本任务一并修复

## 1. 定位

愿景的「用户可感知的变化」：夜间 Planner Agent 根据已验证画像排出未来 N 日的学习计划，每日内容（背词队列、阅读推荐、造句目标）从「难度带一刀切」切换为「执行 Plan」。背词选词权上交规划器（VISION §5.2-1）。

## 2. LearningPlan 结构（顾言拍板）

每份 Plan 覆盖未来 **7 日**，要素：

| 要素 | 说明 |
|---|---|
| 主攻场景 | 1–2 个子场景（来自已验证 Finding 的 weakness 维度；画像不足时按场景词覆盖率低者兜底） |
| 每日词队列 | 从主攻场景 + core 桶选词：水平带内词为主（utility=high/medium），允许掺 ≤20% 超带「接触词」（只要求认识） |
| 阅读推荐 | 按主攻场景选文（TopicTag/场景匹配 + 难度就近） |
| 造句目标 | 每日目标词（水平带内、主攻场景优先），供 SentenceStudio 出题 |
| 生成依据 | 引用的 Finding id 列表（可追溯为什么这样排） |

## 3. 运行机制

1. **PlannerWorker**：`BackgroundJobWorker` 新任务类型，夜间一日一次（成本符合 VISION §2-6）；输入 = 最新 WeaknessProfile（**只消费 Verified 条目，存疑不进规划**）+ 用户水平带 + 词库标注；输出 = LearningPlan 持久化；
2. **内容来源切换**：`DailyWordSelectionService` / 阅读推荐 / SentenceStudio 出题**优先执行当日 Plan**；无 Plan、Plan 过期（>7 天）或 Plan 生成失败 → 回退现有难度带逻辑（用户永远有内容可学）；
3. **接触词纪律**：接触词只进背词识别队列；产出任务与测评选词限水平带内（测评 T-004 已按带过滤，天然排除；产出任务本任务补齐带内约束）。

## 4. T-010 画像去重（随本任务修复）

- Profiler 提示词明确要求「每个维度至多一条 Finding、不跨 Finding 复用同一证据」；
- Profiler 后处理去重：同维度保留证据更强者；证据引用被多条复用时保留置信度最高者；
- Verifier 不变（语义去重是 Profiler 职责，核查职责不膨胀）。

## 5. 验收标准（给周密）

1. PlannerWorker 夜间任务可运行：有画像用户次日有 Plan，Plan 的主攻场景来自 Verified weakness Finding；
2. 存疑 Finding 不出现在任何 Plan 的生成依据中；
3. 每日词队列优先执行 Plan；无 Plan 用户回退难度带逻辑不受影响；
4. 队列中接触词占比 ≤20% 且全部为超带词；产出/测评选词不含超带词；
5. Plan 过期后自动回退；重复触发幂等（同日不重复生成）；
6. T-010：同一画像不再出现同维度重复 Finding、证据不被多条 Finding 复用；
7. `dotnet test`、`npm run build` 通过。

## 6. 非目标

- 瓶颈性质洞察与重规划触发（T-007，紧随其后）；
- 词毕业四阶段的完整生命周期改造（VISION §5.2-2，后续迭代）；
- 用户手动调整 Plan 的 UI。
