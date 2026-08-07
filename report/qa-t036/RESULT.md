# T-036「我的这个月」月度时间轴 · QA 验收报告（周密）

> 日期：2026-08-07 ｜ 被验：worktree `.worktrees/t-036-monthly-timeline` @ 7e97628（分支 feat/t-036-monthly-timeline，16 文件 +1011/-5）
> 环境：worktree 旁路构建 API Development @5186（已杀）；库 `nextword_qa_t036`（pg_dump 自 `nextword_sim`，验完已 DROP）；前端 vite dev @5198（已杀）；LLM DashScope qwen-plus 真实配置
> 数据基准：`nextword_sim` = 顾言 sim-month「坏了的一个月」原始库（2026-07-01～07-30 仿真 + T-047 探针补丁快照）——**洞察 0 条、画像 1 份（4 条 Finding 全 Questioned）、毕业 0、挑战首过 C1×1（07-28）、定级 A1→B2（07-01）**，验收前已逐表盘点

## 验收结论：**通过（带 1 项 P3 文案保留）**

四条验收标准逐项核验，全部达成；真实数据对账与库内完全一致，空态、窗口过滤、diff 规则、只读与查询数均有实证。

## 逐项证据

### 标准 1：30 天数据用户四段内容与库内一致 — ✅（对账全符，另造数补验两条无真实样本的路径）

小菜（6829d06c）days=30 端点实测（`xiaocai-timeline-d30.json`）vs 库内 SQL 对账：

| 段 | 端点返回 | 库内事实 | 结论 |
|---|---|---|---|
| 分数曲线 | scores/history 25 点（07-08～08-07），三维恒 64 | ProfileScoreSnapshots 32 行，修复前平线 | ✅ 平线属真实历史，前端正常渲染（截图） |
| 里程碑 | challenge_first_pass C1 @07-28 ×1 | 库内窗口内唯一事件（毕业 0 预期；定级/画像生成均 07-01 在窗外） | ✅ 一致 |
| 画像变化 | hasProfile=true、hasComparison=false→（见下）currentFindings=[] | 画像 1 份，4 条 Finding 全 Questioned 被规则过滤 | ✅ 一致（「存疑不参与」是设计决策） |
| 洞察回放 | [] | BottleneckInsights 全库 0 行 | ✅ 一致（空态即真实） |

补充验证：

- **窗口过滤双向正确**：days=40（`xiaocai-timeline-d40.json`）多出 07-01 的 level_change(A1→B2/Initial) 与 profile_generated，排序 desc 正确；
- **画像 diff 与洞察回放无真实样本**（该库是 I7 修复前的「坏月份」），在一次性 QA 库内造数补验：旧画像 4 条转 Verified + 造第二份画像（grammar/vocabulary 弱转强、relevance 仍弱、natural 存疑）+ 造 1 条 MonotonousExpression 洞察 → 端点返回（`xiaocai-timeline-d30-seeded.json`）新强项 2 条、好转弱点 4 条（含「不再上榜」取旧文案）、Questioned 条目全程被过滤、洞察正确回放——**规则逐条手工复核全对**；造数验完即 DELETE，库已 DROP；
- 期间 API 冷启动 Worker 对小菜真实生成画像 Id=3（6 条 Finding，qwen-plus 真实产出），最终态对账（`xiaocai-timeline-final.json`）：hasComparison=true、diff 空、currentFindings 5 条（1 条 Questioned 被过滤）——端点行为始终与库一致。

### 标准 2：新注册空数据用户四段空态 — ✅

- API：新注册 qa.t036.new 用户端点返回全空结构（`newuser-timeline.json`）、scores/history 空数组，无报错；
- 前端（跳过测评口径，DB 置 HasCompletedInitialAssessment）：四段空态文案齐全——「坚持 7 天后出曲线」「还没有里程碑，去完成今天的练习吧」「完成首次测评后，这里会展示你的能力画像」「还没有瓶颈洞察，多练习几天后会自动生成」，无白屏、无 JS 报错（截图 `newuser-profile-1280.png` / `newuser-profile-375.png`）；
- 注：页面有一个 `/api/evaluation/latest` 404，源自既有 `useProfileScores.ts`（非本任务文件），老行为，不计缺陷。

### 标准 3：只读、无 N+1 — ✅

- 代码审查：`MonthlyTimelineService` 固定 6 次查询（毕业 Join Words 单查 / 挑战按级取最早 / 定级 / 画像事件轻投影 / 最新两份画像 Include Findings / 洞察 Take 3），无任何写操作；端点 `RequireAuthorization` + days Clamp(1,365)；
- 单测 `Query_count_stays_bounded_regardless_of_event_volume`（20 毕业词下断言 ≤8 查询，DbCommandInterceptor 实测）复跑：**MonthlyTimelineTests 6/6 通过**（其余 225 未重跑，采信开发自报全量 231+6）。

### 标准 4：npm build + 窄屏不溢出 — ✅

- worktree `npm run build` 通过（✓ built in 482ms，chunk>500kB 警告为既有）；
- Playwright 实测 375px：`documentElement.scrollWidth=375 = innerWidth`，两账号均无横向溢出（4 张截图附 overflow 检测日志）。

## 截图清单（report/qa-t036/）

- `newuser-profile-1280.png` / `newuser-profile-375.png`：新用户四段空态；
- `xiaocai-profile-1280.png` / `xiaocai-profile-375.png`：小菜真实数据（分数平线 + 里程碑 2 条 + 画像变化「暂无变化」+ 洞察空态）——可用作发布素材，但四段仅两段有内容，素材力一般（库情如此）；
- `shoot.mjs`：截图脚本（含溢出与 JS 报错检测）。

## 不足分级

| 级 | 问题 | 说明 |
|---|---|---|
| P3 | 「画像暂无变化」文案在「上一份画像全 Questioned」场景下有误导 | 小菜终态实证：旧画像 4 条全存疑被过滤 → diff 恒空 → 显示「画像暂无变化，继续练习」，实际画像内容已完全更换（首份有效画像）。规则本身符合设计（存疑不参与对比），仅文案口径建议下轮细化（如 hasComparison 但 prev 无有效条目时走「首份有效画像」文案）。不阻塞上线 |
| 观察（非缺陷） | 验收标准 1 预设的「毕业词/洞察/画像对比有内容」在 sim 库无真实样本 | 该库为 I7 修复前的「坏月份」仿真（洞察从未触发、画像从未更新），属库情而非代码缺口；已用一次性库造数补验两条路径，规则全对。建议下轮验收此类功能时先用 I7 修复后的新仿真库 |
| 观察（非缺陷） | 分数趋势 SVG 按点序号等距而非按日期 | 快照有缺日时 X 轴轻微失真；sim 库恒 64 三线完全重叠只显示最上层一条。均不影响口径，记录备查 |

## 收尾确认

- API(5186) / vite(5198) 进程已杀；`nextword_qa_t036` 已 DROP（pg_database 复核 0）；`.token` 已删，报告与日志不含密钥；
- `nextword_sim` 本体全程未动（复核 users=2/profiles=1/insights=0/challenges=4 与验收前一致）；:5108 旧实例未触碰；
- 主仓库工作区零改动，全部验证在 worktree 与一次性库内完成。
