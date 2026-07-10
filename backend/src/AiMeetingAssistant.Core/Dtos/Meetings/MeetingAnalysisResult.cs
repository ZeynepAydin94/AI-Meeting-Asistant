namespace AiMeetingAssistant.Core.Dtos.Meetings;

public record ActionItemAnalysis(
    string Description,
    string? OwnerHint,
    string Priority,
    bool RequiresJiraTicket,
    string? SuggestedTicketTitle,
    string? SuggestedTicketDescription);

public record MeetingAnalysisResult(
    string Summary,
    List<string> KeyDecisions,
    List<ActionItemAnalysis> ActionItems);
