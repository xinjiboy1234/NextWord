# NextWord 开发日志

按时间倒序记录需求、决策、实现与验收。

## 2026-08-07 — I7 T-044：毕业判定口径放宽——整篇 C 及以上且词汇维 ≥3 即达标（程实）

### 需求
- qa-t039 终验 P1：毕业判定要求整篇自由表达 A/B 才执行池词自发使用检查（`FreeExpressionService` 评级闸），菜鸟人设 12 篇全 C/D → 判定代码一次都没执行到，毕业 0（T-034 同一锚点连续两轮不过）。
- 顾言裁定口径：**整篇 C 及以上且词汇维 ≥3 即达标**（整篇平庸不连坐单词）；D 档或词汇维 ≤2 仍不毕业（防烂底线不动）；造句确认门槛（A/B）与 T-040 命中口径不动。

### 实现
- `WordLifecycleService.IsGraduationGrade`：毕业专用整篇评分口径 A/B/C（`IsPassingGrade` 仍为 A/B，造句确认链路不动）。
- `FreeExpressionService.GraduatedSpontaneousUseAsync`：闸门改为 `IsGraduationGrade(OverallGrade) && VocabularyScore ≥ 3`；词汇维取当次 LLM 评分的 `VocabularyScore`（FreeExpressionLog 不落四维分，按参数传入）。
- 文档同步：`docs/DESIGN-word-lifecycle.md` §2 毕业流转条件、`docs/CURRENT-STATE.md` §5.1/§5.17 毕业口径。

### 验证
- `WordLifecycleTests.Free_expression_graduates_spontaneous_use_with_trace` 按新口径重写（注明口径变化）：A 档毕业回归；未出现的词不毕业；**C 档词汇维 4 含池词 → 毕业留痕 graduatedWords**；D 档含池词 → 不毕业；C 档词汇维 2 → 不毕业。
- B 档回归由 `Free_expression_with_phrase_graduates_spontaneous_use`（短语 + B 档 → 毕业）覆盖。
- `dotnet test` 全绿（见 T-045 一并收尾的基线数）。

## 2026-08-06 — I7 T-038：cefrDisplay 下行迟滞——上行即时、降档需连续 3 天低于下限（程实）

### 需求
- 周密 T-033 仿真观察：Overall 在分带边界（B1/B2 边界 70）附近波动时展示档 30 天跳 6 次，用户看到的是噪音。顾言口径：**上行即时、下行迟滞**。

### 决策
- 迟滞落在 `ScoreProfileService.ApplyUpdateAsync`（写入唯一入口），`SyncLegacyLevels` 算出 raw 展示档后再按迟滞规则修正 `progress.CefrDisplay`；`OverallLevel`、`DifficultyBucket` 与分数本身不动（只影响展示层）。
- 规则：raw 档高于当前展示档 → 立即升档；raw 档低于当前展示档 → 需当前 Overall 与近 3 天 `ProfileScoreSnapshots` 的 Overall 全部低于当前展示档下限才降，快照不足 3 天不降（`IScoreMappingService.GetCefrBand` 取档下限，分带单一来源）。
- 测评定级写入不受迟滞约束（首测/复测定级是权威锚点，含 T-042 矫正传导的下调）：`ProfileUpdateCommand` 加 `BypassCefrDisplayHysteresis`，仅 `AssessmentService` 完成定级时置 true；挑战通过、T-022 小步回写走迟滞。

### 实现
- `IScoreMappingService.GetCefrBand(label)`（ScoreMappingService 实现，按标签取 CefrBands 分带定义）。
- `ScoreProfileService`：`ApplyCefrDisplayHysteresisAsync` + 快照 Overall 解析（ScoresJson 只读 `overall` 字段，解析失败视为不满足「低于下限」）。
- `AssessmentService` 定级写入置 `BypassCefrDisplayHysteresis: true`。

### 验证
- 新增 `CefrDisplayHysteresisTests` 5 例：边界上跳即时（69→70 立升 B2）；单日下探不降（3 天快照有一天在带内）；连续 3 天低于下限才降；快照不足 3 天不降；测评写入不受迟滞（无快照也立即降）；并断言迟滞期间 OverallLevel 仍按 raw 映射（只影响展示层）。
- `dotnet test`：216 单元 + 6 集成全绿（较 T-037 提交净增 5）。

## 2026-08-06 — I7 T-037：自由表达评分不再把字面量 free expression 当目标词（程实）

### 需求
- 周密实测：自由表达评分把字面量 `free expression` 当 targetWord 传给 LLM，qwen-plus 当成「写作主题」，高质量段落被判 off-topic 拿 C（aiScore 80 判 C）。

### 决策
- 自由表达请求改传中性主题描述（`TargetWord="日常自由表达"` / `Scene="daily-life"`）+ `SentenceRatingRequest.IsFreeExpression` 标记（可选参数，造句/测评链路签名不变）。
- prompt 出自由表达专门变体（`BuildFreeExpressionRatingPrompt`）：无 Target Word 行，明确「没有指定主题词、不因未用任何特定词扣分」，相关性维度评「内容是否围绕日常场景/主题连贯展开、言之有物」；T-027 挑战度规则抽成共享常量，造句/自由表达两变体共用。
- Mock 同步：自由表达按 `IsFreeExpression` 标记判定（保留字面量回退兼容存量调用），相关性/词汇维不扣「未用目标词」。
- 测评情境表达题（`AssessmentService` 经 `SentenceService.RateAsync` 传 `free expression` 目标）同源问题本任务不动（影响 SentenceLog 留痕口径，另开任务跟进）。

### 实现
- `LlmModels.cs`：`SentenceRatingRequest` 加 `IsFreeExpression = false`。
- `FreeExpressionService.RateAsync`：中性主题 + 标记；`LlmPromptFactory`：变体分支 + `ChallengeRules` 常量；`LlmMockProvider`：按标记判定自由表达。

### 验证
- 新增 `FreeExpressionRatingTests` 4 例：捕获桩断言请求不再含字面量 free expression；prompt 变体无 Target Word 行且挑战度规则仍在；造句 prompt 不变；Mock 回归——高质量自由表达段落相关性/词汇维不被压、不拿 C。
- `dotnet test`：211 单元 + 6 集成全绿。
## 2026-08-06 — I7 T-030：文案与内部字段泄露清理（程实）

### 需求
- 菜鸟月仿真 UI 走查（`report/sim-month/data/ui-walkthrough.md` O6/O7/O9/O11/O13 + E0）：造句反馈泄露场景 key 'directions' 且译作「指路/指示」与计划卡「问路导航」口径不一；Profile/阅读/复习页英文外露（Overall/Initial/Builtin/correct 等）；老用户进 /assessment 仍见「首次水平测评」；登录 502 误报「请检查邮箱和密码」；评估报告三维同分却写「较强/薄弱」自相矛盾。

### 决策
- **场景 key 从 prompt 源头收口**：`BuildSentenceRatingPrompt` 的 Scene 字段改传 `ScenarioTaxonomy` 中文名（未收录回退原 key）——LLM 反馈文案复述的即是中文名，与计划卡同源同口径，比前端再映射一层更小改动。
- **英文外露统一走前端映射表**（项目无 i18n 框架，跟随既有 `NATURE_META`/`LEVEL_LABELS` 硬编码中文的做法）：等级历史 reason（Initial/Upgrade/Rollback）、难度桶（Basic/Intermediate/Advanced）、文章来源（Builtin/Llm）、话题标签（life 等 8 个种子话题）各自 fallback 原文。
- **同分不评强弱**：报告模板 `>=` 分支拆为严格大于两分支 + 同分「表现相当」，消除「较强（64）/薄弱（64）」自相矛盾。
- **登录错误按 HTTP 状态分层**：无响应或 ≥500（网络/网关/服务错误）→「无法连接服务器，请检查网络后稍后再试」；4xx 才是凭证/注册冲突文案。

### 实现
- 后端：`LlmPromptFactory`（Scene 中文名 + 注释）；`EvaluationReportService`（同分不评强弱、摘要去「Overall」英文）；`LogEndpoints`（recent 拼写结果 correct/missed → 正确/拼错）。
- 前端：`LevelPanel`（reason/难度桶中文映射）；`ArticleLibrary`（分组标题难度中文、source/topicTag 中文映射）；`InitialAssessment`+`App.tsx`（新增 `hasCompleted` prop，老用户显示「重新水平测评」+ 覆盖定级提示）；`LoginPage`（axios 状态区分网络/凭证错误）；`ReviewQueue`（最近记录加字母档图例 A 优秀 · B 良好 · C 及格 · D 需重写）。
- 测试：`SentenceRatingPromptTests` 净增 2 例（directions → Scene: 问路导航、未收录 key 回退原文）。

### 验证
- `dotnet test`：209 单元 + 6 集成全绿；前端 `npm run build` 通过。
- 存量已生成的评估报告/日志文案为历史快照不追溯，新内容生效。

## 2026-08-06 — I7 T-029：Dashboard 双卡加载骨架与洞察空状态行动建议（程实）

### 需求
- 菜鸟月仿真 UI 走查（`report/sim-month/data/ui-walkthrough.md` O1/O3）：计划卡/洞察卡接口 10-25s 无加载态，期间像「今天没计划」；洞察卡空状态对 207 条记录的用户仍只说「状态良好」，无行动建议。

### 决策
- **加载态改骨架屏**：计划卡/洞察卡 status 为 loading 时展示卡片外壳 + 既有 `LoadingSkeleton`（`skeleton` 样式已在设计系统内，不引依赖）；仅请求失败（静默降级 error）才不展示卡片，保持 T-018/T-019 降级口径。
- **空状态分层**：探索周进行中（T-032 exploration.active）优先显示探索进度（第 X/7 天 · 再完成 N 次表达），不与计划卡的探索周文案冲突；非探索周给一句行动建议（「今天试试用新学的词造句，表达越多，洞察越准」）。

### 实现
- 仅前端 `Frontend/src/pages/Dashboard.tsx`：showPlanCard/showInsightCard 条件由「active/none」改为「非 error」；两卡 loading 分支渲染骨架；洞察卡 none 分支按探索周三态出文案。

### 验证
- `cd Frontend && npm install && npm run build` 通过；纯前端改动未跑 dotnet test。

## 2026-07-30 — I7 T-040：多词短语命中口径——词序列连续匹配，短语可确认可毕业（程实）

### 需求
- 周密 T-034 仿真实证：词边界分词只产单词 token，多词 lemma（up in arms / see eye to eye 等）在任何句子里恒判「未命中」——13 个 PromptedUse 词中 10 个多词全部卡死（up in arms 造句两次拿 A 且短语逐字出现仍不确认），3 个单词词全部正常确认；确认与毕业两条路都走不通，T-034 配额+补位会持续把多词习语推进 PromptedUse 死胡同。

### 决策
- **统一命中工具落 Domain 纯函数**（`TargetWordMatcher`）：输入目标词（单词或多词短语）与文本，输出是否命中——单词保持既有词边界匹配（不误伤子串）；多词短语按词序列连续匹配，大小写不敏感、容忍标点/多余空白分隔（"up, in arms,"、"up  in  arms" 均命中），词序必须一致（"armed up in" 不命中）；不做词形变换，原样小写词序列匹配，保持简单。
- 不搬 T-033 `BottleneckScreeningService` 的内容词/停用词口径——那套只服务安全词信号（子集匹配、顺序无关），生命周期链路要求逐字连续命中，两套口径分开。
- `WordLifecycleService.IsPromptedUseCorrect/IsPromptedUseMisuse` 入参由 token 集合改为原句文本，命中判定收口到 Domain；瓶颈筛查内部 `Tokenize` 保留自用（连接词统计、安全词内容词），不动。

### 实现
- 新增 `NextWord.Domain/Services/TargetWordMatcher.cs`（`IsHit(target, text)` + `Tokenize`）。
- 改造三处判定调用点，删除各自对 `BottleneckScreeningService.Tokenize` 的复用：`WordLifecycleService.IsPromptedUseCorrect/IsPromptedUseMisuse`（签名 tokens→sentence）、`SentenceService.ApplyLifecycleEvidenceAsync`（造句确认/回退）、`FreeExpressionService.GraduatedSpontaneousUseAsync`（自发毕业）。

### 验证
- `dotnet test`：182 单元 + 6 集成全绿（基线 175+6，净增 7）；前端无改动跳过 build。
- 新增 `TargetWordMatcherTests` 5 例（纯函数）：单词词边界回归（armed 不含 arm）、分隔变体命中（标点/多余空白/大小写）、乱序与中间插词不命中、词边界仍生效（upbeat≠up）、不做词形变换、空目标不命中。
- `WordLifecycleTests` 净增 2 例（真实 PG + 评分桩）：短语造句 A 档确认 + 分隔变体确认 + 乱序不确认；自由表达含短语 B 档毕业 spontaneous_use 留痕 + graduatedWords 响应。
## 2026-07-30 — I7 T-042：测评定级防伪闸——升带阈值 70 + 识别档差矫正留痕（程实）

### 需求
- 按 `docs/DESIGN-assessment-anti-inflation.md`（顾言定稿，T-041 拆出）实现：T-023 分带校准后定级仍虚高（仿真菜鸟表达 75 定 B2、词汇识别参考仅 33），产出池按定级带取词致毕业链路结构性不可达。两道闸：自适应升带阈值 60→70；定级完成后识别防伪闸一次性矫正（档差 ≥2 下调 1 档并明示留痕）。验收：设计 §4 五条。

