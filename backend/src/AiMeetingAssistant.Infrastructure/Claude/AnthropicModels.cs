using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiMeetingAssistant.Infrastructure.Claude;

internal record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal record AnthropicTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] object InputSchema);

internal record AnthropicToolChoice(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name);

internal record AnthropicRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] List<AnthropicMessage> Messages,
    [property: JsonPropertyName("tools")] List<AnthropicTool> Tools,
    [property: JsonPropertyName("tool_choice")] AnthropicToolChoice ToolChoice);

internal record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("input")] JsonElement? Input);

internal record AnthropicResponse(
    [property: JsonPropertyName("content")] List<AnthropicContentBlock>? Content,
    [property: JsonPropertyName("stop_reason")] string? StopReason,
    [property: JsonPropertyName("error")] AnthropicErrorDetail? Error);

internal record AnthropicErrorDetail(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message);
