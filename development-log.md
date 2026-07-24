# NextWord 开发日志

按时间倒序记录需求、决策、实现与验收。

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