### 决策
- **阈值/档差常量集中**：`AssessmentScoringService.BandUpThreshold=70` / `BandDownThreshold=40` / `RecognitionGuardBandGap=2`，后续按仿真再校准只改一处。
- **识别样本缺失的口径落为「无作答」**：设计括号写「用户全跳过识别题」——`SubmitBlockAsync` 起未作答的词汇识别题不再记入样本（原来记为答错），`FinalizeAsync` 以 `vocabulary.Count == 0` 判定样本缺失不矫正；已作答部分仍按正确率映射参考档。
- **留痕位置**：`AssessmentFinalResult` 新增 `OriginalLevelBeforeGuard`（矫正时为表达定级原档，未矫正 null），随 FinalLevel `AssessmentRecord` 持久化；`Assessment.FinalLevel`、`UserProgress.OverallLevel`、LevelHistory 均为矫正后定级。用户可读说明进 `Dimensions.Comments`（结果页已渲染 comments，无需额外 UI）与评估报告摘要（`EvaluationReportService` 两条内容路径都拼接）。
- 识别不加权进表达力综合分、T-023 分带表与 T-027 评分 prompt 不动、确认挑战路径不动（§2.3 不做的事全部守住）。

### 实现
- 后端：`AssessmentScoringService`（阈值常量 + `ApplyRecognitionGuard`，档差 ≥2 下调 1 档、下限 A1、null 样本/反向不矫正）；`IAssessmentScoringService` 接口同步；`AssessmentService.FinalizeAsync`（主定级后应用矫正、矫正说明进 comments、留痕字段）与 `SubmitBlockAsync`（跳过识别题不计样本）；`AssessmentModels.AssessmentFinalResult`（新增 `OriginalLevelBeforeGuard`）；`EvaluationReportService`（报告摘要含矫正说明）。
- 前端：`types/assessment.ts` 补 `originalLevelBeforeGuard` 字段；结果页经 comments 展示矫正说明（既有渲染）。
- 测试：`AssessmentScoringServiceTests` 块表现阈值用例更新（65 不升带/75 升带，60 由 Up 改 Stay）+ 防伪闸 7 例（档差 ≥2 矫正、档差 1/反向/A1 下限/样本缺失不矫正）；`AdaptiveAssessmentServiceTests` 净增 2 例（表达 76 + 识别全错 → B2 矫正为 B1 且留痕可查；全跳过识别题 → 不矫正不报错），strong-user 用例改识别答对（识别全错拖低表达定级正是 T-042 新行为，原「不拖低」断言随之更新）。

### 验证
- `dotnet test`：185 单元 + 6 集成全绿（T-034 基线 168+6，净增 17）；前端 `npm install && npm run build` 通过。
- 菜鸟剧本仿真复跑（设计 §4.1）归周密验收。

### 验收修复（2026-08-06，周密「有条件通过」→ P1 返修）
- **P1（矫正未传导分数先验/CefrDisplay）**：`FinalizeAsync` 原以未矫正表达分写三维先验 → `CefrDisplay` 仍按矫正前虚高档，Planner 词池/造句目标按虚高档取词（实测矫正后 B1 用户拿到全 B2 习语），T-041 病灶未收口。按顾言拍板口径修复：矫正触发时三维先验逐维 clamp 到矫正后档上限以内（`AssessmentScoringService.GetBandScoreCeiling` = 分带 Max − 1，保持相对形状，不做复杂换算）→ `CefrDisplay` 与评估报告摘要头部同根因归正（P2 一并修掉，报告在 `ApplyUpdateAsync` 之后取分，无需额外改动）。
- **P3（阅读题口径对齐）**：阅读题未作答由「记答错」改为与词汇识别同口径「不计样本」；「跳识别用户参考分显示 0/A1」展示语义不动，留 backlog。
- 测试补强：矫正用户三维先验 ≤ 矫正后档上限（69）、`CefrDisplay=B1`、Planner 背词队列与造句目标全部带内 B1（真实 PG，含 `LearningPlanService.GenerateAsync` 实跑）；`GetBandScoreCeiling` 映射 3 例。

## 2026-07-30 — I7 T-034：词生命周期提速——回忆考察配额 + Recalled 池补位 + 毕业时刻可见（程实）

### 需求
- 菜鸟月仿真（`report/sim-month/REPORT.md` 发现五）：一个月 185 词仅 Recalled 10、PromptedUse 1、毕业 0——每日词队列以新词为主，老词成熟后很少再被抽到；Planner 造句目标候选池空时直接落回当日新词，老词永远轮不到产出机会；毕业用户无感知。按 `docs/DESIGN-lifecycle-acceleration.md`（顾言定稿）实现：每日词 ≥40% 回忆考察位、造句目标二级补位 Recalled 池、毕业时刻可见；验收锚点 14 天毕业首词、30 天仿真毕业 ≥3（仿真归 QA）。

### 决策
- **「已成熟待回忆考察」池的口径修正（设计字面 → 可运行口径）**：设计括号写「recognized 且 RepeatCount≥2，recall 模式」，但在 T-014 状态机里认识词在 SM-2 成熟那次认识考察即升 recalled，且 recall 模式考察认识阶段词不推进阶段（状态机一行不改是硬约束）——成熟待推进老词实际落在 **recalled 阶段**（考察模式天然 recall，答对升 prompted_use）。落池口径：`recalled` 阶段 ∪ 认识且 `RepeatCount≥2` 的残留词（答错但自评 Remembered 的边界；其推进考察为认识模式，答对即升 recalled），考察模式统一按阶段派生。§4.1 的「recall 模式占比 ≥40%」按池主体（recalled 词）达成。
- **回忆考察位（`DailyWordSelectionService.RecallExamQuotaRatio = 0.4` 常量）**：Plan 队列与难度带回退队列都应用——池词先占 `ceil(count×0.4)` 名额（`StageUpdatedAt` 最早优先、去重已在薄弱复习位的词），Plan 词/带内新词补满（Plan 定词、配额定考察模式）；接触词 ≤20% 规则不受影响。
- **造句目标二级补位**：`prompted_use` 未确认池（既有）→ Recalled 池（带内、utility 非 low、`StageUpdatedAt` 最早优先）→ 当日带内词（兜底）；两池拼接后 7 天顺次消耗。
- **毕业可见**：`IFreeExpressionService.RateAsync` 返回 `FreeExpressionRatingResult(Log, GraduatedWords)`，端点 DTO 带 `graduatedWords`；新增 `GET /api/words/graduated`（当前用户 spontaneous_use 词 + 毕业时间），前端一处接口三处展示（结果区提示 / Dashboard 本周计数 / 词库标记），失败静默降级。
- 毕业标准与四阶段状态机未改一行；无实体变更，无需迁移。

### 实现
- 后端：`DailyWordSelectionService`（配额常量 + `GetRecallExamPoolAsync`/`IsMaturePending`/`ToReviewItem`，Plan 与回退两路径接入）；`LearningPlanService`（`GetLifecyclePoolAsync` 二级补位，`FilterInBandLemmas` 收口带内过滤）；`FreeExpressionService`（毕业判定返回词列表）；`IFreeExpressionService`（结果记录）；`FreeExpressionEndpoints`（DTO 带 `graduatedWords`）；`WordEndpoints`（`/graduated` 端点 + `GraduatedWordDto`）。
- 前端：`hooks/useGraduations.ts`（新增，周计数 + Id 集合）；`FreeExpression.tsx`（结果区「🎉『xxx』毕业了」提示）；`Dashboard.tsx`（计划卡下方本周毕业计数，无则不显示）；`Home.tsx`（词库行内「已毕业」徽标 + `overrides.css` 一样式）；`endpoints.ts` / `types`（graduatedWords、GraduatedWord）。
- 测试（`WordLifecycleTests` 净增 4 例，真实 PG）：回退队列 10 成熟词 recall 考察位 ≥40%、成熟池不足新词补位不报错（含认识阶段残留词认识模式考察）、Plan 队列同样保底 ≥40%、Planner 二级补位（Recalled 池最早优先、超带与 utility low 不进池、兜底落带内词）；毕业测试补 `graduatedWords` 断言（达标带词、不达标为空）。

### 验证
- `dotnet test`：168 单元 + 6 集成全绿（基线 164+6，净增 4，T-014 生命周期测试零回归）；前端 `npm install && npm run build` 通过。
- 30 天仿真验收（设计 §4.4）归周密另跑。
## 2026-07-30 — I7 T-032：画像冷启动「探索周」——表达任务攒证据 + 冷启动画像重生成 + Verifier 放宽档（程实）

### 需求
- 菜鸟月仿真发现三（`report/sim-month/REPORT.md`）：首测画像 Finding 全被 Verifier 以「证据仅 1 条」判 Questioned → Planner 只消费 Verified → 计划永远「探索期」兜底；画像重生成唯一触发是瓶颈性质变化，而洞察对菜鸟不触发 → 画像死锁，30 天 5 份计划全探索期。按顾言定稿的 `docs/DESIGN-cold-start-profile.md` 实现：探索周每日 1 个场景表达任务攒证据、Dashboard 进度可见、满 7 天或证据 ≥10 条自动重生成画像（每用户一次）、Verifier 冷启动档。

### 决策
- **触发判断抽纯服务**：`ColdStartExplorationService`（注册满 7 天 / 产出证据 = SentenceLogs + FreeExpressionLogs ≥10 条，满足其一）供 `ProfileScoreSnapshotWorker` 日检复用，可单测。
- **「每用户仅一次」落标记位**：冷启动重生成画像以 `ModelProfileId = "weakness-profile-coldstart"` 落库，与瓶颈触发（T-007）的 `"weakness-profile"` 重生成互不混淆；复用既有字段，无 schema 变更、无迁移。
- **放宽档只放宽样本量纪律**：证据真实、数值一致但条数不足 → 置信下调 low 标 Verified 注「初步判断」；伪造/越权/数值不符机械核查任何情况不放宽；第二份画像起（默认档）恢复既有纪律。low 进规划走既有「只认 Verified」逻辑，无需改 Planner。
- **探索任务编排最小侵入**：不动 LearningPlan ContentJson 与评分链路，`GET /api/planner/current` 响应附带 `exploration` 字段下发第 x/7 天、还差 N 条（N = max(0, 10 − 证据条数)）、今日场景表达题（taxonomy 轮转、优先词池已标注子场景）。

### 实现
- 后端：Domain 新增 `IColdStartExplorationService` + `ColdStartModels`（ExplorationWeekStatus / ColdStartTriggerEvaluation）；Infrastructure `ColdStartExplorationService`（探索周状态、触发判定、场景轮转出题）；`FindingVerifier` 增 `relaxedColdStart` 放宽档；`WeaknessProfileService.GenerateAsync` 增 `coldStart` 参数（放宽档 + 标记位）；`ProfileScoreSnapshotWorker` 日检挂触发器（面向全部用户，含跳过首测者）+ 入队 force Planner（幂等键 `planner:coldstart:{userId}:{yyyyMMdd}`）；`PlannerEndpoints` /current 附 exploration；DI 注册。
- 前端：`types/planner` 增 `ExplorationWeek`；`useLearningPlan` 携带 exploration（无 Plan 也透传）；新增 `useExplorationWeek`；Dashboard 计划卡探索周徽章 + 进度文案 + 「去写今日表达」入口（无 Plan 的探索周用户同样显示）；SentenceStudio 探索周默认落自由表达 Tab（用户手动切换后不覆盖）；FreeExpression 今日探索任务横幅 + placeholder 带题；LevelPanel 低置信 Finding 带「初步」徽标。
- 测试：`ColdStartExplorationTests` 9 例——触发器满 7 天 / 证据 ≥10 / 周内不足静默、「仅一次」且与瓶颈重生成不混淆、进度口径（第 x/7 天、N 口径与下限 0、7 天后探索周结束）、Verifier 放宽档（不足→low Verified 注初步判断、伪造/篡改仍 Questioned、默认档恢复纪律）、全链路标记位闭环（生成后触发器不再放行）。

### 验证
- `dotnet test`：180 单元 + 6 集成全绿（基线 171+6，净增 9）；前端 `npm install && npm run build` 通过。

### 验收阻断与修复（2026-07-30 周密验收不通过，`report/qa-t032/REPORT.md`，2 项阻断）
- **阻断 1：FreeExpressionLogs 对画像不可见**。触发计数含自由表达，但 `WeaknessProfiler` 只聚合 SentenceLogs/测评/场景词/阅读统计、Verifier 无对应证据类型 → 纯表达用户冷启动画像必为 0 条 Finding（实测用户 A/B 画像 findings=0）。修复：`WeaknessProfileRequest` 增 `FreeExpressionLogs`（最近 30 条，Id + AiScore + OverallGrade），Profiler 同权聚合；`LlmPromptFactory` 画像提示词增自由表达数据段与 `free_expression_log` 证据规则；`FindingVerifier` 增 `free_expression_log` 证据类型机械核查（存在性/归属本人按 UserId 过滤、Metric=aiScore 数值一致），与 sentence_log 同一纪律，coldStart 放宽档同步生效。
- **阻断 2：Skill 维 Finding 不进 sourceFindingIds**。`LearningPlanService` 只把场景维 weakness Finding 计入来源，技能画像读了不用 → 有 Verified 技能画像计划仍显示「探索期」（实测用户 C 3 条 Verified 仍 sourceFindingIds=[]）。修复：sourceFindingIds 计入 Verified 技能维 weakness Finding（主攻场景选择逻辑不变：仍只从场景维取、无则覆盖率兜底）——顾言口径：「个性化」徽章语义 = 计划基于了任何 Verified Finding。
- 测试：新增 3 例——Profiler 聚合自由表达留痕进请求、Verifier 对 free_expression_log 的伪造/篡改/未知指标核查 + 放宽档同步、技能维画像 sourceFindingIds 计入（含仅技能画像的独立用例）；`LearningPlanTests.Generate_uses_only_verified_findings_and_is_idempotent` 断言按新口径反转（skillId 从不含改为计入）。修复合入原提交（--amend）。
- 非阻断不足（记录在案，未修）：同日幂等不区分 ModelProfileId（同日先有瓶颈重生成则冷启动标记位延迟到次日，反之亦然，最终仍仅一次）；触发循环单用户 LLM 异常会中断当轮其余用户判定（次日恢复）；多实例并发理论可双触发；存量老用户首个日检一次性批量触发（每用户 1 次画像 LLM，上线需关注配额）；真实 LLM 下 low「初步判断」曝光率可能很低（产品知悉）。

