using System.Net.Http.Json;
using System.Text.Json;
using AiMeetingAssistant.Core.Services;
using Microsoft.Extensions.Configuration;

namespace AiMeetingAssistant.Infrastructure.Claude;

public class AnthropicClient(HttpClient httpClient, IConfiguration configuration) : IAnthropicClient
{
    private const string ApiVersion = "2023-06-01";

    public async Task<JsonElement> InvokeToolAsync(
        string systemPrompt,
        string userMessage,
        string toolName,
        string toolDescription,
        object inputSchema,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? configuration["Claude:ApiKey"] : apiKeyOverride;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AnthropicApiException("Claude API key is not configured.");
        }

        var model = configuration["Claude:Model"] ?? "claude-sonnet-5";
        var maxTokens = int.TryParse(configuration["Claude:MaxTokens"], out var configuredMaxTokens)
            ? configuredMaxTokens
            : 4096;

        var request = new AnthropicRequest(
            Model: model,
            MaxTokens: maxTokens,
            System: systemPrompt,
            Messages: [new AnthropicMessage("user", userMessage)],
            Tools: [new AnthropicTool(toolName, toolDescription, inputSchema)],
            ToolChoice: new AnthropicToolChoice("tool", toolName));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);

        using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var response = await httpResponse.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken: cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var message = response?.Error?.Message ?? $"Claude API request failed with status {(int)httpResponse.StatusCode}.";
            throw new AnthropicApiException(message);
        }

        var toolUseBlock = response?.Content?.FirstOrDefault(block => block.Type == "tool_use" && block.Name == toolName);
        if (toolUseBlock?.Input is not { } input)
        {
            throw new AnthropicApiException("Claude did not return the expected structured tool response.");
        }

        return input;
    }
}
