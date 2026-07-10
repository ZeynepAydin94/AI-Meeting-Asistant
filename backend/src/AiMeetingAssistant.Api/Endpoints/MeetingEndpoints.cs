using System.Security.Claims;
using AiMeetingAssistant.Api.Extensions;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Claude;
using AiMeetingAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiMeetingAssistant.Api.Endpoints;

public static class MeetingEndpoints
{
    public static void MapMeetingEndpoints(this WebApplication app)
    {
        var meetings = app.MapGroup("/api/meetings").RequireAuthorization();

        meetings.MapPost("/", async (
            CreateMeetingRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            IMeetingAnalysisService analysisService) =>
        {
            if (string.IsNullOrWhiteSpace(request.TranscriptText))
            {
                return Results.BadRequest(new { message = "Transcript text is required." });
            }

            var userId = user.GetUserId();
            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Untitled meeting",
                CreatedAtUtc = DateTime.UtcNow,
                OriginalTranscriptText = request.TranscriptText,
                Status = MeetingStatus.Analyzing,
            };

            try
            {
                var userSettings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == userId);
                var analysis = await analysisService.AnalyzeAsync(request.TranscriptText, userSettings?.ClaudeApiKey);

                meeting.Status = MeetingStatus.Analyzed;
                meeting.Title = DeriveTitle(analysis.Summary);
                meeting.SummaryText = analysis.Summary;
                meeting.KeyDecisions = analysis.KeyDecisions
                    .Select(description => new KeyDecision { Id = Guid.NewGuid(), MeetingId = meeting.Id, Description = description })
                    .ToList();
                meeting.ActionItems = analysis.ActionItems
                    .Select(item => new ActionItem
                    {
                        Id = Guid.NewGuid(),
                        MeetingId = meeting.Id,
                        Description = item.Description,
                        AssigneeHint = item.OwnerHint,
                        Priority = Enum.TryParse<ActionItemPriority>(item.Priority, ignoreCase: true, out var priority)
                            ? priority
                            : ActionItemPriority.Medium,
                        SuggestedForJira = item.RequiresJiraTicket,
                        SuggestedTicketTitle = item.SuggestedTicketTitle,
                        SuggestedTicketDescription = item.SuggestedTicketDescription,
                        DueDate = item.DueDate,
                    })
                    .ToList();
            }
            catch (AnthropicApiException ex)
            {
                meeting.Status = MeetingStatus.Failed;
                meeting.Title = "Analysis failed";
                meeting.ErrorMessage = ex.Message;
            }

            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();

            return meeting.Status == MeetingStatus.Failed
                ? Results.UnprocessableEntity(new { message = meeting.ErrorMessage })
                : Results.Ok(ToDetailDto(meeting));
        });

        meetings.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var list = await db.Meetings
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Select(m => new MeetingSummaryDto(m.Id, m.Title, m.CreatedAtUtc, m.Status.ToString(), m.ActionItems.Count))
                .ToListAsync();

            return Results.Ok(list);
        });

        meetings.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var meeting = await db.Meetings
                .Include(m => m.KeyDecisions)
                .Include(m => m.ActionItems).ThenInclude(ai => ai.JiraTickets)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            return meeting is null ? Results.NotFound() : Results.Ok(ToDetailDto(meeting));
        });

        meetings.MapPost("/{id:guid}/jira-tickets", async (
            Guid id,
            CreateJiraTicketsRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            IJiraClient jiraClient) =>
        {
            var userId = user.GetUserId();
            var meeting = await db.Meetings
                .Include(m => m.ActionItems).ThenInclude(ai => ai.JiraTickets)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (meeting is null)
            {
                return Results.NotFound();
            }

            var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == userId);
            if (settings is null
                || string.IsNullOrWhiteSpace(settings.JiraBaseUrl)
                || string.IsNullOrWhiteSpace(settings.JiraEmail)
                || string.IsNullOrWhiteSpace(settings.JiraApiToken)
                || string.IsNullOrWhiteSpace(settings.JiraDefaultProjectKey)
                || string.IsNullOrWhiteSpace(settings.JiraDefaultIssueType))
            {
                return Results.BadRequest(new { message = "Configure your Jira connection in Settings before creating tickets." });
            }

            var results = new List<JiraTicketResultDto>();

            foreach (var actionItemId in request.ActionItemIds)
            {
                var item = meeting.ActionItems.FirstOrDefault(ai => ai.Id == actionItemId);
                if (item is null)
                {
                    results.Add(new JiraTicketResultDto(actionItemId, false, null, null, "Action item not found.", null));
                    continue;
                }

                item.UserConfirmed = true;

                string? assigneeAccountId = null;
                string? assigneeDisplayName = null;
                if (!string.IsNullOrWhiteSpace(item.AssigneeHint))
                {
                    var matches = await jiraClient.SearchUsersAsync(
                        settings.JiraBaseUrl,
                        settings.JiraEmail,
                        settings.JiraApiToken,
                        item.AssigneeHint);

                    // Only auto-assign on an unambiguous single match — guessing wrong is worse than leaving it unassigned.
                    if (matches.Count == 1)
                    {
                        assigneeAccountId = matches[0].AccountId;
                        assigneeDisplayName = matches[0].DisplayName;
                    }
                }

                var createResult = await jiraClient.CreateIssueAsync(
                    settings.JiraBaseUrl,
                    settings.JiraEmail,
                    settings.JiraApiToken,
                    settings.JiraDefaultProjectKey,
                    settings.JiraDefaultIssueType,
                    item.SuggestedTicketTitle ?? item.Description,
                    item.SuggestedTicketDescription ?? item.Description,
                    item.DueDate,
                    assigneeAccountId);

                db.JiraTickets.Add(new JiraTicket
                {
                    Id = Guid.NewGuid(),
                    ActionItemId = item.Id,
                    Status = createResult.Success ? JiraTicketStatus.Created : JiraTicketStatus.Failed,
                    JiraIssueKey = createResult.IssueKey,
                    JiraIssueUrl = createResult.IssueUrl,
                    ErrorMessage = createResult.ErrorMessage,
                    AssignedDisplayName = createResult.Success ? assigneeDisplayName : null,
                    CreatedAtUtc = DateTime.UtcNow,
                });

                results.Add(new JiraTicketResultDto(
                    item.Id,
                    createResult.Success,
                    createResult.IssueKey,
                    createResult.IssueUrl,
                    createResult.ErrorMessage,
                    createResult.Success ? assigneeDisplayName : null));
            }

            await db.SaveChangesAsync();

            return Results.Ok(results);
        });
    }

    private static MeetingDetailDto ToDetailDto(Meeting meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.CreatedAtUtc,
        meeting.Status.ToString(),
        meeting.SummaryText,
        meeting.ErrorMessage,
        meeting.KeyDecisions.Select(kd => new KeyDecisionDto(kd.Id, kd.Description)).ToList(),
        meeting.ActionItems.Select(ai =>
        {
            var latestTicket = ai.JiraTickets.OrderByDescending(t => t.CreatedAtUtc).FirstOrDefault();
            return new ActionItemDto(
                ai.Id,
                ai.Description,
                ai.AssigneeHint,
                ai.Priority.ToString(),
                ai.SuggestedForJira,
                ai.UserConfirmed,
                ai.SuggestedTicketTitle,
                ai.SuggestedTicketDescription,
                ai.DueDate,
                latestTicket?.Status.ToString(),
                latestTicket?.JiraIssueKey,
                latestTicket?.JiraIssueUrl,
                latestTicket?.ErrorMessage,
                latestTicket?.AssignedDisplayName);
        }).ToList());

    private static string DeriveTitle(string summary)
    {
        var trimmed = summary.Trim();
        var periodIndex = trimmed.IndexOf('.');
        var candidate = periodIndex is > 10 and < 80 ? trimmed[..periodIndex] : trimmed;
        return candidate.Length > 60 ? candidate[..60].TrimEnd() + "…" : candidate;
    }
}