## 2026-07-30 — I7 T-027：造句/自由表达评分纳入相对水平挑战度（程实）

### 需求
- 菜鸟仿真发现：简单但正确的句子频繁拿 A/四维满分（如 B2 目标词下「It's healthy. Moreover, it's super cheap.」），评分只看正确性、奖励安全简单句，与愿景「干预安全词策略」相悖，也喂坏平台期信号。顾言定两条规则：评分手里的尺子 = 用户水平带；挑战度纳入评分口径（安全简单句词汇维 ≤3、总评封顶 B；高超带尝试不因难度扣分；与水平相称的简单句不受罚——菜鸟公平性，prompted_use 确认链路依赖 A/B 档）。
- 顺带收口 next-steps 既有 follow-up「SentenceStudio 评分入参改传难度 bucket」。

### 决策
- **评分带单一来源**：新增 `RatingBandResolver`（Domain）——UserProgress 投影的 CefrDisplay（ScoreMapping 单一来源，含分数推出）优先 → 调用方显式传入的带（测评/挑战路径）→ 默认带 A2（匿名/无进度）。`SentenceService` / `FreeExpressionService` 在评分前从 `IScoreProfileService` 解析，两个评分端点不再写死 `?? "A2"`、不再信任客户端传带（仅作无进度回退）；测评/挑战显式传带行为不变。
- **Prompt**：造句评分模板（自由表达共用同一模板）追加中文挑战度规则，四维与 overall_grade 都说明白；`LlmResponseParser` 不变。
- **Mock 口径**：各水平带设最低句长/连接词数期望（A1 3/0、A2 4/0、B1 6/1、B2 9/2、C1 11/2、C2 13/3），句长与连接词数都未达到即视为安全简单句——词汇维压到 ≤3、总评 A→B 封顶，并追加中文提示；A1/A2 期望连接词为 0，菜鸟简单句永不触罚。

### 实现
- 后端：`Services/RatingBandResolver.cs`（新增）；`ISentenceService` / `IFreeExpressionService`（userLevel 改可空 + 注释口径）；`SentenceService` / `FreeExpressionService`（评分前解析带，后者新增 `IScoreProfileService` 注入）；`SentenceEndpoints` / `FreeExpressionEndpoints`（去掉 `?? "A2"`，传原值）；`LlmPromptFactory.BuildSentenceRatingPrompt`（挑战度规则段）；`LlmMockProvider.RateSentenceAsync`（带期望表 + 压分启发式）。
- 测试：`RatingChallengeTests.cs` 7 例——Mock：B2 安全简单句词汇维 ≤3 且不拿 A、A2 相称简单句仍 B 及以上、B2 带内挑战句不压分；真实 PG：造句评分带取 UserProgress（B2）、无进度回退调用方带（B1）与默认带（A2）、自由表达带取 UserProgress（C1）；prompt 含挑战度规则断言。`WordLifecycleTests` 两处 `FreeExpressionService` 构造补注入。

### 验证
- `dotnet test`：164 单元 + 6 集成全绿（基线 157+6，净增 7）；前端 `npm install && npm run build` 通过（前端无改动——客户端传带降级为回退入参）。
## 2026-07-30 — I7 T-033：瓶颈洞察信号 v2——相对基线 + 零起步信号（程实）

### 需求
- 菜鸟月仿真（`report/sim-month/REPORT.md` 发现四）：3 次筛查 0 触发，最需要洞察的起步期用户永远得不到洞察——回避模式绝对基线 0.3 菜鸟数学不可达、平台期 stdDev≤0.5 对菜鸟真实波动（0.8–1.5）太紧、安全词窗口被 7 天计划周期 + 24h 宽限压缩到样本不足。按顾言定稿的 `docs/DESIGN-insight-signals-v2.md` 实现。

### 决策
- **信号从「绝对阈值」改为「和自己比」**：回避模式废弃 `AvoidanceMinBaseRate` 绝对基线，前半段率 >0 即有基线；从未用过复杂连接的用户不判回避，交新增零起步信号覆盖。
- **零起步信号只触发不定性**：`BottleneckSignal.ColdStart`（wire 名 `cold_start`，字符串存储/传输，旧数据不受影响）映射到哪类性质由 InsightAgent 细读原文判断，7 类性质枚举与 prompt 不硬编码、不动。
- **安全词窗口按篇数不按天数**：最近 5 篇自由产出跨计划周期累计（原 T-012「窗口自 `Plan.CreatedAt` 起算」口径废止，防误判职责由保留的 24h 宽限期独立承担）；目标词仍取当前生效 Plan。多词短语匹配修口径：拆词去停用词取内容词、全部同现（词边界）才算用过，避免整串匹配永不命中与功能词恒命中两个极端。

### 实现
- `BottleneckScreeningService`：平台期 `PlateauWindow` 10→12、`PlateauMaxStdDev` 0.5→1.0；回避模式改 `firstRate > 0 && secondRate ≤ firstRate × 0.5`；新增 `IsColdStart`（近 10 次连接恒 0 + 后半段平均句长 ≤ 前半段 ×1.1 + 跨度 ≤30 天，句长 = 词数 `WordCount` 与 `Tokenize` 同口径不去重）；安全词 `SafeWordMinFreeSamples` 3→5、查询改为最近 5 篇跨计划累计、新增 `StopWords` 表与 `TargetContentWords`（全功能词短语退化为原拆词防空集恒真）。
- 枚举：`BottleneckSignal.ColdStart = 4` + `BottleneckSignalNames` wire 名/解析；`InsightEndpoints`/`ProfileScoreSnapshotWorker`/`BottleneckInsightWorker` 均透传信号，无需改动。
- 测试（`BottleneckInsightTests`，13→20 例）：平台期窗口 12 与 stdDev 边界（0.82 触发/1.5 不触发，回文序列保证斜率 0）；回避相对基线（率 1/6 低于旧 0.3 仍触发、从未使用不判回避）；零起步三边界（恒 0+句长不增触发、句长增长不触发、跨度超 30 天不触发）+ wire 名回读；安全词新窗口（5 篇触发/4 篇不足/跨计划周期累计触发）与短语匹配三边界（只中功能词触发、内容词同现不触发、只中其一触发）；原 T-012「Plan 创建前产出不计入」用例按新口径反转（计入且触发，已注明），宽限期用例保留。

### 验证
- `dotnet test`：164 单元 + 6 集成全绿（基线 157+6，净增 7）；前端无改动（`NATURE_META` 7 类性质映射不涉及信号）。

## 2026-07-30 — I7 T-023：首次测评定级校准——CEFR 分带重排（程实）

### 需求
- 菜鸟仿真（`report/sim-month/REPORT.md` 发现二）：四维均分 3.2/5 → 表达力综合分 64 → 旧分带（B2 50–70）定 B2 明显偏高，导致计划词、造句目标、推荐文章全部超水平。顾言定锚点：B2 =「能就日常场景组织清晰连贯的句子、偶有小错」，对应四维均分 4/5 以上；验收口径：综合 64 必须落 B1、80 以上进 B2。

### 决策
- **分带重排（全局口径，`ScoreMapping:CefrBands`）**：A1 0–20 / A2 20–35 不变（低端行为不动），B1 35–70（扩带承接原 B2 下半段），B2 70–85（起点 70 ≈ 均分 3.5/5，满足 64→B1、80→B2 且与 `DifficultyBuckets` Advanced 起点 70 自然对齐），C1 85–95，C2 95–100。单调全覆盖 0–100；`DifficultyBuckets` 不动；测评封顶 C1 既有规则不变。
- **定级阈值去硬编码**：`AssessmentScoringService.MapExpressionScore` 原硬编码阈值 [19,34,49,69]（声称「与分带对齐」实则是分带的漂移副本），改为构造注入 `ScoreMappingOptions`、直接从 `CefrBands` 派生（score < band.Max 归带，跳过 C2 带封顶 C1），分带成为分数→CEFR 的单一来源。
- **识别映射不动**：`MapVocabAccuracy` / `MapReadingAccuracy` 是识别正确率的独立参考口径（不参与主定级），阈值与表达力分带本就不同，保持原样。
- LLM 评分 prompt 宽松问题（简单句拿满分）归 T-027 另案，本任务不动。

### 实现
- 配置：`appsettings.json` 与 `ScoreMappingOptions` 默认值同步改分带（Dev/Prod/Testing 三个环境文件均未覆盖 ScoreMapping，无需同步）。
- 代码：`AssessmentScoringService`（主构造注入 options + `MapExpressionScore` 派生实现）；`DependencyInjection.cs`（注册改为工厂注入 IOptions）。
- 硬编码排查结论：分数→CEFR 的唯一硬编码点就是 `MapExpressionScore`（已收口）；Profile 展示/legacy levels 走 `ScoreProfileService`→`ScoreMappingService`（配置驱动）；挑战 `AttemptedLevel` 源自 `progress.OverallLevel`（由 Score 内核按配置推出）；前端只有 CEFR 排序常量与文案，无分带阈值。
- 测试调整（只改预期不改逻辑）：`ScoreMappingServiceTests`（分带边界 50→B1、70→B2、85→C1、95→C2）；`AssessmentScoringServiceTests`（60→B1，新增 T-023 锚点组：64→B1、80→B2、69→B1、70→B2、100→C1 封顶）；`ScoreProfileServiceTests`（总分 53：B2→B1）；`AdaptiveAssessmentServiceTests`（稳定用户 52 分定级 B2→B1，强者 100→C1、弱者降带与 2–3 块收敛在新分带下不变）。

### 验证
- `dotnet test`：157 单元 + 6 集成全绿（基线 150+6，净增 7）；`npm install && npm run build` 通过。

## 2026-07-30 — I7 T-022：日常造句/自由表达评分小步回写 Score 内核（程实）

### 需求
- 仿真实测发现日常造句/自由表达评分完全不写 Score（一个月 91 条造句 + 12 篇自由表达零写入，三维分 30 天不变）；`SentenceLlmScoringWorker` 全库无入队点是死链路。要求评分后小步 delta 写 Writing 维（幂等现成）并在评分反馈展示分数变化，顺带清理死代码。

### 决策
- **回写挂在端点层**：新增 `PracticeScoreWritebackService`，仅由 `POST /api/sentences/rate` 与 `POST /api/free-expression/rate` 在评分落库后调用；测评（AssessmentService）与挑战（ChallengeService）复用 `SentenceService.RateAsync` 但不经过端点，天然隔离、绝不叠加 delta（测评完成时已写 absolute 先验）。
- **口径**：observed = `MapSentenceToScore(四维均分)`（自由表达 `AiScore` = 四维总分 × 5，数值同口径）；delta = clamp(round((observed − current) × 0.1, AwayFromZero), −2, +2)；delta = 0 也照常走 `ApplyUpdateAsync` 落幂等记录，防重放。
- **幂等键**：`sentence-score:{sentenceLogId}` / `freeexpr-score:{logId}`（Guid 天然唯一，重试/重放不重复加分）。
- **响应带出**：两个评分 DTO 增加可空 `writingScoreBefore/After`；前端新增 `WritingScoreBadge`，仅在有变化时显示「写作 64→65（+1）」（涨 success、降 warn）。
- `SentenceLlmScoringWorker` 死代码删除（类、DI 注册、`BackgroundJobWorker` 分支），全库 grep 零引用。

### 实现
- 后端：`Services/PracticeScoreWritebackService.cs`（新增）；`SentenceEndpoints.cs` / `FreeExpressionEndpoints.cs`（注入回写 + DTO 两字段）；`DependencyInjection.cs`（换注册）；`BackgroundJobWorker.cs`（删分支）。
- 前端：`components/WritingScoreBadge.tsx`（新增）；`types/sentence.ts`（两类型加可空字段）；`SentenceCard.tsx` / `FreeExpression.tsx`（结果区挂徽标）。
- 测试：`PracticeScoreWritebackTests.cs` 7 例（真实 PG）——高/低观测分 clamp ±2、小差距 +1、同幂等键重放不重复加、delta=0 幂等记录照落、自由表达回写、测评路径 RateAsync 零 sentence-score/freeexpr-score 事件。

### 验证
- `dotnet test`：150 单元 + 6 集成全绿（基线 143+6，净增 7）；`npm install && npm run build` 通过。

## 2026-07-30 — 顾言：菜鸟用户一个月体验仿真（产出 I7 需求 T-022~T-031）

### 需求与方法
- 顾言以「一个月时间线体验产品、寻找产品灵感而非堆功能」为目标，仿真初中水平菜鸟「小菜」：真实全栈（独立库 `nextword_sim` + DashScope qwen-plus 真实 LLM），30 天时间线（DB 时间戳回拨模拟流逝），活跃 28 天完整走背词/拼写/造句/自由表达/阅读/挑战闭环，3 次瓶颈筛查，Playwright 实拍 20 张 UI。报告与数据：`report/sim-month/REPORT.md`（仿真器 sim.py 可复跑）。

