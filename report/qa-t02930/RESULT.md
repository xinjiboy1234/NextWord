# QA 验收：T-029 / T-030（轻量，低风险）

- 验收人：周密（测试经理）
- 日期：2026-08-06
- 代码：worktree `.worktrees/t-029-30-ui-polish`，分支 feat/t-029-30-ui-polish
- Commit：ccb6681（T-029 Dashboard 骨架/空状态）、eab3d9e（T-030 文案泄露清理）

## 结论：通过

## 验证项与证据

1. **diff 审查**：两 commit 均最小改动、无夹带。
   - ccb6681：仅 Dashboard.tsx（loading 骨架 + 探索周空态 + 空态行动建议）+ 文档/tasks；
   - eab3d9e：后端 3 文件（LogEndpoints 结果中文化、LlmPromptFactory 场景中文名、EvaluationReportService 同分不评强弱 + 去 "Overall"）+ 前端 6 文件（登录错误区分、hasCompleted、LevelPanel/ArticleLibrary 中文标签、ReviewQueue 评分档图例）+ 2 个新单测 + 文档/tasks。

2. **场景中文名同源（T-030 核心风险点）**：
   - `LlmPromptFactory.cs:21` 仅改 prompt 输入（`ScenarioTaxonomy.Find(key)?.ZhName ?? key`，taxonomy 未收录回退原 key）；
   - Mock（`LlmMockProvider.RateSentenceAsync`）直接读 `SentenceRatingRequest` 结构体字段，不解析 prompt 文本——不受影响；
   - 响应解析路径不消费 prompt 中的 Scene 行——无破坏；
   - 新增 2 单测（directions→问路导航、未知 key 回退）随单测全量通过。

3. **登录错误区分（LoginPage.tsx）**：`axios.isAxiosError` 取 status，`undefined`（网络错误）或 `>=500` →「无法连接服务器」，4xx → 凭证/邮箱提示。分支正确。

4. **老用户测评文案**：`App.tsx:129` 传 `progress?.hasCompletedInitialAssessment ?? false`，字段存在于 `types/models.ts:49`；InitialAssessment 按 hasCompleted 切换标题/提示/按钮文案。链路正确。

5. **真实链路抽查**（API @5191、Development、独立新库 nextword_qa_t02930、qwen-plus）：
   - 注册 OK → 跳过初测 → POST /api/sentences/rate（scene=directions, target=lost）→ 200；
   - 反馈为中文（见 rate-response.json）：errorTags 写「问路场景」「导航语境」，suggestion 全中文；
   - **未出现场景 key 'directions' 外露**（文中 'ask for directions' 为正当英文短语建议，非 key 泄露）；响应 DTO 的 `scene` 字段回显请求原值属日志记录，非反馈文案。
   - 插曲：首次 401 invalid_token 系手工粘贴 token 出错，脚本提取 token 后正常，非产品缺陷。

6. **构建与测试复跑**：
   - `dotnet test NextWord.UnitTests`：209 通过 / 0 失败；
   - `dotnet test NextWord.IntegrationTests`：6 通过 / 0 失败（开发自报 209+6 属实）；
   - `npm run build`：通过（541 kB chunk 警告为既有现象）。

## 清理

- API 进程已杀（5191 无监听）；`nextword_qa_t02930` 已 DROP。

## 不足 / 备注

- 登录错误区分、hasCompleted 文案为代码级审查，未做浏览器端 UI 实测（低风险，文案逻辑简单）；
- EvaluationReportService 同分分支无新增单测覆盖，仅代码审查确认（低概率路径，可后续补测）；
- 前端 chunk >500 kB 警告既有存在，与本任务无关。
