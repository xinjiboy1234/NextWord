# 《林晓的七天》— NextWord Agent 协作演示数据集

本目录是一个**自包含、可复现**的演示数据集：在不改代码、不改数据的前提下，
用真实 API 多轮操作 + 真实 LLM（DashScope qwen-plus）演示 NextWord 各 Agent
（Profiler / Verifier / Planner / Insight）围绕一个虚拟用户「林晓」的完整协作链路。

## 目录结构

```
story.md                     剧本全文（人物设定、分幕、各 Agent 预期行为、诚实性声明）
data/
  persona.json               全部输入测试数据：人设、测评作答模板、造句文本（正常期/回避期）、自由表达文本
scripts/
  llm-proxy.py               LLM 记录代理：转发 DashScope 并留痕全部 Agent ↔ LLM 对话
  run-story.py               演示驱动脚本：按剧本执行全部 API 操作并记录事件时间轴
  build-timeline.py          生成 timeline.html + 导出对话 markdown
timeline.html                交互式查看器（自包含单文件）：左侧按故事日分组的事件导航
                             （角色过滤/搜索/折叠），顶部缩略泳道时间轴总览，右侧完整详情——
                             事件原始 JSON、完整 LLM 对话（气泡渲染 + JSON 美化 + token 用量），
                             剧情节点 ↔ LLM 对话双向跳转，←/→ 键盘翻页
output/
  timeline.json              全部事件（故事时间标签 + 真实时间戳 + 角色 + 详情）
  llm-conversations.jsonl    全部 LLM 调用的原始记录（请求/响应/耗时/用量/归属）
  conversations/             按序导出的每篇对话 markdown（index.md 为目录）
  api-snapshots/             关键端点响应快照（测评结果/画像/报告/计划/洞察，前后两版）
  db/                        关键表只读 dump（BackgroundJobs/SentenceLogs/LearningPlans/洞察/画像）
  evaluation.md              最终评价（逐项核查 Agent 链路 + 与剧本预期的偏差）
```

## 复现方式

```bash
# 1. 依赖：postgres 容器（docker compose up -d postgres）、DASHSCOPE_API_KEY 环境变量
docker exec nextword-postgres-1 createdb -U nextword nextword_demo   # 空库

# 2. 启动 LLM 记录代理
python scripts/llm-proxy.py                                          # :5299

# 3. 启动演示后端（独立库 + 真实 LLM 经代理）
cd Backend/NextWord.Api && env ASPNETCORE_ENVIRONMENT=Development \
  "ConnectionStrings__PostgreSql=Host=localhost;Port=5432;Database=nextword_demo;Username=nextword;Password=nextword" \
  Llm__OpenAI__Enabled=true Llm__OpenAI__Model=qwen-plus \
  Llm__OpenAI__ApiKeyEnvironmentVariable=DASHSCOPE_API_KEY \
  Llm__OpenAI__BaseUrl=http://localhost:5299/v1 \
  dotnet run --no-launch-profile --urls http://localhost:5108

# 4. 执行剧本（约 20-40 分钟，含真实 LLM 等待）
python scripts/run-story.py

# 5. 生成时间轴与对话导出
python scripts/build-timeline.py
```

## 关键设计约束

- **零代码改动**：演示只使用既有公开 API 与配置项（连接串 / LLM 端点环境变量）；
- **零数据篡改**：用户侧数据全部经 API 真实提交并由真实 LLM 评分；数据库仅 SELECT 用于观测留痕；
- **定时任务触发时机**：日级指标筛查用公开手动端点 `POST /api/insights/bottleneck/jobs`
  触发（与 ProfileScoreSnapshotWorker 每日自动执行的筛查是同一代码路径）；
- **回避模式信号是真实构造的**：前 6 条造句每句含 1-2 个复杂连接词、后 6 条全简单句，
  由规则引擎零 LLM 捕获，Insight Agent 细读原文判定瓶颈性质并触发重规划。
