using System.Net;
using System.Net.Http.Json;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Settings;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Tests.Fakes;
using Xunit;

namespace AiMeetingAssistant.Tests.Integration;

public class SettingsControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly FakeJiraClient _jiraClient = new();
    private readonly HttpClient _client;

    public SettingsControllerTests()
    {
        _factory.ReplaceService<IJiraClient>(_jiraClient);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_WithNoSettingsSavedYet_ReturnsAllUnsetFields()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings").WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<SettingsResponse>();
        Assert.False(settings!.HasClaudeApiKey);
        Assert.False(settings.HasJiraApiToken);
        Assert.Null(settings.JiraBaseUrl);
    }

    [Fact]
    public async Task Put_SavesNonSecretFieldsAndMasksSecretsOnRead()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/settings")
        {
            Content = JsonContent.Create(new UpdateSettingsRequest(
                "sk-ant-real-key", "https://example.atlassian.net", "you@example.com", "real-token", "TEST", "Task"))
        }.WithAuth(token);
        var putResponse = await _client.SendAsync(putRequest);
        var putBody = await putResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        // Secrets are reported as booleans, never echoed back.
        Assert.True(putBody!.HasClaudeApiKey);
        Assert.True(putBody.HasJiraApiToken);
        Assert.Equal("https://example.atlassian.net", putBody.JiraBaseUrl);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/settings").WithAuth(token);
        var getResponse = await _client.SendAsync(getRequest);
        var getBody = await getResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.True(getBody!.HasClaudeApiKey);
        Assert.Equal("TEST", getBody.JiraDefaultProjectKey);
    }

    [Fact]
    public async Task Put_WithBlankSecretField_LeavesExistingSecretUnchanged()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);

        using var firstPut = new HttpRequestMessage(HttpMethod.Put, "/api/settings")
        {
            Content = JsonContent.Create(new UpdateSettingsRequest(
                "sk-ant-original", "https://example.atlassian.net", "you@example.com", "original-token", "TEST", "Task"))
        }.WithAuth(token);
        await _client.SendAsync(firstPut);

        // Second update omits the secret fields (simulating the frontend leaving them blank).
        using var secondPut = new HttpRequestMessage(HttpMethod.Put, "/api/settings")
        {
            Content = JsonContent.Create(new UpdateSettingsRequest(
                null, "https://updated.atlassian.net", "you@example.com", null, "TEST", "Bug"))
        }.WithAuth(token);
        var response = await _client.SendAsync(secondPut);
        var body = await response.Content.ReadFromJsonAsync<SettingsResponse>();

        // Secrets remain set (not wiped out by the blank update) while non-secret fields did change.
        Assert.True(body!.HasClaudeApiKey);
        Assert.True(body.HasJiraApiToken);
        Assert.Equal("https://updated.atlassian.net", body.JiraBaseUrl);
        Assert.Equal("Bug", body.JiraDefaultIssueType);
    }

    [Fact]
    public async Task TestJiraConnection_WithoutSettingsSaved_ReturnsFailureWithoutCallingJira()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/settings/test-jira-connection").WithAuth(token);
        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<JiraConnectionTestResult>();

        Assert.False(result!.Success);
    }

    [Fact]
    public async Task TestJiraConnection_WithSettingsSaved_ReturnsTheFakeClientsResult()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _jiraClient.ConnectionTestResult = new JiraConnectionTestResult(true, "Connected as Zeynep Aydın.");

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/settings")
        {
            Content = JsonContent.Create(new UpdateSettingsRequest(
                null, "https://example.atlassian.net", "you@example.com", "fake-token", "TEST", "Task"))
        }.WithAuth(token);
        await _client.SendAsync(putRequest);

        using var testRequest = new HttpRequestMessage(HttpMethod.Post, "/api/settings/test-jira-connection").WithAuth(token);
        var response = await _client.SendAsync(testRequest);
        var result = await response.Content.ReadFromJsonAsync<JiraConnectionTestResult>();

        Assert.True(result!.Success);
        Assert.Equal("Connected as Zeynep Aydın.", result.Message);
    }
}
