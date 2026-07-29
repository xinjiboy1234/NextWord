# 最终评价 —— 《林晓的七天》Agent 协作演示

> 执行时间：2026-07-28　环境：nextword_demo 独立库 + DashScope qwen-plus（经 :5299 记录代理）
> 评价人：周密（QA）　依据：output/ 下全部快照、数据库留痕与 25 条 LLM 对话原文

## 一、结论

**演示成立。** 五个 Agent/规则角色在一条真实链路上各司其职、全部留痕可核查：
测评收敛 → Profiler 画像 → Verifier 核查 → Planner 计划 → 行为数据积累 →
规则引擎零 LLM 捕获回避信号 → Insight 细读原文定性 → 自动重规划（画像重生成 + Plan 原地重建）。
全程**零代码改动、零数据篡改**（数据库仅 SELECT 观测），25 次 LLM 调用全部经代理留痕。

## 二、逐项核查

| # | 核查点 | 结果 | 证据 |
|---|---|---|---|
| 1 | 测评产出题真实 LLM 四维评分、2 块收敛定级 | ✅ 块表达分 58/56，稳定收敛定 B2（expressionScore=57）；识别全错（0 分）未干扰定级 | api-snapshots/assessment-block-*.json、assessment-final.json |
| 2 | Profiler 画像生成（每测评 1 次 LLM） | ✅ 4 条 Finding，全部 skill 维度 | weakness-profile-1.json、conversations/006-profiler-agent.md |
| 3 | Verifier 机械核查 | ✅ 4/4 Verified，0 存疑；报告 schemaVersion=2 | evaluation-latest.json |
| 4 | Planner 首个计划 | ✅ 主攻 agree_disagree+daily_routine（新用户无场景 Finding，走覆盖率兜底，符合设计）；8 带内词+2 接触词；三处内容源 fromPlan | plan-1.json、timeline Day2-6 背词/阅读记录 |
| 5 | 接触词纪律 | ✅ 每日 10 词中接触词恰 2 个（≤20%）且 isExposure 标记正确 | timeline.json Day2-6 词队列事件 |
| 6 | 回避信号零 LLM 捕获 | ✅ 手动触发筛查 triggered=true，signals=['avoidance']（前 6 句连接词率 ≈1.7/句 → 后 6 句 = 0，腰斩成立） | timeline Day 7、db/sentence-logs.json |
| 7 | Insight Agent 定性 + 证据真实性 | ✅ nature=VocabularyInsufficient；5 条证据 id 经机械比对**全部真实存在且属本人** | insight-1.json vs db/sentence-logs.json |
| 8 | 性质变化 → 自动重规划全链 | ✅ 任务链 EvaluationReport→Planner→BottleneckInsight→planner:replan 全部 Completed；画像重生成（6 条 Finding）；LearningPlans 仍 1 行（原地重建） | db/background-jobs-all.json、db/learning-plans.json |
| 9 | 重规划后 Verified 场景 Finding 驱动 | ✅ 画像2 出现 scenario/agree_disagree weakness（verified），Plan2 sourceFindingIds=[9] 精确消费——**Verified 驱动的个性化路径首次在演示中跑通** | weakness-profile-2.json、plan-2-replanned.json |
| 10 | Plan 内容确实响应 | ✅ 造句目标从 take issue with/counters/up in arms 换成 draw the line/back up/see eye to eye（同为 agree_disagree 场景带内词） | plan-1 vs plan-2 |

## 三、与剧本预期的偏差（如实记录）

1. **洞察性质 ≠ 预期**：规则信号是 avoidance，但 Insight Agent 判为
   **VocabularyInsufficient**——它细读原文后指出「counters 当作要点/连接词、
   up in arms 当可数名词、take issue with 无宾语硬套」。这符合事实（模板句把
   习语当普通词用，qwen 评分也确实低），且恰好展示了**信号只决定「要不要细看」、
   定性权在 Agent** 的设计意图——Agent 没有给信号盖章背书。
2. **背词作答 isCorrect=False**：驱动脚本在 recognition 模式以词形作答（接口按
   释义判对），属脚本作答方式问题，不影响 Agent 链路；SM-2 自评排程仍正常工作。
3. **首版画像无场景维度 Finding**：新用户无学习行为数据（已知限制 T-011），
   Plan1 主攻场景走覆盖率兜底；重规划时已有行为数据，场景维度 Finding 出现并
   被 Planner 消费——两轮对比反而完整演示了「兜底 → Verified 驱动」的演进。
4. **查词 LLM 只发生 1 次**：Day 4 与 Day 2 查到同一篇文章同一词，第二次命中
   文章级缓存（ArticleVocabMappings），属设计的缓存行为。

## 四、成本与性能

- LLM 调用 25 次：造句/测评评分 16、自由表达 5（含测评情境题 2）、画像 2、
  洞察 1、查词 1；**画像与洞察均严格按设计各 1 次/触发，同日幂等零浪费**。
- 总 tokens ≈ 22.8k，LLM 累计耗时 152s；剧本端到端约 3 分钟（不含环境启动）。
- 后台任务从入队到 Completed：Planner 秒级，画像/洞察各 1 次 LLM 调用耗时。

## 五、Agent 价值点（对「演示不到 Agent 作用」的回答)

1. **Verifier 的存在感**：画像不是 LLM 说什么就信什么——核查状态、存疑原因
   全部留痕（本次 4/4、6/6 全 Verified，核查逻辑本身有攻击测试单测覆盖）。
2. **规则引擎看不到的，Agent 看得到**：回避期 6 条简单句语法依然正确、分数
   没崩，传统看板无感；信号筛查 + Insight 细读把「行为变质」翻译成了具体
   瓶颈定性和一句人话结论。
3. **闭环而非报告**：洞察不是终点——性质变化自动重生成画像并重建当日 Plan，
   用户次日拿到的是不一样的词、不一样的造句目标。
4. **一切可审计**：每条 Finding 可回溯证据、每个计划可回溯来源 Finding id、
   每次 LLM 对话有原文——这正是本数据集的内容。
