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

internal record JiraAssigneeRef([property: JsonPropertyName("accountId")] string AccountId);

internal record JiraCreateIssueFields(
    [property: JsonPropertyName("project")] JiraProjectRef Project,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("issuetype")] JiraIssueTypeRef IssueType,
    [property: JsonPropertyName("description")] JiraDescriptionDoc Description,
    [property: JsonPropertyName("duedate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DueDate,
    [property: JsonPropertyName("assignee"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JiraAssigneeRef? Assignee);

internal record JiraCreateIssueRequest(
    [property: JsonPropertyName("fields")] JiraCreateIssueFields Fields);

internal record JiraUserSearchResult(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("accountType")] string? AccountType);
