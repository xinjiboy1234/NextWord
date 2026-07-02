using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IScoreProfileService
{
    Task<UserProfileScores> GetScoresAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileUpdateResult> ApplyUpdateAsync(ProfileUpdateCommand command, CancellationToken cancellationToken);
}
