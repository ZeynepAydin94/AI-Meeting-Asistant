using System.Text.Json.Serialization;

namespace AiMeetingAssistant.Infrastructure.Jira;

internal record JiraMyselfResponse(
    [property: JsonPropertyName("displayName")] string? DisplayName);

internal record JiraErrorResponse(
    [property: JsonPropertyName("errorMessages")] List<string>? ErrorMessages,
    [property: JsonPropertyName("errors")] Dictionary<string, string>? Errors);

internal record JiraCreateIssueResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("key")] string? Key);

internal record JiraDocText(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

internal record JiraDocParagraph(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] List<JiraDocText> Content);

internal record JiraDescriptionDoc(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("content")] List<JiraDocParagraph> Content);

internal record JiraProjectRef([property: JsonPropertyName("key")] string Key);

internal record JiraIssueTypeRef([property: JsonPropertyName("name")] string Name);

internal record JiraCreateIssueFields(
    [property: JsonPropertyName("project")] JiraProjectRef Project,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("issuetype")] JiraIssueTypeRef IssueType,
    [property: JsonPropertyName("description")] JiraDescriptionDoc Description);

internal record JiraCreateIssueRequest(
    [property: JsonPropertyName("fields")] JiraCreateIssueFields Fields);
