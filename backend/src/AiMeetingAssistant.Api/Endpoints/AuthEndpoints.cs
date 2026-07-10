using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AiMeetingAssistant.Core.Dtos.Auth;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace AiMeetingAssistant.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
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
    }
}
