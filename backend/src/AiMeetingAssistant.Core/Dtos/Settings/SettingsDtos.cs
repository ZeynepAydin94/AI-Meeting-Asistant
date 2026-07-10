namespace AiMeetingAssistant.Core.Dtos.Settings;

public record UpdateSettingsRequest(
    string? ClaudeApiKey,
    string? JiraBaseUrl,
    string? JiraEmail,
    string? JiraApiToken,
    string? JiraDefaultProjectKey,
    string? JiraDefaultIssueType);

public record SettingsResponse(
    bool HasClaudeApiKey,
    string? JiraBaseUrl,
    string? JiraEmail,
    bool HasJiraApiToken,
    string? JiraDefaultProjectKey,
    string? JiraDefaultIssueType);
