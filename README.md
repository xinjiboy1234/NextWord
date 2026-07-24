# NextWord

AI 驱动的英语词汇学习应用。围绕「每日选词 → 多模式练习 → AI 评分 → 水平测评」的学习闭环，用规则引擎（SM-2 间隔重复 + Score 内核）保证学习路径的确定性，用 LLM 提供造句评分、阅读查词、词汇提取等智能体验。

## 功能

- **每日选词**：按用户词汇水平选取难度带内的单词，弱词优先；看词回忆释义，SM-2 算法调度复习
- **拼写听写**：发音播放 + 逐字母错误标注，到期复习队列自动回退每日词
- **造句工作室**：指定词造句 / 自由表达两种模式，LLM 从语法、自然度、词汇、相关度四维评分（0–5）并给出 A–D 等级与改写建议，反馈语言默认中文
- **短文阅读**：内置分级短文；点词查义（文章级缓存 + 熟悉度展示）、LLM 重点词汇提取（音标 + 双场景例句）、段落批注（可请求 AI 回复）、阅读计时
- **首次水平测评**：词汇选择 → 拼写 → 造句 → 阅读四步，按「最短板」原则定级并写入 Score 内核；新用户强制引导，可跳过（默认 A2）
- **综合挑战**：词汇 + 造句 + 阅读三阶段，服务端计分；支持确认挑战（锁定目标等级，通过则升级）
- **Score 内核**：词汇 / 阅读 /写作三个维度 0–100 分，总分取最短板，映射 CEFR 展示；每日快照供趋势图；所有写入走统一入口并幂等去重
- **等级系统**：连胜天数 + 挑战通过驱动升级评估（C1 封顶），等级历史可查
- **个人中心**：等级仪表盘、学习统计、评估报告、CEFR 标签显示开关、自带密钥（BYOK）的 LLM 设置（OpenAI / DeepSeek / Qwen 等 OpenAI 兼容接口）

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | .NET 10（ASP.NET Core Minimal API）、EF Core + Npgsql（PostgreSQL 16）、JWT 认证、Microsoft.Extensions.AI（OpenAI 兼容）、可选 Redis 缓存 |
| 前端 | React 19 + TypeScript、Vite 8、Tailwind CSS 4 + @base-ui/react、react-router-dom v7、axios |
| 测试 | xUnit（单元 + WebApplicationFactory 集成，依赖真实 PostgreSQL）、Playwright E2E |
| 部署 | Docker / docker-compose（postgres + redis + api） |

## 快速启动

前置：.NET 10 SDK、Node 22+、Docker。

```bash
# 1. 启动 PostgreSQL（首次会自动建 nextword / nextword_test / nextword_unit_test 库）
docker compose up -d postgres

# 2. 启动后端（http://localhost:5108，Development 下自动迁移 + 种子数据）
cd Backend/NextWord.Api
dotnet run --launch-profile http

# 3. 启动前端（http://localhost:5173，/api 自动代理到 5108）
cd Frontend
npm install
npm run dev
```

打开 http://localhost:5173 ，注册账号即可使用。默认使用内置 Mock LLM（无需 API key），在「我的 → 管理 → LLM 设置」里填入自己的 OpenAI 兼容 API key 后切换为真实模型。

完整容器化部署（postgres + redis + api，API 在 8080）：

```bash
docker compose up -d
# 前端 dev 代理指向 Docker API：VITE_API_PROXY_TARGET=http://localhost:8080 npm run dev
```

## 测试

```bash
# 后端单元 + 集成测试（需要本地 PostgreSQL）
docker compose up -d postgres
cd Backend && dotnet test

# 前端 E2E（自动拉起后端 :5108 与前端 :5173）
cd Frontend && npm run test:e2e
```

## 关键配置

见 `Backend/NextWord.Api/appsettings*.json`，常用项：

- `ConnectionStrings:PostgreSql` / `ConnectionStrings:Redis`
- `Auth:JwtSecret` — **生产环境必须覆盖默认值**
- `Llm:OpenAI:Enabled` / `Model` / `ApiKey`（或 `ApiKeyEnvironmentVariable`，默认读 `OPENAI_API_KEY`）— 服务端默认 LLM；未启用时全局回退 Mock
- `Cache:Provider` — `Memory`（默认）或 `Redis`
- `ScoreMapping` / `ChallengeThresholds` — Score 分带与挑战通过阈值

## 文档

- [docs/CURRENT-STATE.md](docs/CURRENT-STATE.md) — 当前功能与架构的完整说明（模块、API、数据模型、测试）
- [docs/VISION-expression-first.md](docs/VISION-expression-first.md) — 产品方向：以表达能力为核心的愿景对齐、差距分析与修正路径
- [docs/DESIGN-ai-learning-architecture.md](docs/DESIGN-ai-learning-architecture.md) — Score 内核 + CEFR 映射层的架构决策（why）
- [docs/DESIGN-auth-profile.md](docs/DESIGN-auth-profile.md) — 认证与个人 LLM 配置设计
- [development-log.md](development-log.md) — 开发日志（按时间倒序）
- [next-steps.md](next-steps.md) — 当前待办
- [front_design/](front_design/) — 各页面的静态 HTML/CSS 设计原型（与实现同步维护）
