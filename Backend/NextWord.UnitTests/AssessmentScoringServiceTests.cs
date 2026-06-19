using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public class AssessmentScoringServiceTests
{
    private readonly AssessmentScoringService _service = new();

    [Fact]
    public void Final_level_uses_shortest_board()
    {
        var vocab = new Domain.Models.StepScoreResult(AssessmentStepType.Vocabulary, CefrLevel.B2, 60, "{}");
        var spelling = new Domain.Models.StepScoreResult(AssessmentStepType.Spelling, CefrLevel.B1, 50, "{}");
        var sentence = new Domain.Models.StepScoreResult(AssessmentStepType.Sentence, CefrLevel.A2, 2, "{}");
        var reading = new Domain.Models.StepScoreResult(AssessmentStepType.Reading, CefrLevel.B1, 55, "{}");

        var result = _service.CalculateFinalLevel(vocab, spelling, sentence, reading);

        Assert.Equal(CefrLevel.A2, result.OverallLevel);
    }

    [Theory]
    [InlineData(80, CefrLevel.C1)]
    [InlineData(35, CefrLevel.B1)]
    [InlineData(5, CefrLevel.A1)]
    public void Vocab_accuracy_maps_to_cefr(double accuracy, CefrLevel expected)
    {
        Assert.Equal(expected, _service.MapVocabAccuracy(accuracy));
    }
}
