-- Rollback: clear Score kernel fields (legacy CEFR columns unchanged)
-- Does NOT drop columns; run before reverting EF migration if needed

UPDATE "UserProgress"
SET
    "VocabularyScore" = NULL,
    "ReadingScore" = NULL,
    "WritingScore" = NULL,
    "SpellingScore" = NULL,
    "DifficultyBucket" = NULL,
    "CefrDisplay" = NULL,
    "ScoresUpdatedAt" = NULL,
    "ScoreSchemaVersion" = 1
WHERE "LegacyCefrJson" IS NOT NULL;

-- Optional: restore display from legacy snapshot (manual review recommended)
-- UPDATE "UserProgress" SET ... FROM json parse LegacyCefrJson

UPDATE "UserProgress"
SET "LegacyCefrJson" = NULL
WHERE "LegacyCefrJson" IS NOT NULL;

UPDATE "WordDifficultyAnnotations"
SET
    "IntrinsicScore" = NULL,
    "DimensionsJson" = NULL,
    "SourcesJson" = NULL,
    "PromptVersion" = NULL,
    "Version" = 1,
    "IsCurrent" = true,
    "SchemaVersion" = 1;
