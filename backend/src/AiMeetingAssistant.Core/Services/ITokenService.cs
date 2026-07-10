namespace AiMeetingAssistant.Core.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(string userId, string email);
}
