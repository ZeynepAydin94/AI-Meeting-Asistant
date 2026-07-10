namespace AiMeetingAssistant.Core.Dtos.Meetings;

public record CreateMeetingRequest(string TranscriptText);

public record KeyDecisionDto(Guid Id, string Description);

public record ActionItemDto(
    Guid Id,
    string Description,
    string? AssigneeHint,
    string Priority,
    bool SuggestedForJira,
    bool? UserConfirmed,
    string? SuggestedTicketTitle,
    string? SuggestedTicketDescription,
    string? JiraTicketStatus,
    string? JiraIssueKey,
    string? JiraIssueUrl,
    string? JiraErrorMessage);

public record MeetingSummaryDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    string Status,
    int ActionItemCount);

public record MeetingDetailDto(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    string Status,
    string? Summary,
    string? ErrorMessage,
    List<KeyDecisionDto> KeyDecisions,
    List<ActionItemDto> ActionItems);
