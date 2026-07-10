using System.Text.Json;

namespace AiMeetingAssistant.Core.Services;

public interface IAnthropicClient
{
    Task<JsonElement> InvokeToolAsync(
        string systemPrompt,
        string userMessage,
        string toolName,
        string toolDescription,
        object inputSchema,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default);
}
