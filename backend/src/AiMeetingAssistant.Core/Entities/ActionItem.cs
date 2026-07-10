namespace AiMeetingAssistant.Core.Entities;

public enum ActionItemPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

public class ActionItem
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public required string Description { get; set; }
    public string? AssigneeHint { get; set; }
    public ActionItemPriority Priority { get; set; }
    public bool SuggestedForJira { get; set; }
    public bool? UserConfirmed { get; set; }
    public string? SuggestedTicketTitle { get; set; }
    public string? SuggestedTicketDescription { get; set; }
    public DateOnly? DueDate { get; set; }

    public List<JiraTicket> JiraTickets { get; set; } = [];
}
