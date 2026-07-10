namespace AiMeetingAssistant.Core.Entities;

public class KeyDecision
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public required string Description { get; set; }
}
