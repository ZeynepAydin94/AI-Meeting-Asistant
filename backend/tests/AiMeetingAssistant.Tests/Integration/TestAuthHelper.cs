using System.Net.Http.Json;
using AiMeetingAssistant.Core.Dtos.Auth;

namespace AiMeetingAssistant.Tests.Integration;

public static class TestAuthHelper
{
    public static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email ?? $"{Guid.NewGuid()}@example.com", "TestPass123!"));
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    public static HttpRequestMessage WithAuth(this HttpRequestMessage request, string token)
    {
        request.Headers.Add("Authorization", $"Bearer {token}");
        return request;
    }
}
