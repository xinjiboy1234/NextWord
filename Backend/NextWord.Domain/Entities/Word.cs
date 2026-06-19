using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class Word
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Lemma { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Phonetics { get; set; } = string.Empty;
    public List<string> Meanings { get; set; } = [];
    public List<string> ExampleSentences { get; set; } = [];
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Basic;
    public CefrLevel CefrLevel { get; set; } = CefrLevel.A1;
    public Guid? LlmAnnotationId { get; set; }
    public bool IsCore { get; set; } = true;

    public WordDifficultyAnnotation? LlmAnnotation { get; set; }
    public List<UserWordRelationship> UserRelationships { get; set; } = [];
    public List<WordLearningLog> LearningLogs { get; set; } = [];
    public List<Sentence> Sentences { get; set; } = [];
    public List<SentenceLog> SentenceLogs { get; set; } = [];
    public List<SpellingLog> SpellingLogs { get; set; } = [];
    public List<ArticleVocabMapping> ArticleVocabMappings { get; set; } = [];
}
