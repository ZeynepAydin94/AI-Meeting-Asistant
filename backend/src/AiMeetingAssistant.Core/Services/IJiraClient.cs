using AiMeetingAssistant.Core.Dtos.Jira;

namespace AiMeetingAssistant.Core.Services;

public interface IJiraClient
{
    Task<JiraConnectionTestResult> TestConnectionAsync(
        string baseUrl,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default);

    Task<JiraCreateIssueResult> CreateIssueAsync(
        string baseUrl,
        string email,
        string apiToken,
        string projectKey,
        string issueType,
        string summary,
        string description,
        CancellationToken cancellationToken = default);
}
