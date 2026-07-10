namespace AiMeetingAssistant.Core.Dtos.Jira;

public record JiraConnectionTestResult(bool Success, string? Message);

public record JiraCreateIssueResult(bool Success, string? IssueKey, string? IssueUrl, string? ErrorMessage);

public record CreateJiraTicketsRequest(List<Guid> ActionItemIds);

public record JiraTicketResultDto(Guid ActionItemId, bool Success, string? JiraIssueKey, string? JiraIssueUrl, string? ErrorMessage);
