# Score Backfill Staging Drill

在 staging / 预发环境执行以下步骤，验证 `Upgrade_ScoreBackfill.sql` 与 `Rollback_ScoreBackfill.sql`。

## 前置

1. 备份数据库（全量快照）。
2. 确认 `UserProgress` 存在 legacy 用户（仅有 CEFR 等级、Score 列为空）。

## Upgrade drill

```bash
# PostgreSQL 示例
psql "$DATABASE_URL" -f Backend/Scripts/Upgrade_ScoreBackfill.sql
```

**验收 SQL：**

```sql
SELECT COUNT(*) AS missing_overall
FROM "UserProgress"
WHERE "HasCompletedInitialAssessment" = true
  AND "OverallScore" IS NULL;

SELECT COUNT(*) AS has_legacy_json
FROM "UserProgress"
WHERE "LegacyCefrJson" IS NOT NULL;
```

期望：`missing_overall = 0`（已测评用户均有 OverallScore）；`has_legacy_json > 0`（legacy 用户保留快照）。

## Rollback drill

```bash
psql "$DATABASE_URL" -f Backend/Scripts/Rollback_ScoreBackfill.sql
```

**验收：** Score 列清空，`LegacyCefrJson` 仍可恢复 CEFR 展示；应用只读路径不崩溃。

## SQLite 开发库快速验证

```bash
cd Backend/NextWord.Api
sqlite3 nextword-dev.db ".read ../Scripts/Upgrade_ScoreBackfill.sql"
sqlite3 nextword-dev.db "SELECT UserId, OverallScore, CefrDisplay FROM UserProgress LIMIT 5;"
```

> 注意：SQLite 脚本语法若与 PG 版本不同，以 PG 为准；开发库仅作 smoke。

## 记录

| 日期 | 环境 | Upgrade | Rollback | 执行人 |
|------|------|---------|----------|--------|
|      |      |         |          |        |
