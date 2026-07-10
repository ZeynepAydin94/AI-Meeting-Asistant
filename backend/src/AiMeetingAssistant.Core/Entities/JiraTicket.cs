namespace AiMeetingAssistant.Core.Entities;

public enum JiraTicketStatus
{
    Created,
    Failed,
}

public class JiraTicket
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public JiraTicketStatus Status { get; set; }
    public string? JiraIssueKey { get; set; }
    public string? JiraIssueUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
