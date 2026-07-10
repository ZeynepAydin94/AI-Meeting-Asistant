using System.Security.Claims;
using AiMeetingAssistant.Api.Extensions;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Settings;
using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiMeetingAssistant.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var settingsGroup = app.MapGroup("/api/settings").RequireAuthorization();

        settingsGroup.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == user.GetUserId());
            return Results.Ok(ToSettingsResponse(settings));
        });

        settingsGroup.MapPut("/", async (UpdateSettingsRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == userId);
            if (settings is null)
            {
                settings = new AppSettings { Id = Guid.NewGuid(), UserId = userId };
                db.AppSettings.Add(settings);
            }

            if (!string.IsNullOrWhiteSpace(request.ClaudeApiKey))
            {
                settings.ClaudeApiKey = request.ClaudeApiKey;
            }

            settings.JiraBaseUrl = request.JiraBaseUrl;
            settings.JiraEmail = request.JiraEmail;
            settings.JiraDefaultProjectKey = request.JiraDefaultProjectKey;
            settings.JiraDefaultIssueType = request.JiraDefaultIssueType;

            if (!string.IsNullOrWhiteSpace(request.JiraApiToken))
            {
                settings.JiraApiToken = request.JiraApiToken;
            }

            await db.SaveChangesAsync();

            return Results.Ok(ToSettingsResponse(settings));
        });

        settingsGroup.MapPost("/test-jira-connection", async (ClaimsPrincipal user, AppDbContext db, IJiraClient jiraClient) =>
        {
            var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == user.GetUserId());
            if (settings is null
                || string.IsNullOrWhiteSpace(settings.JiraBaseUrl)
                || string.IsNullOrWhiteSpace(settings.JiraEmail)
                || string.IsNullOrWhiteSpace(settings.JiraApiToken))
            {
                return Results.Ok(new JiraConnectionTestResult(false, "Jira base URL, email, and API token are all required."));
            }

            var result = await jiraClient.TestConnectionAsync(settings.JiraBaseUrl, settings.JiraEmail, settings.JiraApiToken);
            return Results.Ok(result);
        });
    }

    private static SettingsResponse ToSettingsResponse(AppSettings? settings) => new(
        !string.IsNullOrEmpty(settings?.ClaudeApiKey),
        settings?.JiraBaseUrl,
        settings?.JiraEmail,
        !string.IsNullOrEmpty(settings?.JiraApiToken),
        settings?.JiraDefaultProjectKey,
        settings?.JiraDefaultIssueType);
}
