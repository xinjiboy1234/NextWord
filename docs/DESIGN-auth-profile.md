# 用户登录与个人主页 — 设计文档

> 版本：2026-06-23  
> 状态：已实现

## 1. 目标

在不大改现有架构的前提下，增加：

1. **用户登录**（注册 + 登录，JWT）
2. **个人主页**：展示等级与学习进度；配置个人 LLM（类型、URL、API Key），预设 OpenAI / DeepSeek / Qwen

## 2. 约束与原则

| 原则 | 说明 |
|------|------|
| 最小侵入 | 保留现有 Minimal API、`IUserRepository`、可选 `userId` 查询参数 |
| 向后兼容 | 未登录时仍使用种子默认用户，E2E/集成测试无需改动 |
| 密钥安全 | API Key 仅存服务端；响应中掩码显示，不回传明文 |
| LLM 抽象不变 | 继续通过 `ILLMProvider`；新增 `IUserLlmProviderFactory` 按用户解析 |

## 3. 数据模型

### 3.1 User 扩展

| 字段 | 类型 | 说明 |
|------|------|------|
| `Email` | string? | 唯一，可空（种子用户无邮箱） |
| `PasswordHash` | string? | PBKDF2-SHA256，可空 |

### 3.2 UserLlmSettings（1:1 User）

| 字段 | 类型 | 说明 |
|------|------|------|
| `UserId` | Guid PK/FK | |
| `Provider` | string | `OpenAI` / `DeepSeek` / `Qwen` / `Custom` |
| `BaseUrl` | string | API 根地址 |
| `Model` | string | 模型名 |
| `ApiKey` | string? | 用户密钥（服务端存储） |
| `UpdatedAt` | DateTimeOffset | |

### 3.3 LLM 预设

| 预设 ID | Provider | BaseUrl | 默认 Model |
|---------|----------|---------|------------|
| `openai` | OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| `deepseek` | DeepSeek | `https://api.deepseek.com` | `deepseek-chat` |
| `qwen` | Qwen | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-plus` |

选择预设时自动填充 URL 与 Model；用户可覆盖 URL/Model。

## 4. API 设计

### 4.1 认证 ` /api/auth`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/register` | `{ email, password, displayName }` → `{ token, user }` |
| POST | `/login` | `{ email, password }` → `{ token, user }` |
| GET | `/me` | 需 Bearer Token → 当前用户摘要 |

JWT Claims：`sub` = UserId，`email`，`name` = DisplayName。有效期 7 天。

### 4.2 个人主页 ` /api/profile`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/` | 需登录：进度 + 等级看板 + LLM 设置（Key 掩码） |
| PUT | `/` | 更新 `displayName` |
| PUT | `/llm` | 更新 LLM：`presetId?`, `provider`, `baseUrl`, `model`, `apiKey?` |
| GET | `/llm/presets` | 预设列表（无需登录） |

### 4.3 用户解析顺序（全站）

```
JWT sub → query userId → 默认种子用户
```

## 5. LLM 按用户调度

```
IUserLlmProviderFactory.GetForUserAsync(userId)
  ├─ 有 UserLlmSettings + ApiKey → OpenAI 兼容 ChatClient（自定义 Endpoint）
  └─ 否则 → 全局 Singleton ILLMProvider（Mock / 服务端 OpenAI）
```

造句、自由表达、阅读词汇提取、评论回复、阅读 Agent 在调用前通过 Factory 解析用户级 Provider。

## 6. 前端

| 组件 | 职责 |
|------|------|
| `AuthContext` | Token 存 `localStorage`；Axios 注入 `Authorization` |
| `LoginPage` | 登录 / 注册 Tab |
| `ProfilePage` | 等级、进度、LLM 预设与配置表单 |
| `App.tsx` | 导航增加「我的」；未登录显示登录入口 |

未登录：学习功能仍可用（默认用户）；「我的」页提示登录。

## 7. 迁移

新增迁移 `AddUserAuthAndLlmSettings`：`Users` 增列 + `UserLlmSettings` 表。

## 8. 配置

`appsettings.json`:

```json
"Auth": {
  "JwtSecret": "...",
  "Issuer": "NextWord",
  "Audience": "NextWord",
  "ExpirationDays": 7
}
```

生产环境通过环境变量 `Auth__JwtSecret` 覆盖。
