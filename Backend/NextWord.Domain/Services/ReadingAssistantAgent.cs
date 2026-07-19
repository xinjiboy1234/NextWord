using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// 阅读辅助 Agent：根据 intent 选择并组合 skills，失败时回退到 Mock 能力。
/// </summary>
public sealed class ReadingAssistantAgent(IUserLlmProviderFactory llmFactory, ILLMProvider globalLlm) : IReadingAgentService
{
    public async Task<ReadingAgentResponse> AssistAsync(ReadingAgentRequest request, CancellationToken cancellationToken)
    {
        var llm = request.UserId.HasValue
            ? await llmFactory.GetForUserAsync(request.UserId.Value, cancellationToken)
            : globalLlm;
        var intent = request.Intent.Trim().ToLowerInvariant();
        var calls = new List<ReadingAgentSkillCall>();
        DefinitionResponse? definition = null;
        VocabExtractResponse? vocabExtract = null;
        CommentReplyResponse? commentReply = null;
        var options = request.Options ?? new LlmRequestOptions("reading-agent", "reading_assist");

        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);

        if (intent is "lookup" or "explain" or "word" && !string.IsNullOrWhiteSpace(request.SelectedWord))
        {
            calls.Add(new ReadingAgentSkillCall(ReadingSkillRegistry.LookupWord, request.SelectedWord!));
            definition = await llm.GetDefinitionAsync(new DefinitionRequest(
                request.SelectedWord!,
                request.ParagraphText,
                options,
                explanationLanguage), cancellationToken);

            if (intent is "explain")
            {
                calls.Add(new ReadingAgentSkillCall(ReadingSkillRegistry.ExplainInContext, request.ParagraphText ?? string.Empty));
            }
        }
        else if (intent is "vocab" or "extract")
        {
            calls.Add(new ReadingAgentSkillCall(ReadingSkillRegistry.ExtractKeyVocab, request.ArticleTitle));
            vocabExtract = await llm.ExtractVocabAsync(new VocabExtractRequest(
                request.ArticleTitle,
                request.ArticleContent,
                request.UserLevel,
                request.UserLevel,
                options,
                explanationLanguage), cancellationToken);
        }
        else if (intent is "comment" or "reply" && !string.IsNullOrWhiteSpace(request.ParagraphText))
        {
            calls.Add(new ReadingAgentSkillCall(ReadingSkillRegistry.CommentReply, request.ParagraphText!));
            commentReply = await llm.ReplyToCommentAsync(new CommentReplyRequest(
                request.ParagraphText!,
                request.SelectedWord ?? "Please explain this paragraph.",
                request.ArticleTitle,
                options), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.SelectedWord))
        {
            calls.Add(new ReadingAgentSkillCall(ReadingSkillRegistry.LookupWord, request.SelectedWord!));
            definition = await llm.GetDefinitionAsync(new DefinitionRequest(
                request.SelectedWord!,
                request.ParagraphText,
                options,
                explanationLanguage), cancellationToken);
        }

        var message = BuildMessage(intent, definition, vocabExtract, commentReply);
        return new ReadingAgentResponse(message, calls, definition, vocabExtract, commentReply);
    }

    private static string BuildMessage(
        string intent,
        DefinitionResponse? definition,
        VocabExtractResponse? vocabExtract,
        CommentReplyResponse? commentReply)
    {
        if (commentReply is not null)
        {
            return commentReply.Reply;
        }

        if (vocabExtract is not null)
        {
            return $"Extracted {vocabExtract.KeyVocab.Count} key vocabulary items for your level.";
        }

        if (definition is not null)
        {
            var meaning = definition.Meanings.FirstOrDefault()?.Definition ?? "No definition available.";
            return intent is "explain"
                ? $"In context: {meaning} {definition.SpecialUsage}"
                : meaning;
        }

        return "Reading assistant is ready. Try lookup, vocab, or comment intent.";
    }
}