### 关键结论（一个月轨迹）
- 等级/三维分 30 天零变化（B2、64·64·64）：日常造句/自由表达评分无 Score 写入，`SentenceLlmScoringWorker` 全库无入队点（死链路），Score 实际只剩测评与确认挑战两处写入。
- 定级偏松：四维 3.2/5 定 B2 → 计划目标词/推荐文章全部超水平 → 造句 C/D 为主。
- 个性化冷启动死锁：首测 4 条 Finding 全被 Verifier 以证据不足判 Questioned → 5 份周计划永远探索期；画像仅瓶颈性质变化触发重生成，而洞察三信号对菜鸟结构性失灵（回避基线菜鸟恒 0 不可达、平台期波动超阈值、安全词窗口样本不足）→ 一个月 0 洞察、画像不更新。
- 词生命周期一个月 0 毕业：185 词仅 Recalled 10 / PromptedUse 1（回忆考察曝光不足 + 目标词过难确认不了）。
- 体验层：Daily 挑战通过零反馈；评分奖励简单安全句（满分案例多个）；Dashboard 双卡 10-25s 无骨架；评估报告陈旧自相矛盾；场景 key/英文术语外露。
- 对照 T-017《林晓的七天》：Agent 链路在精心构造的中级用户数据下能转，真实菜鸟有机数据走不通——差距在冷启动与信号口径。

### 决策与产出
- 产品灵感七条（分数微更新/定级校准/探索周/洞察相对基线/毕业提速/挑战有结果化/月度时间轴），登记 I7 backlog：T-022~T-031（P0 两条：T-022 分数回写、T-023 定级校准）。
- 仿真器与数据资产留档 `report/sim-month/`，建议后续迭代验收复用（改人设参数即可仿真其他画像）。

## 2026-07-29 — I6：Agent 价值用户可见（T-018 计划卡 / T-019 洞察卡 / T-011 裁决）

### 需求
- 画像/计划/洞察闭环此前只在后台运转，用户看不到 Agent 的价值。顾言提两个体验闭环任务：Dashboard 展示「今日学习计划」与「学习洞察」；顺带裁决积压的 T-011。

### 决策
- **T-011 裁决 deferred**：新用户无学习数据时不编造场景信号（违背 Verified 证据原则），覆盖率兜底是诚实路径；T-017 演示已证明积累行为后自动演进为 Verified 场景驱动，接受现状。
- 两张卡全部消费既有只读端点（`GET /api/planner/current`、`GET /api/insights/bottleneck/latest`），后端零改动（仅同步一条过时注释）；来源标注口径：`sourceFindingIds` 非空=个性化（Verified 画像驱动）、空=探索期（覆盖率兜底）。
- 洞察卡措辞面向用户：7 类瓶颈性质中文名 + 人话解释（前端 NATURE_META 常量），不暴露 evidenceLogIds 等内部 id。

### 实现（程实，前端纯增量）
- `types/planner.ts`（三个接口）、`hooks/useLearningPlan.ts`（计划 + scenarios 中文名映射，映射失败回退原始 key）、`hooks/useBottleneckInsight.ts`（NATURE_META 7 类映射）、Dashboard 欢迎条与模块网格之间两卡、`styles/overrides.css` 少量样式。
- 失败/加载中卡片不渲染（静默降级）；无计划=引导文案、无洞察=「状态良好」文案。

### 验收（周密 2026-07-29，独立库 nextword_verify_i6，验完已删、dev 库未动）
- [x] 无数据两态：计划卡引导文案、洞察卡「状态良好」（截图 case1）
- [x] 有计划态：POST /api/planner/jobs 触发后卡片逐项与 API JSON 核对一致——场景中文名（agree_disagree→同意与反对）、第 1/7 天、带内 8+接触 2、造句目标、探索期徽章（截图 case2）
- [x] 有洞察态：直插 AvoidancePattern 行 → 中文名+解释+statement+「已为你调整学习计划」徽章；DOM 泄漏检查通过（evidenceLogIds 不出现）（截图 case3）
- [x] `npm run build` 通过；`dotnet test` 143 单测 + 6 集成全绿
- 不足另开：T-020（.env 代理指向 8080 误导本地联调）、T-021（NU1903 Microsoft.OpenApi 高危漏洞）；非阻断观察：skip 测评用户计划卡文案语气待迭代

---

## 2026-07-28 — T-017：Agent 协作演示数据集《林晓的七天》

### 需求
- 此前演示看不到 Agent 的作用。要求：编一个故事做多轮操作演示 Agent 价值；**不改代码、不改数据**（定时任务触发时机可自行调整）；保留剧本、测试数据、Agent 对话与最终评价为独立数据集；以 time chart 呈现整个剧本；原 report/ 报告保留。

### 决策与实现
- 数据集 `demo/agent-story/`：`story.md` 剧本（虚拟用户林晓，7 天行为弧线：正常期 → 回避期 → Agent 介入）、`data/persona.json` 全部输入测试数据、`scripts/`（llm-proxy.py 记录代理 / run-story.py 驱动 / build-timeline.py 时间轴生成）、`output/`（69 事件 timeline.json、25 条 LLM 对话原文 jsonl+md、关键端点快照、关键表只读 dump、pg_dump、evaluation.md 终评）、`timeline.html` 交互式查看器（自包含单文件：故事日分组导航 + 角色过滤/搜索 + 缩略泳道总览 + 完整 LLM 对话气泡渲染 + 剧情节点↔对话双向跳转 + 键盘翻页）。
- 零侵入演示方式：独立库 `nextword_demo` + 环境变量切 LLM 到 qwen-plus 并经 `:5299` 记录代理转发留痕；用户侧数据全部经公开 API 真实提交、真实 LLM 评分；数据库仅 SELECT 观测；日级筛查用公开手动端点 `POST /api/insights/bottleneck/jobs`（与每日自动筛查同一代码路径）。
- 回避信号真实构造：前 6 条造句每句 1-2 个复杂连接词、后 6 条全简单句，由规则引擎零 LLM 捕获。

### 验收（实测结果）
- [x] 测评 2 块收敛定 B2（表达分 58/56，识别 0 分不干扰）；画像 4 Finding 全 Verified、报告 schemaVersion 2；Planner 首计划（新用户覆盖率兜底，接触词 2/10 全超带）
- [x] Day 7 筛查 triggered=avoidance → Insight 独立定性 **VocabularyInsufficient**（未给信号盖章，5 条证据 id 全部真实属本人）→ 性质变化触发重规划：画像重生成 6 条（含场景维度）+ Plan 原地重建（LearningPlans 仍 1 行），Plan2 sourceFindingIds=[9] 精确消费 Verified 场景 Finding——兜底→Verified 驱动的演进完整呈现
- [x] 任务链 EvaluationReport→Planner→BottleneckInsight→planner:replan 全部 Completed；LLM 25 次调用（画像/洞察各 1 次，幂等无浪费）、22.8k tokens
- [x] timeline.html Playwright 无头渲染校验：94 节点 0 JS 错误；角色过滤、搜索、事件↔对话双向跳转（P1/P2/P3 三次 Agent 对话归属全部正确）实测通过
- 偏差如实记录于 evaluation.md：洞察性质≠剧本预期（VocabularyInsufficient vs AvoidancePattern，反而展示定性权在 Agent）；背词作答 isCorrect=False 为驱动脚本以词形作答所致（recognition 按释义判对），不影响 Agent 链路

---

## 2026-07-28 — T-016：选项 RadioGroup 非受控致跨题重选失效修复

### 需求
- 演示录屏实测复现：挑战页连续两题点同一下标选项，第二题不触发 `onValueChange`，「下一个」永远禁用（视觉上 aria-checked 已变）。

### 决策与实现
- 根因：`ui/RadioGroup` 把 `value=undefined` 传给 base-ui `RadioGroup`，进入非受控模式，内部选中值跨题残留；下一题点同一下标时 base-ui 判定值未变化、不发回调。
- 修复（`Frontend/src/components/ui/RadioGroup.tsx` 一行）：始终受控，`value ?? ''` 空串占位表示「未选择」。封装层一处改动，测评/挑战/阅读题所有选项组同愈；附注释说明原因。

### 验收（开发自测）
- [x] 复现脚本 `Frontend/e2e/repro-t016.mjs`（真实 API + 前端）：连续两题点同一下标选项，「下一个」均正常解禁——通过
- [x] `npm run build` 通过
- [x] 周密复验（2026-07-28）：复现脚本实测两题（friend/ambiguous）均点下标 0、「下一个」均正常解禁；`npm run build` 通过。status → done

---

## 2026-07-25 — I4 顾言验收（T-013 / T-014 / T-015）

**验收结论：通过，I4 闭环。**

- T-014 周密六项标准全过：四阶段流转实测 25→50→75→100 吻合；自评切断全库核查彻底（mastery 写入仅 WordLifecycleService、Score 写入仅三处授权路径、EstimatedKnownRate 确认只做排程输入）；候选池 7 天顺次消耗；指定目标词不算自发、自由表达误用不毕业；存量映射幂等；
- **三个口径裁定确认**：①待自发不单列第五阶段（时间戳语义完整）接受；②毕业不强制先经确认——自发正确使用是更强证据、且仍需先过回忆考察进候选池，接受；③留痕 FreeExpressionLog id（自由表达实际通道）正确；
- T-013 僵尸任务回收实测：超时重置重跑、超限 Failed 留痕；
- T-015 迁移链收口复验三场景全过（空库一键、存量库兼容、重复启动幂等）；AGENTS.md 恢复正常迁移纪律——本轮迭代例外正式结束；
- 至此 VISION §5.2 背词重新定位全部落地（选词权上交 T-006、毕业四阶段 T-014、自评退出掌握度），§6 路径五项 + 生命周期改造全部完成。

---

## 2026-07-25 — I4 T-015：迁移链收口——空库一键启动建 schema

### 需求
- I4 验收不足（周密实测）：全新空 PG 库 `dotnet run` 启动依赖「MigrateAsync 吞错 + 补丁兜底」的脆弱路径（AddScoreKernelM1 的 IsCore ALTER 在 PG 必失败被吞、后续两个迁移永不执行、靠补丁补建并在迁移历史上补记），与 AGENTS.md「删库重建靠 Development 启动自动 Migrate + 种子」的预期不符。顾言定方向：按例外条款本意收口，生成正式迁移把 I1–I4 全部 schema 变化纳入迁移链。

### 决策
- **新增 `ConsolidateI1ToI4Schema` 迁移**：`dotnet ef migrations add` 生成后，按顾言方向第 3 条手工加 PG 守卫分支（AGENTS.md「不手工修改迁移文件」纪律的本任务例外）——PG 走幂等 SQL（`IF NOT EXISTS`，口径逐条对齐补丁：列类型、默认值、枚举存字符串、索引、唯一约束），已打补丁的存量库执行全部 no-op；非 PG（SQLite）路径保留生成代码。生成代码里 `LifecycleStage` 默认值空串（枚举转字符串的脚手架缺陷）手工修正为 `'Recognized'`。
- **旧迁移 PG 守卫**：`AddScoreKernelM1`、`AddChallengeSession`（SQLite 口味，PG 执行必失败或产生类型分叉）Up/Down 加 `ActiveProvider` 守卫，PG 上跳过——对应 schema 继续由补丁幂等负责（存量 PG 库历史里这两个迁移本就没真正执行过，守卫只是让空库 MigrateAsync 不再半途中断）。`AddArticleVocabPhoneticsAndExamples` 本身是 PG 类型且与补丁逐字一致，不动。
- **启动不再吞错**：`Program.cs` 移除 MigrateAsync 的 try/catch 吞错（周密建议「补丁失败显式报错而非带病进种子」）——迁移链修通后，失败即快速失败。
- **补丁保留不动**：`Patch_PostgreSql_ScoreKernel.sql` 继续作为存量 dev/prod 库的幂等升级路径；与新迁移重叠部分天然幂等共存（两边都是 IF NOT EXISTS 口径）。
- 口径差异知情确认：`ProfileFindings.VerificationNote` 模型为非空 string（EnsureCreated 建 NOT NULL），补丁 PG 列为可空——PG 分支沿用补丁可空口径（写路径总有值，无实际影响）；迁移 PG 分支额外补 `IX_WeaknessProfiles_AssessmentId`（EF FK 约定索引，补丁漏建，纯补齐无副作用）。

### 实现
- `Data/Migrations/20260725085338_ConsolidateI1ToI4Schema.{cs,Designer.cs}` + 快照更新：I1–I4 全量（WordScenarios、Words.Utility/Role/ScenarioAnnotationVersion、WeaknessProfiles/ProfileFindings、LearningPlans、BottleneckInsights、BackgroundJobs.StartedAt/RetryCount、UWR 生命周期四列 + 存量阶段映射 UPDATE）。
- `AddScoreKernelM1`/`AddChallengeSession` Up/Down 加 PG 守卫；`Program.cs` 去吞错；`DependencyInjection` 注释同步。
- `dotnet ef migrations has-pending-model-changes` 确认快照与模型一致。

### 验收（开发自测）
- [x] 空库一键：新建空库 `nextword_t015_empty` → `dotnet run`（Development）→ 6 个迁移全部干净应用（无吞错、无失败，唯一 fail 日志为 EF 探测历史表的良性首查）→ 种子 1523 词灌入 → 注册用户后 `GET /api/scenarios` 返回 7 大类场景与词数（验完库已删）
- [x] 存量库兼容：`pg_dump` 复制 dev 库 `nextword` → `nextword_t015_copy`（该库停在 I1 前，缺 I1–I4 全部表/列）→ 启动仅应用 Consolidate 迁移、零报错 → 数据行数不变（words 6 / uwr 3 / users 4）、缺失 schema 全部补齐、存量映射按补丁口径执行（RepeatCount<2 → Recognized/25）→ dev 库本体未动（验完副本已删）
- [x] `dotnet build` 通过；`dotnet test` 143 单测 + 6 集成全过（真实 PG）；`npm run build` 通过
- 复现备注：修复前在当前代码上空库实测其实可启动（吞错+补丁兜底路径工作），但属带病路径——任何补丁执行异常都会以「relation 不存在」的形式在种子期爆炸，正是周密观察到的故障形态；T-015 后该路径被正式迁移链取代
- 已知取舍：dotnet-ef 全局工具从 9.0.10 升到 10.0.10 以对齐 EF 10.0.9 包；生产 SQL 脚本路径（`generate-migration-sql.ps1` → `Upgrade_Idempotent.sql`）未重新生成，prod 升级继续走补丁（脚本是否重生成留给部署窗口决定）

