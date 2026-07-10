using AiMeetingAssistant.Api.Extensions;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Settings;
using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiMeetingAssistant.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppDbContext db, IJiraClient jiraClient) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == User.GetUserId());
        return Ok(ToSettingsResponse(settings));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateSettingsRequest request)
    {
        var userId = User.GetUserId();
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

        return Ok(ToSettingsResponse(settings));
    }

    [HttpPost("test-jira-connection")]
    public async Task<IActionResult> TestJiraConnection()
    {
        var settings = await db.AppSettings.FirstOrDefaultAsync(s => s.UserId == User.GetUserId());
        if (settings is null
            || string.IsNullOrWhiteSpace(settings.JiraBaseUrl)
            || string.IsNullOrWhiteSpace(settings.JiraEmail)
            || string.IsNullOrWhiteSpace(settings.JiraApiToken))
        {
            return Ok(new JiraConnectionTestResult(false, "Jira base URL, email, and API token are all required."));
        }

        var result = await jiraClient.TestConnectionAsync(settings.JiraBaseUrl, settings.JiraEmail, settings.JiraApiToken);
        return Ok(result);
    }

    private static SettingsResponse ToSettingsResponse(AppSettings? settings) => new(
        !string.IsNullOrEmpty(settings?.ClaudeApiKey),
        settings?.JiraBaseUrl,
        settings?.JiraEmail,
        !string.IsNullOrEmpty(settings?.JiraApiToken),
        settings?.JiraDefaultProjectKey,
        settings?.JiraDefaultIssueType);
}
