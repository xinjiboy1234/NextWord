# NextWord「表达优先」迭代小组

> 成立日期：2026-07-21
> 使命锚点：[docs/VISION-expression-first.md](../docs/VISION-expression-first.md)

## 使命

按愿景文档的修正路径，把 NextWord 从「仪表盘造好了」补成「司机也造好」：
以**表达能力**为核心，建成 `画像 → 验证 → 规划 → 内容 → 洞察` 的闭环。

## 成员

| 角色 | 姓名 | Profile |
|---|---|---|
| 产品经理 | 顾言 | [profiles/pm-guyan.md](profiles/pm-guyan.md) |
| 软件开发经理 | 程实 | [profiles/dev-chengshi.md](profiles/dev-chengshi.md) |
| 测试经理 | 周密 | [profiles/qa-zhoumi.md](profiles/qa-zhoumi.md) |

使用方式：在会话中点名成员（如「以顾言的身份设计 X」「让周密验收」），
即加载对应 profile，按该角色的职责、关注点与原则工作。

## 工作循环

1. **顾言（产品）**：对齐愿景与核心用户需求 → 提出设计方案（易用性、创新性、趣味性）→ 任务登记入 `tasks.csv`；
2. **程实（开发）**：实现需求，关注性能、安全性、迭代扩展性；
3. **周密（测试）**：细心测试验证；**顾言参与验收**，纠正与愿景的偏移；
4. 本次不足与偏移记录进 `tasks.csv`，作为下一轮迭代需求；
5. 循环往复，直到符合需求为止。

## 共同原则

- **不钻牛角尖**：基本符合即可上线，完美是迭代的敌人；
- **互相搭手**：任何人都可以请其他成员协助，以提升效率；
- **任务透明**：任务状态统一用 `team/tasks.csv` 管理，口头任务不算数；
- **愿景锚定**：需求与验收标准以 VISION-expression-first.md 为准绳——固定等级只是外壳，详细的画像与评价才是内核。

## tasks.csv 约定

| 列 | 取值 |
|---|---|
| id | T-NNN，递增 |
| iteration | I1 / I2 / I3 …（哪一轮迭代） |
| owner | pm / dev / qa |
| status | backlog / in-progress / testing / done / deferred |
| priority | P0 / P1 / P2 |
| source | 需求出处（如 VISION §6-1、I1 验收不足记录） |

规则：

- 字段内不用英文逗号，用中文标点代替；
- 每轮迭代验收后，由顾言把「不足记录」整理为新行（source 记「I{N} 验收不足」）；
- 状态变更即时更新 `updated` 日期。
