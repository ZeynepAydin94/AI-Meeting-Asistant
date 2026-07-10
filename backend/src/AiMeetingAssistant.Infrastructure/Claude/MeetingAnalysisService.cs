using System.Text.Json;
using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Services;

namespace AiMeetingAssistant.Infrastructure.Claude;

public class MeetingAnalysisService(IAnthropicClient anthropicClient) : IMeetingAnalysisService
{
    private const string ToolName = "extract_meeting_analysis";

    private static string BuildSystemPrompt(DateOnly today) => $"""
        You are an assistant that analyzes meeting transcripts for a team. Given a raw transcript,
        produce a concise summary, the concrete decisions that were made, and a list of action items.
        Today's date is {today:yyyy-MM-dd}.

        For each action item, decide whether it requires a Jira ticket. Set requires_jira_ticket to
        true only when the item is a concrete, ownable deliverable someone needs to follow up on
        (e.g. "fix the billing bug", "redesign the signup flow"). Set it to false for vague
        discussion points, FYI-only remarks, or items with no clear owner or deliverable. When true,
        also provide a short suggested_ticket_title and a suggested_ticket_description with enough
        context for someone who wasn't in the meeting to act on it.

        If a deadline is mentioned for an action item, set due_date to an absolute date in
        YYYY-MM-DD format. Resolve relative dates (e.g. "by Friday", "next week", "end of month")
        against today's date above. Omit due_date entirely if no deadline is mentioned or implied.
        """;

    private static readonly object InputSchema = new
    {
        type = "object",
        properties = new
        {
            summary = new { type = "string", description = "A concise 2-4 sentence summary of the meeting." },
            key_decisions = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Concrete decisions the group agreed on during the meeting.",
            },
            action_items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        description = new { type = "string", description = "What needs to be done." },
                        owner_hint = new { type = "string", description = "Who seems responsible, if mentioned. Omit if unclear." },
                        priority = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Urgent" } },
                        requires_jira_ticket = new { type = "boolean" },
                        suggested_ticket_title = new { type = "string", description = "Short Jira ticket title, only if requires_jira_ticket is true." },
                        suggested_ticket_description = new { type = "string", description = "Longer Jira ticket description with context, only if requires_jira_ticket is true." },
                        due_date = new { type = "string", description = "Absolute deadline in YYYY-MM-DD format, only if a deadline was mentioned or implied." },
                    },
                    required = new[] { "description", "priority", "requires_jira_ticket" },
                },
            },
        },
        required = new[] { "summary", "key_decisions", "action_items" },
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MeetingAnalysisResult> AnalyzeAsync(string transcriptText, string? apiKeyOverride = null, CancellationToken cancellationToken = default)
    {
        var toolInput = await anthropicClient.InvokeToolAsync(
            BuildSystemPrompt(DateOnly.FromDateTime(DateTime.UtcNow)),
            transcriptText,
            ToolName,
            "Records the structured analysis of a meeting transcript.",
            InputSchema,
            apiKeyOverride,
            cancellationToken);

        var result = toolInput.Deserialize<MeetingAnalysisResult>(DeserializeOptions);
        return result ?? throw new AnthropicApiException("Claude returned an empty analysis.");
    }
}
