using System.Text.Json;
using AiMeetingAssistant.Core.Services;

namespace AiMeetingAssistant.Tests.Fakes;

/// <summary>
/// Returns a queued sequence of raw JSON strings as successive tool_use responses, so tests can
/// simulate the exact malformed shapes seen live from Claude (bare strings, object arrays, nulls)
/// without hitting the real API.
/// </summary>
public class FakeAnthropicClient(params string[] responses) : IAnthropicClient
{
    private readonly Queue<string> _responses = new(responses);

    public int CallCount { get; private set; }

    public Task<JsonElement> InvokeToolAsync(
        string systemPrompt,
        string userMessage,
        string toolName,
        string toolDescription,
        object inputSchema,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeAnthropicClient ran out of queued responses.");
        }

        using var doc = JsonDocument.Parse(_responses.Dequeue());
        return Task.FromResult(doc.RootElement.Clone());
    }
}
