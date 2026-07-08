# PostgreSQL Schema 迁移（生产）

生产环境 **不在 API 启动时** 执行 EF `MigrateAsync()`。Schema 变更通过本目录 SQL 脚本在部署前执行。

## 发布前（本地）

在仓库根目录运行：

```powershell
# 全量幂等脚本（首次部署或不确定 DB 版本）
.\Backend\Scripts\generate-migration-sql.ps1

# 增量脚本（从上次生产已应用的迁移到最新）
.\Backend\Scripts\generate-migration-sql.ps1 -From 20260630160631_AddChallengeSession

# 仅生成幂等脚本
.\Backend\Scripts\generate-migration-sql.ps1 -IdempotentOnly
```

生成文件：

| 文件 | 用途 |
|------|------|
| `Upgrade_Idempotent.sql` | 全量幂等，含 `__EFMigrationsHistory` 检查 |
| `Upgrade_From_<migration>.sql` | 增量，从指定迁移到最新 |

**必须人工审查 SQL**，尤其 `AlignPostgresModelSnapshot` 中的 `ALTER COLUMN`（类型 TEXT → uuid/timestamptz 等）。

## 生产执行

```bash
# 1. 备份
pg_dump "$DATABASE_URL" > backup_$(date +%Y%m%d).sql

# 2. Schema（二选一）
psql "$DATABASE_URL" -f Backend/Scripts/Migrations/Upgrade_From_<last_prod_migration>.sql
# 或首次 / 不确定版本时：
psql "$DATABASE_URL" -f Backend/Scripts/Migrations/Upgrade_Idempotent.sql

# 3. 数据回填（仅有 legacy CEFR 用户时）
psql "$DATABASE_URL" -f Backend/Scripts/Upgrade_ScoreBackfill.sql

# 4. 部署 API（Production 环境，不自动 Migrate）
```

## Docker 本地模拟生产

```powershell
docker compose up -d postgres
# 等待 healthy 后
psql "Host=localhost;Port=5432;Database=nextword;Username=nextword;Password=nextword" `
  -f Backend/Scripts/Migrations/Upgrade_Idempotent.sql
docker compose up api
```

若 postgres volume 处于半迁移失败状态：`docker compose down -v` 后重来。

## 开发环境

Development 使用 PostgreSQL + 启动时 `MigrateAsync()`，无需手动跑本目录 SQL。

本地 Postgres：`docker compose up -d postgres`（默认库 `nextword`，测试库 `nextword_test`）。

若迁移失败且需清空开发库：`docker compose down -v` 后重新 `up`。

## 迁移历史

| 迁移 ID | 说明 |
|---------|------|
| `20260623104645_InitialCreate` | 初始 schema |
| `20260623131945_AddUserSetting` | 用户设置 |
| `20260630154336_AddScoreKernelM1` | Score 内核 |
| `20260630160631_AddChallengeSession` | 挑战会话 |
| `20260706132041_AlignPostgresModelSnapshot` | PG 列类型对齐 |
