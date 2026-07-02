namespace NextWord.Domain.Interfaces;

using System.Text.Json;

public interface ILearningToolHandler
{
    string Name { get; }
    Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken);
}

public interface ILearningToolRegistry
{
    IReadOnlyList<string> ToolNames { get; }
    Task<object> InvokeAsync(string toolName, JsonElement args, Guid userId, CancellationToken cancellationToken);
}
