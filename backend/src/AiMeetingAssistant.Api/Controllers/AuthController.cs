using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AiMeetingAssistant.Core.Dtos.Auth;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AiMeetingAssistant.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> userManager, ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var (token, expiresAtUtc) = tokenService.GenerateToken(user.Id, user.Email!);
        return Ok(new AuthResponse(token, expiresAtUtc, user.Email!));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized();
        }

        var (token, expiresAtUtc) = tokenService.GenerateToken(user.Id, user.Email!);
        return Ok(new AuthResponse(token, expiresAtUtc, user.Email!));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        return Ok(new { email });
    }
}
