using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiMeetingAssistant.Api.Extensions;
using AiMeetingAssistant.Core.Dtos.Auth;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Dtos.Settings;
using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Auth;
using AiMeetingAssistant.Infrastructure.Claude;
using AiMeetingAssistant.Infrastructure.Data;
using AiMeetingAssistant.Infrastructure.Identity;
using AiMeetingAssistant.Infrastructure.Jira;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<IAnthropicClient, AnthropicClient>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
});
builder.Services.AddScoped<IMeetingAnalysisService, MeetingAnalysisService>();

builder.Services.AddHttpClient<IJiraClient, JiraClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

var auth = app.MapGroup("/api/auth");

auth.MapPost("/register", async (RegisterRequest request, UserManager<AppUser> userManager, ITokenService tokenService) =>
{
    var user = new AppUser { UserName = request.Email, Email = request.Email };
    var result = await userManager.CreateAsync(user, request.Password);

    if (!result.Succeeded)
    {
        return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
    }

    var (token, expiresAtUtc) = tokenService.GenerateToken(user.Id, user.Email!);
    return Results.Ok(new AuthResponse(token, expiresAtUtc, user.Email!));
});

auth.MapPost("/login", async (LoginRequest request, UserManager<AppUser> userManager, ITokenService tokenService) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
    {
        return Results.Unauthorized();
    }

    var (token, expiresAtUtc) = tokenService.GenerateToken(user.Id, user.Email!);
    return Results.Ok(new AuthResponse(token, expiresAtUtc, user.Email!));
});

auth.MapGet("/me", (ClaimsPrincipal user) =>
{
    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
    return Results.Ok(new { email });
}).RequireAuthorization();

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
            results.Add(new JiraTicketResultDto(actionItemId, false, null, null, "Action item not found."));
            continue;
        }

        item.UserConfirmed = true;

        string? assigneeAccountId = null;
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
            CreatedAtUtc = DateTime.UtcNow,
        });

        results.Add(new JiraTicketResultDto(item.Id, createResult.Success, createResult.IssueKey, createResult.IssueUrl, createResult.ErrorMessage));
    }

    await db.SaveChangesAsync();

    return Results.Ok(results);
});

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

app.Run();

static MeetingDetailDto ToDetailDto(Meeting meeting) => new(
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
            latestTicket?.ErrorMessage);
    }).ToList());

static SettingsResponse ToSettingsResponse(AppSettings? settings) => new(
    !string.IsNullOrEmpty(settings?.ClaudeApiKey),
    settings?.JiraBaseUrl,
    settings?.JiraEmail,
    !string.IsNullOrEmpty(settings?.JiraApiToken),
    settings?.JiraDefaultProjectKey,
    settings?.JiraDefaultIssueType);

static string DeriveTitle(string summary)
{
    var trimmed = summary.Trim();
    var periodIndex = trimmed.IndexOf('.');
    var candidate = periodIndex is > 10 and < 80 ? trimmed[..periodIndex] : trimmed;
    return candidate.Length > 60 ? candidate[..60].TrimEnd() + "…" : candidate;
}
