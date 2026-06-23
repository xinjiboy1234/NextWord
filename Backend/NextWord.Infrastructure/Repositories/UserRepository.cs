using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Repositories;

public sealed class UserRepository(ApplicationDbContext db) : IUserRepository
{
    public async Task<User> GetOrCreateDefaultUserAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == SeedData.DefaultUserId, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            Id = SeedData.DefaultUserId,
            DisplayName = "MVP Learner"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(user => user.Email == normalized, cancellationToken);
    }

    public async Task<User> CreateUserAsync(string email, string passwordHash, string displayName, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim()
        };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress { UserId = user.Id });
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<UserLlmSettings?> GetLlmSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return db.UserLlmSettings.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public async Task<UserLlmSettings> UpsertLlmSettingsAsync(UserLlmSettings settings, CancellationToken cancellationToken)
    {
        var existing = await db.UserLlmSettings.FirstOrDefaultAsync(item => item.UserId == settings.UserId, cancellationToken);
        if (existing is null)
        {
            db.UserLlmSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
            return settings;
        }

        existing.Provider = settings.Provider;
        existing.BaseUrl = settings.BaseUrl;
        existing.Model = settings.Model;
        existing.UpdatedAt = settings.UpdatedAt;
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            existing.ApiKey = settings.ApiKey;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<UserWordRelationship> GetOrCreateRelationshipAsync(Guid userId, Guid wordId, CancellationToken cancellationToken)
    {
        var relationship = await db.UserWordRelationships
            .FirstOrDefaultAsync(item => item.UserId == userId && item.WordId == wordId, cancellationToken);

        if (relationship is not null)
        {
            return relationship;
        }

        relationship = new UserWordRelationship
        {
            UserId = userId,
            WordId = wordId,
            Source = WordSource.New,
            NextReviewDue = DateTimeOffset.UtcNow.AddDays(1)
        };
        db.UserWordRelationships.Add(relationship);
        return relationship;
    }

    public async Task<UserProgress> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await db.UserProgress.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (progress is not null)
        {
            return progress;
        }

        progress = new UserProgress
        {
            UserId = userId
        };
        db.UserProgress.Add(progress);
        return progress;
    }

    public async Task AddLearningLogAsync(WordLearningLog log, CancellationToken cancellationToken)
    {
        await db.WordLearningLogs.AddAsync(log, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
