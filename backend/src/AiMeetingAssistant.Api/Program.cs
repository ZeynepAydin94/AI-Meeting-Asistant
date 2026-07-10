using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiMeetingAssistant.Api.Extensions;
using AiMeetingAssistant.Core.Dtos.Auth;
using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Auth;
using AiMeetingAssistant.Infrastructure.Claude;
using AiMeetingAssistant.Infrastructure.Data;
using AiMeetingAssistant.Infrastructure.Identity;
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

    var meeting = new Meeting
    {
        Id = Guid.NewGuid(),
        UserId = user.GetUserId(),
        Title = "Untitled meeting",
        CreatedAtUtc = DateTime.UtcNow,
        OriginalTranscriptText = request.TranscriptText,
        Status = MeetingStatus.Analyzing,
    };

    try
    {
        var analysis = await analysisService.AnalyzeAsync(request.TranscriptText);

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
        .Include(m => m.ActionItems)
        .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

    return meeting is null ? Results.NotFound() : Results.Ok(ToDetailDto(meeting));
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
    meeting.ActionItems.Select(ai => new ActionItemDto(
        ai.Id,
        ai.Description,
        ai.AssigneeHint,
        ai.Priority.ToString(),
        ai.SuggestedForJira,
        ai.UserConfirmed,
        ai.SuggestedTicketTitle,
        ai.SuggestedTicketDescription)).ToList());

static string DeriveTitle(string summary)
{
    var trimmed = summary.Trim();
    var periodIndex = trimmed.IndexOf('.');
    var candidate = periodIndex is > 10 and < 80 ? trimmed[..periodIndex] : trimmed;
    return candidate.Length > 60 ? candidate[..60].TrimEnd() + "…" : candidate;
}
