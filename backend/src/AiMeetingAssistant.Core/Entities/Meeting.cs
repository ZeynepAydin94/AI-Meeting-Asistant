namespace AiMeetingAssistant.Core.Entities;

public enum MeetingStatus
{
    Analyzing,
    Analyzed,
    Failed,
}

public class Meeting
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required string OriginalTranscriptText { get; set; }
    public MeetingStatus Status { get; set; }
    public string? SummaryText { get; set; }
    public string? ErrorMessage { get; set; }

    public List<KeyDecision> KeyDecisions { get; set; } = [];
    public List<ActionItem> ActionItems { get; set; } = [];
}