### 复验（周密 2026-07-25）：通过，T-015 done
- [x] 空库一键：新建空库 `nextword_verify_t015_empty` → `ASPNETCORE_ENVIRONMENT=Development dotnet run` → 6 个迁移干净应用（历史表 6 行）、33 张表、种子 1523 词/21 文章/2383 WordScenarios、注册后 `GET /api/scenarios` 正常、日志零异常（唯一 fail 记录为 EF 首次探测 `__EFMigrationsHistory` 的良性首查）、全程无手动 ef/手动补丁（验完库已删）
- [x] 存量库兼容：`pg_dump` 复制 dev 库到 `nextword_verify_t015_existing`（缺 I1–I4 全部表列、历史 5 行）→ 仅应用 `ConsolidateI1ToI4Schema` 一行迁移零异常、数据行数逐项不变（words 6/users 4/uwr 3/sentencelogs 1/articles 21/bgjobs 0）、7 张缺失新表补齐、T-014 四列到位、存量关系映射 Recognized/25 无误判、注册与 daily 接口正常、dev 库本体未动（验完副本已删）
- [x] 重复启动幂等：同库二次启动零 `Applying migration` 零异常、种子不重复（仍 1523 词）
- [x] `dotnet test` 143 单测 + 6 集成全过（真实 PG）
- 观察（不阻断）：`--no-launch-profile`（Production 环境）空库启动按设计跳过自动迁移、在种子期快速失败报 relation 不存在——非缺陷（prod 走 SQL 脚本路径），快速失败行为符合预期；生产 SQL 脚本未重生成为开发已声明取舍

---

## 2026-07-25 — I4 T-014：词毕业四阶段生命周期（含 T-013 僵尸任务回收）

### 需求
- 按 `docs/DESIGN-word-lifecycle.md`（已定稿）：词的毕业标准是「能用」不是「认识」——四阶段闭环（认识→回忆→造句使用→自发使用）；SM-2 只管前两阶段调度；Remembered/Forgot 自评只改排程、不再参与掌握度与 Score；产出候选池由 Planner 优先编排；自由产出自发正确使用一次才毕业；顺带修 T-013（BackgroundJobWorker 只捞 Pending，进程中断后 Processing 任务永久僵尸）。

### 决策
- **四阶段状态机为纯规则领域服务**（`WordLifecycleService`）：认识→回忆 = SM-2 成熟阈值 `RepeatCount≥2`（复用 repetitions/interval 口径，不新造指标）；回忆→造句使用 = 回忆模式考察通过（看义正确拼词）；造句使用→待自发 = 提示造句词边界命中 + A/B 档（`PromptedUseConfirmedAt` 留痕）；待自发→毕业 = 自由表达中自发出现 + 当次 A/B 档（复用 T-007 分词口径），留痕 `GraduatedFreeExpressionLogId`。回退仅造句使用阶段（D 档或词汇维 ≤2 → 退回回忆重进 SM-2）；认识/回忆不回退。
- **掌握度阶段派生**（25/50/75/100）：切断两处自评/结果直写——`LearningEndpoints` 的 `ScoreDelta` 掌握度加减（整段删除）、`SpellingService` 的 ±10；EstimatedKnownRate/PersonalDifficulty EMA 保留（接触词排程输入，非掌握度/Score）；Score 三个写入点（测评/挑战/后台造句评分）本就不经自评路径，无需改动。
- **毕业留痕指 FreeExpressionLog**：设计的「SentenceLog」口径落地为自由表达留痕表（自发判定只发生在自由表达通道；指定目标词造句永不毕业）。
- **T-013 回收口径**：`BackgroundJobs` 增 `StartedAt/RetryCount`；worker 每轮先回收——Processing 超 5 分钟（或存量空 StartedAt）重置 Pending（RetryCount+1），超 3 次标记 Failed 留痕。
- **不做 EF 迁移**：新列走 `Patch_PostgreSql_ScoreKernel.sql` 幂等补丁 + 存量映射（RepeatCount≥2 → recalled，掌握度回填）；Development 删库重建。

### 实现
- Domain：`WordLifecycleStage`/`WordQuizMode` 枚举、`UserWordRelationship` 四列、`WordLifecycleService`（推进/回退/毕业/阶段派生掌握度/考察模式与 token 换算）、`BackgroundJob` 两列。
- Infrastructure：`StaleJobReclaimer` + worker 接线（StartedAt 打点）；`SentenceService.RateAsync` 造句证据（确认/回退）；`FreeExpressionService.RateAsync` 自发毕业判定；`LearningPlanService` 候选池优先编排（7 天顺次消耗，confirmed/超带词排除）；`DailyWordSelectionService` 复习词带 stage/quizMode；`SpellingService` 掌握度直写改阶段派生；DbContext 配置 + 补丁 SQL。
- API：`/api/learning/submit` 增 `mode`（回忆模式按 lemma 判对）、响应带 `stage`/`quizMode`；`/api/words/daily` 项带 stage/quizMode。
- 前端（最小适配）：WordCard 回忆模式题面（看义想词、隐藏单词、提交后揭示）、阶段徽标（认识/回忆/会用/毕业）、FeedbackArea 显示阶段；useLearningLog 传 mode。
- 测试：`WordLifecycleTests` 6 个 + `BackgroundJobReclaimTests` 2 个（真实 PG）；实测脚本 `Backend/Scripts/verify-lifecycle-t014.py`。

### 验收（开发自测）
- [x] `dotnet build` 通过；`dotnet test` 143 单测 + 6 集成全过（真实 PG）；`npm run build` 通过
- [x] DashScope（qwen-plus）真实链路实测 8/8（独立库 nextword_verify_t014，验完已删、dev 库未动）：认识 ×2 成熟推进（25→50）、Forgot 自评 mastery/Score 四维不变且 SM-2 interval 重置、回忆通过进候选池（75）、Planner 当日造句目标候选池词在列、每日词带阶段/模式、指定目标词造句 A 档确认但不毕业、自由表达 B 档自发毕业 + 留痕 log id、T-013 超时回收重跑 + 超限 Failed 留痕
- 实测插曲：首轮造句用「decide my English skills」被真实 LLM 判低分触发回退（误用→recalled）——回退规则在真实链路上自然生效；脚本换自然句后全过
- 已知取舍：「待自发」不单列第五阶段（设计四阶段口径），以 `PromptedUseConfirmedAt` 留痕表达；毕业不强制先确认（自发证据更强）；掌握度 25/50/75/100 为阶段映射档位非连续评分

---

## 2026-07-25 — I3 顾言验收（T-007 / T-012）

**验收结论：通过，I3 闭环，愿景 §6 落地路径五项全部完成。**

- 周密验证六项标准全过：三类信号触发/不误触发实测（含 5 组对照）；洞察带真实证据引用（LLM 宁空不编造）；性质变→重规划、未变→零副作用；每周兜底 14 个存量用户全部获新 Plan；未触发零 LLM 双重佐证；单测 131+6、npm build 全过；
- **口径裁定确认**：性质变化「与上一条洞察比对」予以认可——设计的「与 Plan 主攻方向比对」本身不可执行（场景坐标系 vs 瓶颈 7 分类无映射），事件驱动主路径下两者等价，反例成本有界（日幂等封顶）；
- 阈值边界实测与实现常量精确吻合，首版阈值合理，接受现状；
- 不足处置：**T-012**（P1，安全词误触发）已在迭代内修复并复验通过（窗口从 Plan.CreatedAt 起算 + 24h 宽限期，复现场景零副作用、真信号不误杀）；**T-013**（僵尸 Processing 任务回收，P2）为 T-007 前既有基建缺口，每周兜底可兜住，保留 backlog 下轮处理；T-011 维持 backlog 观察；
- 至此愿景 §6-1～6-5 全部落地：内容建设 → 测评重构 → 画像 + Verifier → Planner + 内容切换 → 瓶颈洞察 + 重规划，表达优先架构闭环成型。

---

## 2026-07-25 — I3 T-012：安全词筛查误触发修复

### 需求
- 周密 T-007 验收实测复现：用户原数据正确不触发，但每周兜底下发新 Plan 后，下一轮日筛查用「Plan 生效日 00:00 起」的自由产出（早于 Plan 创建时间）判定新目标词出现率为 0 → 误触发 safe_word 并产出错误洞察 + 一次重规划。

### 决策（顾言定口径）
- 自由产出窗口从 `Plan.CreatedAt` 起算（不是生效日 00:00——Plan 创建前的产出与新目标词无关，不该计入）；
- 新 Plan 给 24h 宽限期（`SafeWordGracePeriod`）：创建未满 24h 不做安全词判定——双重保险，避免窗口内样本过少再误判。

### 实现
- `BottleneckScreeningService.IsSafeWordStrategyAsync`：宽限期提前返回 + 产出过滤条件 `Timestamp >= plan.CreatedAt`（替换原 `planStart = StartDate 零点`）。
- 测试：`BottleneckInsightTests` 新增 3 个（真实 PG）——Plan 创建前的产出不计入（复现案例回归）、新 Plan 24h 内不判、宽限期后目标词 0 出现仍正确触发；既有安全词用例播种改为默认 Plan 创建于 2 天前（越过宽限期）。

### 验收（开发自测）
- [x] `dotnet test` 134 单测 + 6 集成全过（真实 PG）
- [x] 周密复验（2026-07-25）：**通过**。真实 qwen-plus 链路 + 独立库（验完已删、dev 库未动，脚本 `Backend/Scripts/verify-safeword-t012-qa.py`）6/6：复现原误触发场景（产出在前 + 模拟兜底刚下发新 Plan）不再触发、日筛查与每周兜底过后仍零副作用；宽限期边界 23h 不判定；宽限期后真安全词仍触发且洞察落库 SafeWordStrategy；全量测试 134+6 通过

---

## 2026-07-25 — I3 T-007：瓶颈性质洞察 + 重规划触发

### 需求
- 按 `docs/DESIGN-bottleneck-insight.md`（已定稿）：愿景闭环最后一环（§6-5）——指标筛查（平台期/回避模式/安全词策略，规则零 LLM、日级）→ InsightAgent 细读产出原文判瓶颈性质（7 分类 + SentenceLog 证据引用）→ 性质变化事件驱动重规划 + 每周兜底重规划（补 T-006「无测评存量用户不获新 Plan」缺口）。

### 决策
- **新表 `BottleneckInsights`**（走幂等补丁 SQL + Development 删库重建，不做 EF 迁移，枚举存字符串）：性质 + 触发信号 + 中文结论 + 证据引用（SentenceLog id 列表）+ `ReplanTriggered`。
- **「性质是否变化」与上一条洞察比对**（近似设计中的「与当前 Plan 主攻方向比对」——Plan 主攻方向由最近一次洞察驱动，两者等价）：首次发现或性质不同 = 已变 → 重规划；相同 → 仅记录。规则确定、可测、无重规划风暴。
- **重规划绕开同日幂等的做法**：`LearningPlanService.GenerateAsync(force: true)` 同日已有 Plan **原地重建**内容（`(UserId, StartDate)` 唯一不破）；画像重生成走 `WeaknessProfileService.GenerateAsync(assessmentId: null)`，幂等维度从「按测评」扩展为「无测评时按日」。事件驱动入队键 `planner:replan:{userId}:{yyyyMMdd}`，每周兜底 `planner:weekly:{userId}:{ISO 年}-W{ISO 周}`。
- **筛查挂日快照 worker**：`ProfileScoreSnapshotWorker` 快照后对全部完成初测用户跑 `BottleneckScreeningService`（纯规则），触发才入队 `BottleneckInsight` 任务（幂等键 `insight:{userId}:{yyyyMMdd}`）；洞察服务同日幂等（已有当日洞察直接返回，零 LLM）。
- **证据纪律沿用画像**：LLM 返回的 evidenceLogIds 持久化前对照真实 SentenceLog 机械过滤（编造/越权 id 丢弃）； Mock 洞察由信号与真实分数确定性推导，结论带 [Mock] 前缀。

### 实现
- Domain：`BottleneckInsight` 实体、`BottleneckEnums`（Nature 7 分类 / Signal 3 信号 + 线上名）、`BottleneckInsightModels`（请求含产出原文样本 + Plan 主攻方向）、ILLMProvider 第 8 方法 `GenerateBottleneckInsightAsync`、Prompt/解析（枚举 `|` 容错，nature 无法识别整条失败回退 Mock）、Mock 确定性实现。
- Infrastructure：`BottleneckScreeningService`（平台期：斜率≤0.05 且标准差≤0.5 且跨度≤30 天；回避：近 12 样本连接词率后半 ≤ 前半×0.5 且前半 ≥0.3/句；安全词：生效 Plan 目标词在 ≥3 篇自由产出出现率为 0）、`BottleneckInsightService`（细读 + 落库 + 性质比对 + 重规划触发）、`BottleneckInsightWorker`（BackgroundJob 新任务类型）、`WeeklyReplanWorker`（新 HostedService，24h 检查按 ISO 周入队）、`ProfileScoreSnapshotWorker` 挂筛查、`LearningPlanService` force 参数、`WeaknessProfileService` 无测评按日幂等、补丁 SQL 补 `BottleneckInsights`。
- API：`POST /api/insights/bottleneck/jobs`（手动筛查 + 触发入队）、`GET /api/insights/bottleneck/latest`（验收调试只读；用户可见展示是设计明确的非目标）。
- 测试：`BottleneckInsightTests`（真实 PG，9 个：三信号触发/不误触发 ×3、洞察落库证据过滤+性质变→重规划、性质未变→只记录、未触发零 LLM 计数桩、force 原地重建 Plan、每周兜底入队+同周幂等、无测评画像按日幂等、解析容错）。

