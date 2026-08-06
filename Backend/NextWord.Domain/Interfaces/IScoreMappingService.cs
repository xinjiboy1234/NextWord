using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IScoreMappingService
{
    string? MapToCefr(int score);
    string MapToBucket(int score);
    UserProfileScores Project(UserProgress progress);
    int ComputeOverall(int? vocabulary, int? reading, int? writing);
    int ClampScore(int score);
    CefrLevel MapScoreToCefrLevel(int score);
    /// <summary>T-038：按标签取 CEFR 分带定义（Min/Max），供 cefrDisplay 下行迟滞取当前档下限。</summary>
    ScoreBand? GetCefrBand(string label);
}
