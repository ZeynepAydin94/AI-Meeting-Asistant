namespace AiMeetingAssistant.Core.Entities;

public class AppSettings
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public string? ClaudeApiKey { get; set; }
    public string? JiraBaseUrl { get; set; }
    public string? JiraEmail { get; set; }
    public string? JiraApiToken { get; set; }
    public string? JiraDefaultProjectKey { get; set; }
    public string? JiraDefaultIssueType { get; set; }
}