### 验收（开发自测）
- [x] `dotnet build` 通过；`dotnet test` 131 单测 + 6 集成全过（真实 PG）
- [x] `npm run build` 通过（前端无改动）
- [x] DashScope（qwen-plus）真实链路实测（独立库 nextword_verify_t007，验完已删、dev 库未动，脚本 `Backend/Scripts/verify-bottleneck-t007.py` 可复用）：平台期用户触发 plateau → InsightAgent 真实细读判 GrammarErrors（结论点名 don't/wasn't/has/goes 误用，5 条证据全真实）→ 画像重生成（AssessmentId 空）+ planner:replan force → Plan 落库；同日重复触发复用同一 job 洞察仍 1 行；正常用户（分数爬升+连接词稳定）triggered=false 零洞察零任务；重启后 WeeklyReplanWorker 首轮为全部 assessed 用户入队 planner:weekly force 并处理完成、Plan 原地重建仍 1 行
- 观察：真实 LLM 三次独立用户均判 GrammarErrors（播种数据即语法错误句，判断一致且结论具体到误用点，细读成立）；实测发现脚本竞态——画像重生成与洞察落库同属一个后台任务、在洞察之后完成，脚本需轮询等待（已在脚本内修正）
- [x] 周密验收（2026-07-25）：**通过**。六项标准全过（真实 qwen-plus 链路 + 独立库 nextword_verify_t007_qa，验完已删、dev 库未动；QA 脚本 `Backend/Scripts/verify-bottleneck-t007-qa.py`、`verify-bottleneck-t007-qa-boundary.py` 可复用）：①三类信号触发/不误触发实测；②洞察落库 7 条性质全合法、SentenceLog 证据全部真实（5/5）、结论点名原文误用；③性质变化实测重规划（昨日 VocabularyInsufficient→今日 GrammarErrors：画像重生成+planner:replan force→Plan 原地重建）、性质未变实测只记录零副作用；④每周兜底 14 存量用户全获新 Plan、同周第二轮入队 0、未完成初测用户排除；⑤6 个未触发用户零洞察零任务零画像（零 LLM）；⑥dotnet test 131+6 全过、npm run build 通过
- **口径裁定（重点 A）**：认可「与上一条洞察比对」替代设计的「与 Plan 主攻方向比对」——设计口径依赖的「瓶颈性质（7 分类）↔Plan 主攻方向（场景坐标系）映射」本未定义、无法直接执行；事件驱动主流路径两者结果一致；首条洞察算变化符合设计意图；理论反例（兜底重建使 Plan 已对新性质、洞察历史滞后）至多造成一次有界的多余重规划（日幂等封顶），非正确性问题
- **阈值边界实测（重点 B）**：平台期斜率边界精确落在「末次 3 维+1（0.0409 触发）/4 维+1（0.0545 不触发）」之间，与实现常量一致；回避腰斩 ≤ 含边界（后半=前半×0.5 触发、×0.75 不触发）；安全词 1 篇用目标词即不触发；扁平但跨 36 天、剧烈波动（标准差 1.0）均不误触发
- **不足另开**：T-012（P1，安全词筛查用早于新 Plan 创建的自由产出判定 → 新 Plan 下发后误触发 safe_word，实测复现）、T-013（P2，BackgroundJobWorker 无僵尸 Processing 回收，实测任务卡死致重规划链丢失、ReplanTriggered 名不副实）

---

## 2026-07-25 — I3 T-006 顾言验收

**验收结论：通过。**

- 周密七项标准全过且有实测：主攻场景来自 Verified Finding（构造用户实测 sourceFindingIds 精确匹配，存疑与非法 key 均被排除）；三处内容源 Plan 优先、无 Plan 回退正常；接触词 2/10 全超带、产出/测评无超带；过期回退、同日幂等实测通过；T-010 去重真实链路回归通过；单测 121+6、npm build 全过；
- 不足 T-011（P2）**顾言判定：接受现状**。新用户画像缺场景维度、Planner 走覆盖率兜底是设计允许的冷启动路径；测评情境题与造句留痕都带场景标注，场景 Finding 会随学习行为自然积累，不为此加额外机制。T-011 保留 backlog 观察，不排期；
- 附带修正记录：水平带口径从 IntrinsicScore 改为 CefrLevel（词库多数词无 Intrinsic 标注），与 T-004 测评词池同口径——决策合理，予以确认。

---

## 2026-07-25 — I3 T-006：PlannerWorker + 每日内容来源切换（含 T-010 画像去重）

### 需求
- 按 `docs/DESIGN-planner-worker.md`（已定稿）：夜间 Planner 依已验证画像排 7 日 LearningPlan（主攻场景/每日词队列/阅读推荐/造句目标/生成依据），每日内容从「难度带一刀切」切换为「执行 Plan」；随任务修复 T-010 画像去重。

### 决策
- **新表 `LearningPlans`**（走幂等补丁 SQL + Development 删库重建，不做 EF 迁移）：`(UserId, StartDate)` 唯一 → 同日幂等；内容明细（7 天 × 词队列/接触词/造句目标 + 主攻场景 + 阅读推荐 + 生成依据 Finding id）存 `ContentJson`。
- **水平带用 CEFR 而非 IntrinsicScore**（实测修正）：词库词多数无 IntrinsicScore 标注，legacy 仅 25/50/75 三档，`[score, score+12]` intrinsic 带在 B2 用户实测带内词为 0、计划退化成只有接触词 → 改为 `CefrLevel == 用户带`（与 T-004 测评词池同一口径），带池过薄向下一带补充、绝不超带；接触词 = CEFR 严格高于用户带的词。
- **触发链路复用现有基建**：测评完成 → 评估报告任务处理时入队 Planner 任务（幂等键 `planner:{userId}:{yyyyMMdd}`），`BackgroundJobWorker` 新增任务类型 dispatch；`POST /api/planner/jobs` 手动触发、`GET /api/planner/current` 只读查询。
- **T-010 去重放 Profiler**：提示词加「每维度至多一条、证据不跨条复用」约束；草稿交 Verifier 前 `WeaknessProfiler.Deduplicate` 后处理（同维度留证据强者、证据复用留置信度高者、被剥光证据的整条丢弃），Verifier 职责不变。

### 实现
- Domain：`LearningPlan` 实体、`LearningPlanModels`（Content/Day）、`ILearningPlanService`（GenerateAsync/GetActiveAsync）。
- Infrastructure：`LearningPlanService`（Verified 场景 weakness → 主攻场景，覆盖率兜底；词队列带内+≤20% 超带接触词；阅读按场景选文）、`PlannerWorker` + `BackgroundJobWorker` dispatch + `EvaluationReportService` 触发入队、`DailyWordSelectionService`/`ArticleService`/`SentenceService` 三处 Plan 优先 + 回退、补丁 SQL 补 `LearningPlans`。
- API：`/api/planner/jobs|current`、`/api/articles/recommended`；`DailyWordItem`/`SentencePromptDto` 带 `fromPlan`（词项另带 `isExposure`）。
- 前端：WordDisplay/SentenceCard「来自今日计划」徽标、接触词「认识即可」、短文库「今日推荐」区块。
- 测试：`LearningPlanTests`（真实 PG，6 个：Verified-only+幂等、覆盖率兜底、每日词执行 Plan+接触词上限、过期/无 Plan 回退、造句 Plan 目标、阅读推荐与回退）+ T-010 去重 4 个（纯函数 3 + 全链路 1）。

### 验收（开发自测）
- [x] `dotnet build` 通过；`dotnet test` 121 单测 + 6 集成全过（真实 PG）
- [x] `npm run build` 通过
- [x] DashScope（qwen-plus）真实链路实测（独立库 nextword_verify_t006，验完已删、dev 库未动，脚本 `Backend/Scripts/verify-planner-t006.py` 可复用）：测评 2 块收敛定 B2 → 报告 schemaVersion 2（4 条 Finding 全 Verified、无同维度重复无证据复用）→ Planner 自动触发 → Plan 当日 8 带内+2 接触词、每日词/造句/阅读推荐全部 fromPlan、接触词 2/10 全超带、同日重复触发 LearningPlans 仅 1 行、无画像用户覆盖率兜底出 Plan
- 观察：初测新用户画像无场景 weakness Finding（无学习行为数据，与已知限制一致），真实链路主攻场景走覆盖率兜底；「主攻场景来自 Verified Finding」路径由 LearningPlanTests（真实 PG）覆盖
- [ ] 周密验收（status → testing）

---

## 2026-07-24 — I2 T-005 顾言验收

**验收结论：通过，WeaknessProfile v1 + Verifier 闭环，I2 全部完成。**

- 周密验证六项标准全过：测评后自动生成并持久化（幂等实测不重复生成）；证据引用逐条可回溯且数值与库内一致；Verifier 攻击测试覆盖伪造 id/越权引用/篡改数值/样本量不足/空证据全部标存疑；展示层只呈现已验证条目；失败回退 v1 模板；单测 111+6、npm build 全过；
- 不足记入 T-010（P2，I3 处理）：LLM 偶产同维度重复 Finding、跨 Finding 复用同一证据——Verifier 按设计不拦语义重复，T-006 消费画像前在 Profiler 提示词/后处理去重；
- 至此愿景 §6 落地路径第 3 项（WeaknessProfile v1 + Verifier）完成，I2（测评重构 + 画像）收官；下一轮 I3：T-006 PlannerWorker + 内容来源切换、T-007 瓶颈洞察。

---

## 2026-07-24 — I2 T-005：WeaknessProfile v1 + Verifier

### 需求
- 按 `docs/DESIGN-weakness-profile.md`（已定稿）：测评完成后由 Profiler Agent 生成带证据引用的结构化画像（Finding 五要素：维度/强弱/结论/证据引用/置信度）并持久化；Verifier Agent 机械核查，存疑条目不展示、不进规划输入；评估报告从模板文案切换为已验证 Finding 列表。

### 决策
- **两张新表**（枚举存字符串，走幂等补丁 SQL + Development 删库重建，不做 EF 迁移）：`WeaknessProfiles`（`(UserId, AssessmentId)` 唯一 → 同一测评幂等）+ `ProfileFindings`（EvidenceJson 存证据引用列表）。
- **触发链路复用现有基建**：测评收敛 → `EvaluationReport` 后台任务 → `ProcessJobAsync` 内生成画像（仅 AssessmentId 关联的测评触发；挑战触发不生成）→ 报告 ContentJson schemaVersion 2 = 已验证 Finding 列表；画像失败或全部存疑回退 schemaVersion 1 模板（不阻断报告）。
- **Verifier 不调 LLM**：证据引用存在性（sentence_log 按 UserId 过滤防越权引用）、数值属实（sentence_log 四维分 / assessment_dimension / word_stats / reading_stats，场景与阅读统计由 `WeaknessProfileStats` 单一来源计算并统一两位小数，引用值与重算值同源可比）、置信度样本量（high≥3 / medium≥2 / low≥1 条证据）。
- **LLM 抽象加第 7 方法** `GenerateWeaknessProfileAsync`：真实链路走 `LlmChatClientProvider`（异常回退 Mock）；Mock 产出确定性真实引用（可通过核查）且结论带 [Mock] 前缀。

### 实测修正
- qwen-plus 会把提示词里的枚举白名单原样照抄（`"dimension": "skill|grammar"`），首轮实测全部草稿被解析丢弃、画像 0 条 → 提示词模板改具体占位值 + `LlmResponseParser` 按 `|` 逐 token 容错（有单测）。

### 实现
- Domain：`ProfileFindingEnums`、`WeaknessProfile`/`ProfileFinding` 实体、`WeaknessProfileModels`（EvidenceClaim/Draft/请求响应）、`IWeaknessProfileService/IWeaknessProfiler/IFindingVerifier`、prompt 与解析。
- Infrastructure：`WeaknessProfiler`（聚合 30 条 SentenceLogs + FinalLevel 四维 + 场景词统计 + 阅读统计）、`FindingVerifier`、`WeaknessProfileService`（幂等持久化）、`EvaluationReportService` 接画像、`Patch_PostgreSql_ScoreKernel.sql` 补两表。
- API：`GET /api/profile/weakness`（画像 + 每条 Finding 核查状态与存疑原因）。
- 前端：`LevelPanel` 报告卡优先渲染 findings（维度/置信度徽标 + 结论），无 findings 时回退旧优势/待提升列表。
- 测试：`WeaknessProfileTests`（真实 PG）：生成持久化与幂等、伪造引用/越权引用/篡改数值/样本量不足均标存疑、报告 schemaVersion 2 切换与全存疑回退、解析容错，共 5 个。

### 验收
- [x] `dotnet build` 通过；`dotnet test` 110 单测 + 6 集成全过（真实 PG）
- [x] `npm run build` 通过
- [x] DashScope（qwen-plus）真实链路实测（独立库 nextword_verify_t005，验完已删、dev 库未动，脚本 `Backend/Scripts/verify-weaknessprofile-t005.py` 可复用）：测评（2 块收敛定 B1）→ 报告 schemaVersion 2、5 条 Finding 全部 Verified 且带证据引用、`GET /api/profile/weakness` 一致、DB 落库精确吻合。观察：LLM 会把 expressionScore 当 skill 维度 dimensionKey（不影响核查，后续可在提示词再收紧）
- [ ] 周密验收（status → testing）

