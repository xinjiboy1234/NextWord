using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

public sealed record VocabQuizQuestion(string Word, IReadOnlyList<string> Options, int CorrectIndex, DifficultyLevel Difficulty);
public sealed record SpellingQuizQuestion(string Chinese, string CorrectSpelling, DifficultyLevel Difficulty);
public sealed record SentenceQuizQuestion(Guid? WordId, string Word, string Scene);
public sealed record ReadingQuizQuestion(Guid ArticleId, string Question, IReadOnlyList<string> Options, int CorrectIndex, string ArticleExcerpt);

public sealed record ChallengePack(
    IReadOnlyList<VocabQuizQuestion> Vocabulary,
    SentenceQuizQuestion Sentence,
    ReadingQuizQuestion Reading,
    CefrLevel AttemptedLevel);

public sealed record StepScoreResult(
    AssessmentStepType Step,
    CefrLevel? MappedLevel,
    double RawScore,
    string ScoresJson);

public sealed record FinalLevelResult(
    CefrLevel VocabLevel,
    CefrLevel SpellingLevel,
    CefrLevel SentenceLevel,
    CefrLevel ReadingLevel,
    CefrLevel OverallLevel);

public sealed record UpgradeCheckResult(
    bool IsCandidate,
    bool RequiresConfirmationChallenge,
    string Summary);
