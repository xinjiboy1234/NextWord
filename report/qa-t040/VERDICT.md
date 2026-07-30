# T-040「多词短语命中口径」验收报告（周密）

- 被验：worktree `.worktrees/t-040-phrase-lemma`，分支 feat/t-040-phrase-lemma，commit 57fc5bf
- 验收时间：2026-08-06
- 环境：worktree 起 API（Development，端口 5193，独立库 nextword_qa_t040，LLM DashScope qwen-plus）
- 验收结论：**通过（accept）**

## 一、diff 审查（9 文件 +224/-15，最小改动确认）

- `TargetWordMatcher.cs`（新增 76 行 Domain 纯函数）：单词词边界匹配；多词连续子序列匹配；`Tokenize` 按小写字母序列切词，标点/空白/数字皆分隔符。逻辑与注释一致，无词形变换（有意为之）。
- 三处调用点改动均为「tokens 集合 → 原句文本 + TargetWordMatcher.IsHit」的直替：`WordLifecycleService.IsPromptedUseCorrect/IsPromptedUseMisuse`（入参签名变更）、`SentenceService.ApplyLifecycleEvidenceAsync`、`FreeExpressionService.GraduatedSpontaneousUseAsync`。无夹带改动。
- `BottleneckScreeningService.Tokenize`（T-033 内容词口径）**未被误改**：本 commit 未触碰该文件，Tokenize 仍为 internal 自用（安全词窗口/复杂连接词/平均句长三处内部调用），两套口径并存成立。
- 测试净增 7 例与提交说明一致（纯函数 5 + 真实 PG 2）；development-log.md / CURRENT-STATE.md / tasks.csv 同步更新。

## 二、真实链路实测证据（详见本目录各证据文件）

用户 qa.t040@example.com，注册→跳过初测（defaultLevel A2）→SQL 置 4 词 PromptedUse 未确认（give up / up in arms / arm / achieve，后补 look forward to）。

| # | 场景 | 操作 | 结果 | 判定 |
|---|---|---|---|---|
| a | 造句含短语 | "I will never **give up** learning English..." → LLM 评 **A** | `PromptedUseConfirmedAt=2026-08-06 10:13:15` 留痕（a-sentence-rate.json / a-confirm-state.txt） | ✅ |
| b | 自由表达自发含短语 | "...told myself not to **give up**..." → LLM 评 **B** | 响应 `graduatedWords:["give up"]`；DB `LifecycleStage=SpontaneousUse`、`MasteryScore=100`、`GraduatedFreeExpressionLogId=3da51244`（= 该次 log id）（b-free-rate.json / b-graduate-state.txt / final-state.txt） | ✅ |
| c1 | 乱序 | "The soldiers put their **arms up in** surrender."（三词全在、逆序）→ D 档 | 不确认、**也未误回退**（仍为 PromptedUse/50 分）（c1 / c-negative-state.txt） | ✅ |
| c2 | 插词 | "climbed **up and in** his **arms**..." → D 档 | 不确认不回退 | ✅ |
| c3 | 子串 | 目标 arm，句中 "The **armed** guard..." → D 档 | 不确认不回退（词边界生效） | ✅ |
| c7 | 自由表达插词负例（决定性） | "I **look forward eagerly to** every lesson..." → **B 档**（达毕业线） | `graduatedWords:[]`，stage 仍 PromptedUse —— 排除「评分不达标」干扰，纯命中口径不命中 | ✅ |
| c8 | 同词正对对照 | "I **look forward to** the family dinner..." → B 档 | `graduatedWords:["look forward to"]`，SpontaneousUse/100 —— 同一词、同端点、同档位，仅差一个插词，对照严密 | ✅ |
| d | 单词正向回归 | "She worked very hard to **achieve** her dream..." → B 档 | achieve 确认留痕（d-achieve-rate.txt / final-state.txt） | ✅ |

补充说明：c1–c3 三例 LLM 均评 D 档，恰好形成额外强证据——未命中时既不确认也**不触发使用错误回退**（符合「不含目标词算回避」的既有设计）。自由表达乱序负例尝试了 3 段（c4/c5/c6）LLM 均只给 C 档，无法隔离命中口径，故改用 c7 插词负例完成决定性验证。

## 三、验收标准逐条核对

1. 造句含多词目标词 + A/B → 确认留痕：✅（a）
2. 自由表达自发含 prompted_use 短语 + A/B → 毕业 + graduatedWords + MasteryScore=100：✅（b、c8）
3. 乱序/插词/子串不命中：✅（c1、c2、c7 负例 + c3 子串）
4. 单词零回归 + T-033 口径未误改：✅（d + diff 审查）

## 四、不足分级

- **P2 提示（不阻塞）**：可分离短语动词的自然语序不命中——如目标 "put off"，正确英文 "put the meeting off"（插词）按口径不命中、不确认。当前口径「不做词形/语序变换」是提交说明中明示的有意取舍，单测已固化该行为；若后续仿真显示分离短语动词确认率偏低，建议另开任务评估（如面向 phrasal verb 的容忍窗口）。本轮不记验收不足。
- 开发自报 dotnet test 182+6 全绿，本轮按约定未重跑全量；净增 7 例的断言内容与实测观察一致。

## 五、清理

API 进程已杀（端口 5193 已释放）、库 nextword_qa_t040 已 DROP、api.log 无异常错误（仅无害的 https redirect 警告）。