---

## 2026-07-24 — I2 顾言验收（T-004 / T-008 / T-009）

**验收结论：通过，I2 测评重构闭环。**

- 周密验证六项验收标准全过：产出题 60% 走真实 LLM 四维评分（SentenceLogs 留痕精确吻合）；自适应 5 类用户实测升降带与收敛正常、≤15 题；词池无超带无 low、阅读题答案位置不恒定；识别分双向不拖级不抬级；单测 105+6、npm build 全过；T-008 修复抽验属实；
- 唯一不足（升带阈值偏严）经实测数据支撑，顾言拍板 65→60 并已修复（T-009），修复后 106+6 全过；
- 观察项（不另开任务）：每块题量固定 5 题未按带微调；测评 e2e 未纳入本轮验收；
- 遗留风险：qwen-plus 对部分低带简单好答案给 0 维分（偏严），后续 WeaknessProfile（T-005）接入真实数据时再观察。

---

## 2026-07-24 — I2 T-009：测评升带阈值校准（65 → 60）

### 背景与决策
- 周密真实链路实测（qwen-plus）：低带中等偏上质量答案块均分 61.3–64.7，摸不到升带阈值 65，升带探测对该分段失效；降带阈值 40 工作正常。顾言拍板：**升带阈值 65 → 60，降带 40 不变**。

### 实现
- `AssessmentScoringService.DecideBandMove` 常量 65→60（含注释说明来源）；单测边界断言同步（60 升带、59.9 保持）；`docs/CURRENT-STATE.md` §5.5 同步。

### 验收
- [x] `dotnet test` 全过（单元 106 + 集成 6）

---

## 2026-07-24 — I2 T-004：测评重构（产出型为主 + LLM 真实评分）+ T-008 词表修复

### 需求
- 按 `docs/DESIGN-assessment-rework.md`（已定稿）：产出型题 ≥60%（提示造句 + 情境表达），识别型（词义选择、阅读理解）降级为参考；产出题全部走 LLM 真实评分；主定级改表达力综合分、废弃最短板 min；分块自适应 2–3 块收敛、总量 ≤15；词池限水平带内 + utility=high/medium；阅读题库内选文、答案位置随机。
- 顺带 T-008：词表 168 词 `example`→`examples`、70 词补音标、`shop around`/`have` 标注修正。

### 决策
- 固定 4 步流程改为块循环：`GET next-block`（幂等重发未提交块）→ `POST blocks/{n}/submit`（同步 LLM 评分，收敛即定级）；块状态全部落在 `AssessmentRecord`（新增 `AdaptiveBlock` 枚举值，枚举存字符串 → 无表结构变化、无需迁移补丁）。
- 每块固定 5 题（2 造句 + 1 情境 + 1 词汇 + 1 阅读），产出恰 60%；情境表达复用 `free expression` 目标词走同一四维评分链路。
- 表达力综合分 = 语法/自然度 0.3 + 词汇/相关度 0.2 加权（0–100）；阈值 [19,34,49,69] 与 ScoreMapping 分带对齐、封顶 C1；块决策 ≥65 升带 / <40 降带；收敛 = 满 2 块且稳定，或满 3 块。
- 档案写入：各维度分数以表达力综合分为初始先验（识别参考分不写权威分，避免 min 拖低），等级外壳由测评主定级显式设定；识别参考分与四维均值/错误标签记入 FinalLevel 记录，供 T-005 WeaknessProfile。
- 顶端带（C1 仅 2 个 high/medium 词）兜底：向下一带补充 + 允许重复用词，绝不超带、不含 low。

### 实现
- Domain：`AssessmentScoringService` 重写（表达分映射/块决策/收敛规则，删除 min 相关与拼写映射）；`AssessmentModels` 新块/结果模型；`BandMove`、`AssessmentStepType.AdaptiveBlock`。
- Infrastructure：`AssessmentService` 重写（出题/评分/自适应/定级）；旧步骤端点替换为 `next-block` + `blocks/{n}/submit`。
- 前端：`useAssessmentFlow` + `InitialAssessment` 改为块循环（最小适配）；e2e spec 同步。
- T-008：`Backend/Scripts/fix-wordlist-t008.py` 幂等修复词表（音标由开发按标准 IPA 给出）。
- 测试：`AssessmentScoringServiceTests` 重写；新增 `AdaptiveAssessmentServiceTests`（真实 PG + 可控 LLM 桩：强用户升带 C1、弱用户降带 A1、稳定用户 2 块收敛、识别全错不拖定级、阅读答案位置不恒定、词池纪律）。

### 验收
- [x] `dotnet build` 通过；`dotnet test` 105 单测 + 6 集成全过（真实 PG）
- [x] `npm run build` 通过
- [x] WordlistSeedTests 规模口径仍达标（T-008 修复后）
- [x] DashScope（qwen-plus）真实链路实测（独立库 nextword_verify_t004，验完已删）：块循环/升/降带/收敛/定级全部真实走通，SentenceLogs 留痕 36 条，好答案四维均分 12.2/20 vs 坏答案 0.8/20，区分度成立。观察：qwen-plus 在低带评分偏严，mention 式好答案也只到 3–4 维分，升带阈值 65 对真实用户可能偏高——留给周密实测校准（不阻断）

---

## 2026-07-23 — I1 T-003：内容建设验证 + 顾言验收

### 验证（周密）
- taxonomy 常量与设计 §2 逐项一致（7 大类 20 子场景，key 与中文名全对）；
- 临时库端到端实测：`GET /api/scenarios`、`GET /api/words?scenario=` 正常；种子灌入 1523 词；`WordScenarios` 落库 2385 行与词表精确吻合（验后删库，dev 库未动）；
- **抽样 100 词人工核对准确率 98%**（≥90% 达标），错例 2 个：`shop around` 误人 core 桶、`have` 违反 §3 原则标了 3 个场景；
- 规模全达标：每子场景 ≥72、core 桶 569、core_verb+connector 52.0%、无 low/无重复/无非法 key；
- 幂等实测：同小时重复触发复用同一 jobId、已标注词按版本跳过、断点续跑有单测覆盖；
- `dotnet test` 84 单测 + 6 集成全过（真实 PG）；前端 `npm run build` 通过。

### 顾言验收结论
**通过，I1 内容建设闭环。** 错例率 2% 在容忍范围内（不钻牛角尖）；愿景对齐确认：core 桶不强塞场景、接触词无静态字段、难度不重复标注。不足记入 T-008（P2，下轮处理）：168 词例句字段单复数不一致导致种子例句丢失、70 词音标为空、2 个标注错例修正。

---

## 2026-07-23 — I1 T-002：Word 场景标注与词库扩充

### 需求
- 按 `docs/DESIGN-scenario-taxonomy.md`（T-001 定稿）落地：7 大类 × 20 子场景 taxonomy；词级 `scenarios`（0–3，0 = core 通用桶）/ `utility`（low 不入库）/ `role` 标注；每子场景 ≥60 有效词 + core ≥500，core_verb+connector ≥40%；LLM 批量标注复用 ReAnnotation worker 模式。

### 决策
- Taxonomy 做成 `ScenarioTaxonomy` 常量集（无管理后台）；子场景关联走 `WordScenarios` 关联表（WordId+ScenarioKey 复合主键）；`Word` 增加 `Utility`/`Role`/`ScenarioAnnotationVersion`（0=未标注）。
- 本轮不做 EF 迁移：PG 由幂等补丁 SQL 补列建表，Development 删库重建。
- 词表来源：环境里有 DashScope key（OpenAI 兼容），`Backend/Scripts/generate-wordlist.py` 真实跑批生成 + `assemble-wordlist.py` 装配（跨场景重复词合并标签、主场景优先）；core 桶 LLM 类目饱和后由开发（LLM）直接补量约 170 词；产物为内置词表 `wordlist-scenarios.json`（嵌入资源、随种子灌库，验收不依赖运行时 LLM）；运行时标注链路由 `ScenarioAnnotationWorker` 承担，Mock 路径可跑通。
- 全局 LLM 配置新增可选 `Llm:OpenAI:BaseUrl`，可接任意 OpenAI 兼容端点。

### 实现
- Domain：`WordUtility`/`ExpressionRole` 枚举、`ScenarioTaxonomy`、`WordScenario` 实体；`ILLMProvider` 第 6 方法 `AnnotateScenarioAsync`（批量）+ Prompt/Parser/Mock/ChatClient/装饰器全链路。
- `ScenarioAnnotationWorker`：分批标注（默认 20/批），按版本号跳过已标注词 → 幂等可重跑、断点可续；LLM 漏标的词留待下轮；整批无进展时防死循环退出。
- 端点：`GET /api/scenarios`（taxonomy + 各场景词数 + core 桶词数）、`POST /api/scenarios/annotation-jobs`（按小时幂等触发）、`GET /api/words?scenario=` 过滤、`WordDto` 带场景标注、`POST /api/words` 自动入队标注。
- 单测：taxonomy、解析器容错、Mock 启发式、worker 幂等/续跑（真实 PG）、词表验收口径（规模/占比/合法性）。

### 验收
- [x] `dotnet build` 全量通过
- [x] `dotnet test` 84 全过（含词表规模/占比守护、worker 幂等续跑、解析器容错）
- [x] 真实链路实测：DashScope（OpenAI 兼容端点）下批量标注 job + 新词自动标注落库可查
- [x] 种子端到端实测：全新 PG 库灌入 1520 词，`GET /api/scenarios` 返回每子场景最少 72、core 桶 569
- [ ] 标注质量人工抽检（验收 2，移交周密 T-003）

---

## 2026-07-20 — 文档集整理与本地开发代理修复

### 实现
- 新增根 `README.md`（功能、技术栈、快速启动、配置、测试）
- 重写 `docs/CURRENT-STATE.md`：对齐 React Router、全量 PostgreSQL、Score 内核 v1、Base UI 前端等现状
- 删除已完成/被取代的文档：`plans/` 全部 Phase 计划、`docs/SPEC-ai-learning-*`（4 份）、`docs/DESIGN-frontend-ux.md`、`docs/AUDIT-cefr-read-path.md`、`docs/superpowers/`、`英语学习.md`；活口待办并入 `next-steps.md`
- 保留 `docs/DESIGN-ai-learning-architecture.md`（架构 why）与 `docs/DESIGN-auth-profile.md`，补「已实现」状态标注
- 修复 `Frontend/vite.config.ts` 代理端口漂移：8080 → 默认 5108（可用 `VITE_API_PROXY_TARGET` 覆盖）

### 验收
- [x] 后端启动（`dotnet run`，:5108）：注册/登录、每日词、文章、profile、progress、匿名 401 全部实测通过
- [x] 前端 `npm run dev`（:5173）页面与 /api 代理实测通过

---

## 2026-07-19 — UI checkpoint

### 实现
- `front_design/` 7 个屏幕与 `Frontend` 同步（设计原型与实现并行维护）

---

## 2026-07-10 — fix：PG Score 内核补丁缺失列

### 问题
`AddScoreKernelM1` 在 PG 上无法走 EF 迁移，补丁曾标记已应用但未创建 `WordDifficultyAnnotations.DimensionsJson` 等列，导致阅读查词失败。

### 实现
- `Patch_PostgreSql_ScoreKernel.sql` 补齐缺失列（幂等）

---

## 2026-07-08 — 阅读查词例句与重点词汇增强

### 需求
- 查词/重点词：结构化双例句（文中场景 + 其他场景）+ 精髓说明，UI 点击「查看例句」展开
- 文章级缓存：先查 `ArticleVocabMappings`，缺失再 LLM 并 upsert
- 重点词汇提取：音标 + 用法例句持久化；存量数据按需 lazy backfill

### 实现
- `WordExample` 模型；`ArticleVocabMapping.Phonetics` / `ExamplesJson`
- Prompt/Parser/Mock 结构化 examples；vocab extract 含 phonetics + usageExample
- `ArticleVocabService.GetOrCreateWordDetailAsync` 统一缓存；`ReadingLookupService` 使用 `ArticleId`
- 扩展 `ReadingLookupResponse`、`ArticleVocabMappingDto`；前端 WordPopover / VocabExtractPanel
- Migration `20260708161532_AddArticleVocabPhoneticsAndExamples` + Upgrade/Rollback SQL

### 验收
- [x] `dotnet test` 54 通过
- [x] `npm run build` 通过

---

## 2026-07-08 — Base UI 前端重构 + Score 日快照 + PG schema 补丁

### 需求
按 `2026-07-07-base-ui-frontend-redesign` spec（已随文档整理删除，内容已全部落地）重构前端：`@base-ui/react` 组件封装、黑白主题、沉浸式首次测评 Onboarding + 跳过测评。

### 实现
- 前端：`@base-ui/react` 封装层（`src/components/ui/`：Button/Dialog/Drawer/Select/Switch/Tabs/Badge/Progress/RadioGroup）、黑白主题 tokens、底部导航精简为「首页/我的」
- 首次测评沉浸式 `OnboardingLayout` + `POST /api/assessment/initial/skip`（可跳过，默认 A2）
- 后端：`ProfileScoreSnapshotWorker`（Score 每日快照）+ `GET /api/profile/scores/history`
- `PostgreSqlSchemaPatcher` + `Patch_PostgreSql_ScoreKernel.sql`：PG 上幂等补齐 Score 内核 schema
- `ApplicationDbContextFactory`（design-time Npgsql）

---

## 2026-07-06 — 全环境切换 PostgreSQL（移除 SQLite）

### 需求
开发、默认、生产环境统一使用 PostgreSQL，不再使用 SQLite。

