# AGENTS.md — NextWord 协作规则

> 本文件约束 AI 代理在本仓库中的工作方式。2026-07-22 起生效。

## 1. 项目方向（愿景锚定）

NextWord 已从「词汇学习工具」修正为「**以表达能力为核心**」的 AI-native 学习产品。
一切需求与验收以 [docs/VISION-expression-first.md](docs/VISION-expression-first.md) 为准绳：

- **固定等级/分数只是给用户看的外壳；真正的内核是详细的画像与评价**（WeaknessProfile → 交叉验证 → 定制内容 → 瓶颈洞察 → 重规划）。
- 规则引擎拥有分数权威，AI 不直接改分；Agent 负责解读、验证与规划。
- 难度策略分层：背词允许掺入「接触词」（只要求认识），产出任务只用水平带内的词，阅读靠查词机制应对，测评词池严格控难度。
- 实现现状参考 [docs/CURRENT-STATE.md](docs/CURRENT-STATE.md)，待办见 [next-steps.md](next-steps.md)。

## 2. 团队角色与工作循环

本仓库按「表达优先」迭代小组（见 [team/README.md](team/README.md)）的方式运作：

| 角色 | 姓名 | Profile |
|---|---|---|
| 产品经理 | 顾言 | [team/profiles/pm-guyan.md](team/profiles/pm-guyan.md) |
| 软件开发经理 | 程实 | [team/profiles/dev-chengshi.md](team/profiles/dev-chengshi.md) |
| 测试经理 | 周密 | [team/profiles/qa-zhoumi.md](team/profiles/qa-zhoumi.md) |

**工作循环**：

1. **顾言（产品）**：对齐愿景与核心用户需求 → 提出设计方案（易用性、创新性、趣味性）→ 任务登记入 `team/tasks.csv`；
2. **程实（开发）**：实现需求，关注性能、安全性、迭代扩展性；
3. **周密（测试）**：细心测试验证；**顾言参与验收**，纠正与愿景的偏移；
4. 本次不足与偏移记录进 `team/tasks.csv`（source 记「I{N} 验收不足」），作为下一轮迭代需求；
5. 循环往复，直到符合需求为止。

**使用方式**：在会话中点名成员（如「以顾言的身份设计 X」「让周密验收」），即加载对应 profile，按该角色的职责、关注点与原则工作。

## 3. 共同原则

- **不钻牛角尖**：基本符合即可上线，完美是迭代的敌人；
- **互相搭手**：任何人都可以请其他成员协助，以提升效率；
- **任务透明**：任务状态统一用 `team/tasks.csv` 管理，口头任务不算数；
- **最小改动**：改动限定在任务涉及的范围内，不顺手重构无关代码。

## 4. tasks.csv 约定

| 列 | 取值 |
|---|---|
| id | T-NNN，递增 |
| iteration | I1 / I2 / I3 …（哪一轮迭代） |
| owner | pm / dev / qa |
| status | backlog / in-progress / testing / done / deferred |
| priority | P0 / P1 / P2 |
| source | 需求出处（如 VISION §6-1、I1 验收不足） |

规则：

- 字段内不用英文逗号，用中文标点代替；
- 状态变更即时更新 `updated` 日期；
- 开始任何任务前先看 `team/tasks.csv` 确认任务归属与状态。

## 5. 工程基线

- 后端：.NET 10，`cd Backend && dotnet test`（需 `docker compose up -d postgres`）；
- 前端：React 19 + Vite，`cd Frontend && npm run build`；E2E 为 `npm run test:e2e`；
- 交付验收标准：对应测试通过 + 构建通过，测试经理确认后方可标记 `done`。

### 数据库迁移纪律

- 修改实体或 `ApplicationDbContext` 后，**必须立即用 `dotnet ef` 命令生成一次迁移**（如 `dotnet ef migrations add <Name>`），不要攒多次实体变更再一次迁移——攒太多容易生成错误的大迁移；
- **不手工修改迁移命令生成的文件**；生成结果有问题就回退后调整实体重新生成；
- **当前迭代例外（T-015 已收口）**：I1–I4 期间暂不做迁移的例外已由 `ConsolidateI1ToI4Schema` 迁移收口——I1–I4 全部 schema 变化进入迁移链，空库 `MigrateAsync` 一次建全 + 种子可跑；`Patch_PostgreSql_ScoreKernel.sql` 幂等补丁保留，作为存量 dev/prod 库的升级路径（与迁移幂等共存）。该迁移的 PG 幂等守卫分支为「不手工修改迁移文件」纪律的一次性例外；此后实体变更恢复上面的正常迁移纪律。

## 6. 注释、文档与提交纪律

### 代码注释与文档落地

- 代码注释必须随代码同步更新：改动行为时，同处过时的注释、docstring 一并修正，不留描述旧行为的注释；
- 项目文档（`docs/CURRENT-STATE.md`、`development-log.md`、`next-steps.md`）与代码同步维护：功能落地、结构调整、约定变更时，在同一任务内更新对应文档，不积压到以后；
- `development-log.md` 按时间倒序记录每轮迭代的需求、决策、实现与验收结果。

### 分支与提交颗粒度

- **一个任务（tasks.csv 中的一行）对应一次提交**，提交信息中关联任务 id（如 `T-002: ...`）；
- 不攒批量修改：发现与当前任务无关的问题时，记入 `tasks.csv` 另开任务，不顺手混进本次提交；
- 单次提交保持可独立审查：构建通过、对应测试通过，不把「实现一半」的状态提交进主干；
- 较大任务按任务拆分分支（worktree/branch），完成并验收后合回，避免主干长期挂着未完成的改动；
- 提交前自查：diff 里是否只有本任务的内容？文档是否已同步？测试是否通过？
