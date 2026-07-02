using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Infrastructure.Services;

public sealed class DuckDuckGoSearchService(
    HttpClient httpClient,
    IOptions<SearchOptions> options,
    ILogger<DuckDuckGoSearchService> logger) : IWebSearchService
{
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var trimmed = query.Trim();
        if (trimmed.Length > 120)
        {
            trimmed = trimmed[..120];
        }

        try
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(trimmed)}&format=json&no_html=1&skip_disambig=1";
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var results = new List<WebSearchResult>();
            CollectTopics(doc.RootElement, results, options.Value.MaxResults);
            if (results.Count == 0 && doc.RootElement.TryGetProperty("AbstractText", out var abstractText))
            {
                var text = abstractText.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(new WebSearchResult(trimmed, text, doc.RootElement.GetProperty("AbstractURL").GetString() ?? string.Empty));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DuckDuckGo search failed for query {Query}", trimmed);
            return [];
        }
    }

    private static void CollectTopics(JsonElement element, List<WebSearchResult> results, int max)
    {
        if (results.Count >= max) return;

        if (element.TryGetProperty("RelatedTopics", out var topics))
        {
            foreach (var topic in topics.EnumerateArray())
            {
                if (results.Count >= max) break;
                if (topic.TryGetProperty("Topics", out var nested))
                {
                    CollectTopics(topic, results, max);
                    continue;
                }

                var text = topic.TryGetProperty("Text", out var textEl) ? textEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                var url = topic.TryGetProperty("FirstURL", out var urlEl) ? urlEl.GetString() ?? string.Empty : string.Empty;
                var title = text.Split(" - ", 2)[0];
                var snippet = text.Contains(" - ") ? text.Split(" - ", 2)[1] : text;
                results.Add(new WebSearchResult(title, snippet, url));
            }
        }
    }
}