### 实现
- `appsettings.json` / `appsettings.Development.json`：`Database:Provider` → `PostgreSql`，移除 `Sqlite` 连接串
- `DependencyInjection.cs`：固定 `UseNpgsql`，移除 SQLite 分支；保留 `PendingModelChangesWarning` 忽略（模型快照与历史迁移仍有差异）
- 移除 `Microsoft.EntityFrameworkCore.Sqlite` 包引用
- 新增 `appsettings.Testing.json`（集成测试库 `nextword_test`）
- 单元/集成测试改为 PostgreSQL；docker init 脚本创建 `nextword_test` / `nextword_unit_test`
- 测试库 bootstrap：`EnsureCreated` + 首次运行重建库

### 验收
- [x] `dotnet build` 通过
- [x] `dotnet test` 45 通过（需 `docker compose up -d postgres`）

### 本地开发
```powershell
docker compose up -d postgres
cd Backend/NextWord.Api
dotnet run
```
Development 启动时自动 `MigrateAsync()` 到 `nextword` 库。

---

## 2026-07-06 — SQLite 排序修复 + 生产 SQL 迁移流程

### 需求
- 开发环境：`BackgroundJobWorker` SQLite 不支持 `DateTimeOffset` SQL ORDER BY
- Docker/生产：`PendingModelChangesWarning`（Snapshot 被 `AddScoreKernelM1` 污染为 SQLite 类型）
- 生产 Schema 改为 SQL 脚本迁移，发布前本地抽取

### 实现
- 7 个文件改为先 `ToListAsync()` 再内存排序（BackgroundJob / Challenge / Spelling / LearningTool / Endpoints）
- `ApplicationDbContextFactory`（Design-Time 默认 Npgsql）
- EF 迁移 `20260706132041_AlignPostgresModelSnapshot`（TEXT → uuid/timestamptz 等）
- `Scripts/generate-migration-sql.ps1` → 输出 `Scripts/Migrations/Upgrade_Idempotent.sql`
- `Program.cs`：仅 Development 自动 `MigrateAsync()`；Production 走 SQL
- `Scripts/Migrations/README.md` 发布 runbook

### 验收
- [x] `dotnet ef migrations has-pending-model-changes`（PostgreSql）→ No changes
- [x] `dotnet test` 45 通过
- [x] `Upgrade_Idempotent.sql` 已生成

### 生产部署
1. `.\Backend\Scripts\generate-migration-sql.ps1`
2. `psql "$DATABASE_URL" -f Backend/Scripts/Migrations/Upgrade_Idempotent.sql`
3. 可选：`Upgrade_ScoreBackfill.sql`
4. 部署 API（Production 不自动 Migrate）

---

## 2026-06-30 — Score 内核 v1 批量落地

### 需求
按 `docs/SPEC-ai-learning-*` 一次性完成 Layer 0–5：Score Profile 内核、初测/挑战服务端计分、阅读查词 AI、每日词 Score 驱动、评估报告、前端 Score 展示；FR-6 选项 A（真实挑战 UI + 服务端阈值）。

### 后端
- M1 迁移 `AddScoreKernelM1` + `AddChallengeSession` 已 apply
- `ScoreMappingService` / `ScoreProfileService`（唯一写入路径）/ `EffectiveDifficultyCalculator`
- `AssessmentService.CompleteInitialAsync` → Profile 更新 + 评估/造句 LLM 任务入队
- `ChallengeService` 重写：`ChallengeSession` 存包，客户端提交原始答案，服务端计分
- `ReadingLookupService`、`DailyWordSelectionService`、`EvaluationReportService`（模板报告）
- `BackgroundJobWorker`、`SentenceLlmScoringWorker`
- `DuckDuckGoSearchService` + `LearningToolRegistry`（7 handlers）+ `/api/tools`
- 学习提交 `ApplyKnownRateEma` 更新 `EstimatedKnownRate` / `PersonalDifficulty`

### 前端
- `types/score.ts`、`useProfileScores`、`useEvaluationReport`
- `ChallengeMode` 三阶段 UI，提交原始答案（无客户端 correctIndex）
- `LevelDashboard` Score 维度 + 评估报告轮询
- `InitialAssessment` 定级结果展示 Score
- `useWordLookup` → `POST /api/reading/lookup`；`WordPopover` 熟悉度
- `AppShell` 侧栏 CEFR + Score；每日词 count=10

### 验收
- [x] `dotnet test` 39 unit + 6 integration 通过
- [x] `npm run build` 通过
- [x] `dotnet ef database update`（含 ChallengeSessions）

### 未完成 / 已知缺口
- T-005 staging backfill drill 实际执行（脚本与 README 已就绪）
- 评估报告 LLM 结构化（已有 toolPrefetch 字段）
- Annotation lookup singleflight
- Release Blockers B1–B8 正式 sign-off

---

## 2026-06-30 — Score 内核收尾（T-043 / FR-7 / E2E）

### 实现
- `ProfileScoreSnapshotWorker` 日批 + `GET /api/profile/scores/history`
- `ReAnnotationWorker` + `UserFeedbackService`（DefinitionWrong / MarkKnown / ExcludeWord）
- `EvaluationDataAssembler` 预取工具数据写入报告
- 前端 `FeedbackButton`、`useDisplaySettings` CEFR toggle
- E2E `challenge.spec.ts`；CEFR read-path audit；`Scripts/README_BackfillDrill.md`

### 验收
- [x] `dotnet test` 45 通过
- [x] `npm run build` 通过

---

## 2026-06-27 — AI 学习架构叙事归档

### 背景
产品方向讨论：从五项 AI 体验需求（评价化等级、Agent 工具、DuckDuckGo、AI 每日词、阅读查词 AI）出发，梳理定级机制，进而重估 CEFR 在系统中的角色。

### 决策记录
- **定级与评价不冲突**：规则引擎产出权威 Score/等级；LLM 产出叙事评价，不改定级结果
- **CEFR 降级为映射层**：内部以 DifficultyScore (0–100) + User Profile 为核心；CEFR 仅展示与互操作
- **AI 判官 + CEFR 翻译官**：AI 负责标注、解释、辅导；规则负责 SM-2、升级、复习
- **收紧原则**：AI 标注持久化（非全链路实时）；区分 intrinsic / personal difficulty；规则引擎不可省略

### 产出
- 新增 `docs/DESIGN-ai-learning-architecture.md`（完整来龙去脉、理想分层、待决问题）
- §10 待决问题随后在产品规格中全部锁定，并由 Score 内核 v1（06-30）实现

---

## 2026-06-26 — 前端 UX 换皮 P3：React Router + 挑战历史

### 实现
- `react-router-dom`：`BrowserRouter` + `Routes` 替代 `view` state
- `navigation/routes.ts`：路径映射（词库 `/word-bank`，阅读 `/reading/:articleId`）
- `AppShell` 改用 `useLocation` / `useNavigate`
- `ArticleReaderRoute`：`useParams` 包装阅读页
- `ChallengeRecentList` 挂到挑战页空闲态，调用 `/api/challenge/recent`
- 未完成初测时 `navigate('/assessment', { replace: true })`
- E2E：`helpers.ts` 注册登录 + API 跳过初测；Vite 代理改为 `:5108`

### 验收
- [x] `npm run build` 通过
- [x] `npm run test:e2e` 3/3 通过

---

## 2026-06-25 — 句子评分中文反馈 + 测评流程导航优化

### 实现
- `Llm:SentenceRating:ExplanationLanguage` 配置：error_analysis 与 suggestion 按指定语言（默认 zh-CN）输出
- `ExplanationLanguageHelper`；Mock Provider 同步支持
- Initial Assessment 各阶段独立题目索引，Timeline 步骤跳转改进

---

## 2026-06-24 — 认证与个人中心

### 需求
邮箱注册/登录、JWT 认证、个人主页、用户级 LLM 配置（BYOK）。

### 实现
- `User` 扩展 Email/PasswordHash（PBKDF2-SHA256）；`UserLlmSettings` 1:1
- JWT HS256 发 token；全站授权 FallbackPolicy，匿名仅健康检查与注册/登录
- OpenAI/DeepSeek/Qwen 预设；`IUserLlmProviderFactory` 按用户构建 provider
- 前端 AuthContext + 登录页 + ProfilePage（等级、统计、LLM 设置）
- 设计文档：`docs/DESIGN-auth-profile.md`

---

## 2026-06-24 — 主导航重构与首次测评自动引导

### 需求
1. 测评、挑战、词库移至「我的」菜单
2. 其余功能以卡片形式展示在主界面（登录后默认首页）
3. 未完成首次测评时自动进入测评流程（取代黄色引导横幅）

### 实现
- 新增 `Dashboard.tsx` 卡片首页（学习、拼写、造句、阅读、等级、复习、进度）
- `App.tsx` 精简顶栏为「返回首页」+「我的」；默认视图改为 dashboard
- `ProfilePage` 增加「更多功能」区块（测评、挑战、词库）
- `InitialAssessment` 支持 `autoStart` 与 `onComplete` 回调
- 移除 `OnboardingBanner` 使用；进度加载完成前显示加载态避免首页闪烁

### 验收
- [x] npm run build 通过

---

## 2026-06-19 — Phase 6 生产增强

### 需求
Redis 缓存、docker-compose 生产栈、LLM 遥测、EF snapshot/迁移对齐、Playwright E2E、升级候选横幅。

### 决策
- Cache:Provider 切换 Memory / Redis，应用层仍用 ICacheService
- LLM 链：Inner → LlmRetryProvider → LlmTelemetryProvider
- 合并 hand-written Phase3/5 迁移为正式 EF 链 `Phase6AssessmentAndWorkersSync`
- SQLite 不兼容的 OrderBy(DateTimeOffset/Guid.NewGuid) 改为内存排序

### 实现
- RedisCacheService + StackExchangeRedis DI
- LlmTelemetryProvider（耗时 + ProfileId 日志）
- docker-compose：postgres + redis + api（PostgreSql + Redis 缓存）
- ApplicationDbContextModelSnapshot 补齐 Assessment 实体
- Playwright：reading + assessment E2E（2 用例通过）
- UpgradeCandidateBanner 前端
- Worker SQLite 修复；Host BackgroundServiceExceptionBehavior=Ignore
- CommentService / AssessmentService SQLite 查询修复
- useAssessmentFlow POST 补 `{}` body
- 单元测试 +3（RedisCache、LlmTelemetry）

### 验收
- [x] `dotnet test` 15/15 通过（单元 12 + 集成 3）
- [x] `npm run build` 通过
- [x] `npm run test:e2e` 2/2 通过

---

## 2026-06-19 — Phase 5 集成测试 + 引导 + 后台任务

### 需求
落实 next-steps P0：集成测试、首次测评引导、复习/等级后台 Worker。

### 实现
- UserProgress 增加 PendingReviewCount、IsUpgradeCandidate
- ReviewReminderWorker（6h）、LevelCheckWorker（24h）
- Progress API 返回 hasCompletedInitialAssessment / isUpgradeCandidate / pendingReviewCount
- NextWord.IntegrationTests：Article + Assessment 共 3 用例
- 前端 OnboardingBanner 引导未完成初测用户

### 验收
- [x] `dotnet test` 12/12 通过（单元 9 + 集成 3）
- [x] npm run build 通过

---

## 2026-06-19 — Phase 4 完善与优化

### 需求
缓存层、LLM 重试、单元测试、Docker 部署、HealthChecks、前端错误边界。

### 实现
- ICacheService + MemoryCacheService（开发环境）
- LlmRetryProvider 装饰 ILLMProvider（指数退避 3 次）
- NextWord.UnitTests：Sm2、AssessmentScoring、LevelUpgrade（9 用例通过）
- Dockerfile + docker-compose.yml
- /api/health/details HealthChecks
- ErrorBoundary、LoadingSkeleton 组件

### 验收
- [x] `dotnet test` 9/9 通过
- [x] dotnet build 通过
- [x] npm run build 通过

---

## 2026-06-19 — Phase 3 测评与挑战

### 需求
5 步初测、挑战测评、等级升降、等级历史与前端测评流程。

### 决策
- 测评编排由确定性 AssessmentService 完成，LLM 仅用于造句（复用既有能力）
- 短板定级：overall = min(vocab, sentence, reading)
- 挑战包预生成（ChallengePackGenerator）
- AssessmentRecord 用 JSON 存题目/答案/分数

### 实现
- Assessment / AssessmentRecord / ChallengeRecord / LevelHistory 实体与迁移
- AssessmentScoringService、LevelUpgradeEngine、ChallengePackGenerator
- API：/api/assessment、/api/challenge、/api/level
- 前端：InitialAssessment、ChallengeMode、LevelDashboard

### 验收
- [x] dotnet build 通过
- [x] npm run build 通过

---

## 2026-06-19 — Phase 2 阅读模块

### 需求
实现短文阅读器、点击查词、LLM 重点词汇提取、段落评论、阅读日志与阅读辅助 Agent。

### 决策
- 短文逐词 React 渲染以支持 onClick 查词
- 查词优先读 ArticleVocabMappings 缓存
- 阅读辅助 Agent 通过 ReadingSkillRegistry + ReadingAssistantAgent 组合 skills
- LLM 调用统一扩展 ILLMProvider（ExtractVocab、ReplyToComment）

### 实现
- 新增 Article / ReadingLog / ArticleComment / ArticleVocabMapping 实体与迁移
- 内置 21 篇分级短文种子数据
- API：articles CRUD、vocab-extract、lookup、comments、reading-logs、reading/agent
- 前端：ArticleLibrary、ArticleReader、查词弹层、词汇面板、评论线程

### 验收
- [x] dotnet build 通过
- [x] npm run build 通过
- [x] 21 篇内置短文
- [x] 阅读主流程不依赖真实 LLM（Mock 降级）
