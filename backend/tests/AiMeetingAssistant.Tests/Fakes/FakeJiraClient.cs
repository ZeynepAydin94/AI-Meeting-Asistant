using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Services;

namespace AiMeetingAssistant.Tests.Fakes;

/// <summary>
/// Records every call it receives and returns canned, configurable results — lets tests assert on
/// exactly what the controller sent to Jira without making real network calls.
/// </summary>
public class FakeJiraClient : IJiraClient
{
    public List<(string ProjectKey, string IssueType, string Summary, string? AssigneeAccountId)> CreateIssueCalls { get; } = [];

    public JiraConnectionTestResult ConnectionTestResult { get; set; } = new(true, "Connected as Test User.");

    public JiraCreateIssueResult CreateIssueResult { get; set; } = new(true, "TEST-1", "https://example.atlassian.net/browse/TEST-1", null);

    /// <summary>Keyed by search query; defaults to no match (empty list) for any query not configured.</summary>
    public Dictionary<string, List<JiraUserSummary>> SearchResults { get; set; } = [];

    public Task<JiraConnectionTestResult> TestConnectionAsync(
        string baseUrl, string email, string apiToken, CancellationToken cancellationToken = default)
        => Task.FromResult(ConnectionTestResult);

    public Task<JiraCreateIssueResult> CreateIssueAsync(
        string baseUrl,
        string email,
        string apiToken,
        string projectKey,
        string issueType,
        string summary,
        string description,
        DateOnly? dueDate = null,
        string? assigneeAccountId = null,
        CancellationToken cancellationToken = default)
    {
        CreateIssueCalls.Add((projectKey, issueType, summary, assigneeAccountId));
        return Task.FromResult(CreateIssueResult);
    }

    public Task<List<JiraUserSummary>> SearchUsersAsync(
        string baseUrl, string email, string apiToken, string query, CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResults.GetValueOrDefault(query, []));
}
