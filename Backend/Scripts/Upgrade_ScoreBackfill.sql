-- Upgrade: backfill Score fields from legacy CEFR on UserProgress
-- Run AFTER EF migration AddScoreKernelM1
-- Maps CEFR band center; sub-dimensions left NULL when only OverallLevel known

-- CEFR center values (0-100): A1=10, A2=27, B1=42, B2=60, C1=77, C2=92
UPDATE "UserProgress"
SET
    "LegacyCefrJson" = json_build_object(
        'overall', "OverallLevel",
        'vocab', "VocabLevel",
        'spelling', "SpellingLevel",
        'sentence', "SentenceLevel",
        'reading', "ReadingLevel"
    )::text,
    "VocabularyScore" = CASE "VocabLevel"
        WHEN 'A1' THEN 10 WHEN 'A2' THEN 27 WHEN 'B1' THEN 42
        WHEN 'B2' THEN 60 WHEN 'C1' THEN 77 WHEN 'C2' THEN 92 ELSE NULL END,
    "ReadingScore" = CASE "ReadingLevel"
        WHEN 'A1' THEN 10 WHEN 'A2' THEN 27 WHEN 'B1' THEN 42
        WHEN 'B2' THEN 60 WHEN 'C1' THEN 77 WHEN 'C2' THEN 92 ELSE NULL END,
    "WritingScore" = CASE "SentenceLevel"
        WHEN 'A1' THEN 10 WHEN 'A2' THEN 27 WHEN 'B1' THEN 42
        WHEN 'B2' THEN 60 WHEN 'C1' THEN 77 WHEN 'C2' THEN 92 ELSE NULL END,
    "SpellingScore" = CASE "SpellingLevel"
        WHEN 'A1' THEN 10 WHEN 'A2' THEN 27 WHEN 'B1' THEN 42
        WHEN 'B2' THEN 60 WHEN 'C1' THEN 77 WHEN 'C2' THEN 92 ELSE NULL END,
    "CefrDisplay" = "OverallLevel"::text,
    "DifficultyBucket" = CASE
        WHEN "OverallLevel" IN ('A1', 'A2') THEN 'Basic'
        WHEN "OverallLevel" IN ('B1', 'B2') THEN 'Intermediate'
        ELSE 'Advanced' END,
    "ScoresUpdatedAt" = NOW(),
    "ScoreSchemaVersion" = 1
WHERE "LegacyCefrJson" IS NULL;

-- Initialize annotation v1 fields on existing rows
UPDATE "WordDifficultyAnnotations"
SET
    "Version" = 1,
    "IsCurrent" = true,
    "SchemaVersion" = 1
WHERE "Version" = 0 OR "Version" IS NULL;
