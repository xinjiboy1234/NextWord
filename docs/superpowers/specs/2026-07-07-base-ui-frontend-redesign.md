# NextWord 前端 Base UI 重构设计

**日期**: 2026-07-07  
**状态**: Approved for implementation  
**目标**: 用 @base-ui/react 重写交互控件，精简 2C 导航，首次测评沉浸式体验 + 跳过功能，保持黑白主题。

---

## 1. 设计原则

| 原则 | 说明 |
|------|------|
| **2C 极简** | 主导航 ≤4 项；次要功能从首页卡片进入 |
| **黑白克制** | 延续 `#000` / `#fff` shell；`--brand` 绿色仅用于主 CTA 与成功态 |
| **Headless + Token** | Base UI 负责 a11y/行为；样式用现有 `tokens.css` + 薄封装 |
| **沉浸 onboarding** | 首次测评全屏，无侧栏/底栏/管理入口 |
| **渐进迁移** | 先建 `components/ui/*` 封装层，再替换各页面手写控件 |

---

## 2. 信息架构（导航重构）

### 2.1 问题

- 侧栏 8 项 + 管理 + 用户区，与 Dashboard 卡片重复
- 底栏与侧栏语义重叠（「学习」vs「学习中心」）
- 首次测评时完整 AppShell 仍可见，用户可点走又被 redirect，体验割裂

### 2.2 新导航结构

**桌面侧栏（4 项，无分组标题）**

| 项 | 路由 | 说明 |
|----|------|------|
| 首页 | `/dashboard` | 模块入口聚合 |
| 练习 | `/learn` | 高亮含 learn/spelling/sentence |
| 阅读 | `/reading` | 含文章详情 |
| 我的 | `/profile` | 含设置、管理入口 |

**移动端底栏**：与上表一致 4 tab。

**从侧栏移除、改由 Dashboard / Profile 进入**

- 拼写、造句 → Dashboard 卡片（已有）
- 等级、复习、进度 → Dashboard 卡片（已有）
- 管理 → Profile 页底部「高级设置」链接
- 综合挑战 → Dashboard 新增卡片或 Profile 快捷入口

**顶栏简化**

- 非首页：左侧「返回」；中间页面标题；右侧去掉重复的「个人主页」
- 首页：问候语即可

### 2.3 路由不变

保留现有 path，仅改导航暴露方式，避免破坏 deep link 与 E2E。

---

## 3. 首次测评（Onboarding Shell）

### 3.1 布局

```
┌─────────────────────────────────────────┐
│  NextWord          Step 2/5    [跳过]   │  ← 极简顶栏
├─────────────────────────────────────────┤
│         ●───●───○───○───○  步骤条      │
│                                         │
│              测评内容区                  │
│                                         │
│              [上一步]  [下一步]          │
└─────────────────────────────────────────┘
```

- `App.tsx`：当 `needsAssessment` 时，**不渲染 AppShell**，改用 `OnboardingLayout`
- 管理页、挑战等路由在 onboarding 期间仍被 guard 到 `/assessment`

### 3.2 跳过功能

**前端**

- 右上角文字按钮「跳过本次测评」
- Base UI `AlertDialog` 确认：「跳过将使用默认等级 A2，之后可在「我的」重新测评」
- 确认后 `POST /api/assessment/initial/skip` → reload progress → `/dashboard`

**后端（新增）**

- `IAssessmentService.SkipInitialAsync(userId)`
- 若无进行中 Initial assessment 则创建并标记 `Cancelled`；若有则取消
- `UserProgress.HasCompletedInitialAssessment = true`
- 若 `OverallLevel` 为空则设为 `A2`
- 不写完整测评分数（或写默认 baseline，与现有 mock 一致即可）

---

## 4. Base UI 组件封装层

安装：`pnpm add @base-ui/react`（在 Frontend 目录）

### 4.1 封装清单 (`src/components/ui/`)

| 文件 | Base UI 源 | 用途 |
|------|-----------|------|
| `Button.tsx` | Button | 统一 `.btn` 变体 |
| `Dialog.tsx` | Dialog | 确认框、设置 |
| `Drawer.tsx` | 重写用 Dialog/Drawer | LLM 设置等 |
| `Popover.tsx` | Popover | WordPopover |
| `Tabs.tsx` | Tabs | Login、SentenceStudio |
| `Select.tsx` | Select | 文章筛选 |
| `Switch.tsx` | Switch | Profile CEFR 显示 |
| `RadioGroup.tsx` | RadioGroup | OptionTags、RatingButtons |
| `Progress.tsx` | Progress | 进度条 |
| `Tooltip.tsx` | Tooltip | 可选，图标提示 |

### 4.2 全局设置

- `index.html` / `main.tsx`：根节点加 `className="root"` + CSS `isolation: isolate`
- `body { position: relative }` 用于 iOS Safari backdrop

### 4.3 样式约定

- 封装组件用 `className` 合并现有 token 类（`btn-primary`, `card` 等）
- 不引入 shadcn；保持项目 CSS 体系
- Focus ring：2px dashed `var(--fg)`（延续现有）

---

## 5. 页面迁移优先级

1. **P0** — `AppShell`, `App.tsx` onboarding split, `InitialAssessment`, `LoginPage`
2. **P1** — `OptionTags`, `WordPopover`, `Drawer`/`LlmSettingsDrawer`, `ProfilePage`
3. **P2** — `SentenceStudio`, `ArticleLibrary`, `Dashboard`, `RatingButtons`, `StepNavigator`
4. **P3** — 其余页面替换原生 input/select 为 Base UI Input/Field 封装（如需要）

---

## 6. 视觉规格（延续 + 微调）

```css
/* 保持不变 */
--bg: #ffffff;
--fg: #000000;
--brand: #16a34a;
--sidebar-w: 220px;  /* 略收窄 */
--radius-sm: 50px;   /* 主按钮药丸 */
--radius-md: 8px;    /* 卡片 */
```

**侧栏 active 态**：黑底白字圆角 pill（已有 pattern，保持一致）  
**底栏**：仅图标+短标签，active 用 `--fg` 填充圆角背景

---

## 7. 验收标准

- [ ] `@base-ui/react` 已安装，build 通过
- [ ] 侧栏/底栏仅 4 项；管理从 Profile 进入
- [ ] 首次测评无侧栏/底栏；有跳过按钮且 API 可用
- [ ] OptionTags、Tabs、Dialog、Popover、Switch、Select 使用 Base UI
- [ ] 黑白主题无回归；无新增彩色装饰块
- [ ] `npm run build` 与现有 e2e assessment 用例通过或已更新

---

## 8. 不在本次范围

- 暗色模式
- 后端测评逻辑大改
- 新功能（除 skip API）
- 全面 Tailwind 重写 CSS
