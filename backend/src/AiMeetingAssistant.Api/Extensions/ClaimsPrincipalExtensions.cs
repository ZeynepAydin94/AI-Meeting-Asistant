using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AiMeetingAssistant.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated user has no sub claim.");
    }
}
