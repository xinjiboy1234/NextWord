using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IModelProfileResolver
{
    ModelProfile Resolve(string? modelProfileId);
}
