using System.Text;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Auth;
using AiMeetingAssistant.Infrastructure.Claude;
using AiMeetingAssistant.Infrastructure.Data;
using AiMeetingAssistant.Infrastructure.Identity;
using AiMeetingAssistant.Infrastructure.Jira;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured via the options pipeline (IOptions<JwtOptions>) rather than a raw config snapshot, so
// token validation always reads the same settings JwtTokenService used to create the token — a raw
// snapshot here previously went stale under WebApplicationFactory-based tests, where a config source
// added after WebApplication.CreateBuilder() ran didn't retroactively apply to an eagerly-read local.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var options = jwtOptions.Value;
        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
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

app.MapControllers();

app.Run();

// Makes the implicit Program class visible to WebApplicationFactory<Program> in the test project.
public partial class Program;
