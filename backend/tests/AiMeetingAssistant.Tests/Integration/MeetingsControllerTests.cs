using System.Net;
using System.Net.Http.Json;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Dtos.Settings;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Tests.Fakes;
using Xunit;

namespace AiMeetingAssistant.Tests.Integration;

public class MeetingsControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly FakeMeetingAnalysisService _analysisService = new();
    private readonly FakeJiraClient _jiraClient = new();
    private readonly HttpClient _client;

    public MeetingsControllerTests()
    {
        _factory.ReplaceService<IMeetingAnalysisService>(_analysisService);
        _factory.ReplaceService<IJiraClient>(_jiraClient);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Create_WithSuccessfulAnalysis_ReturnsAnalyzedMeeting()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _analysisService.ResultToReturn = new MeetingAnalysisResult(
            "The team agreed to ship on Friday.",
            ["Ship on Friday."],
            [new ActionItemAnalysis("Write release notes.", "Zeynep", "Medium", true, "Write release notes", "Draft notes.", null)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest("Some transcript text."))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var meeting = await response.Content.ReadFromJsonAsync<MeetingDetailDto>();
        Assert.Equal("Analyzed", meeting!.Status);
        Assert.Single(meeting.KeyDecisions);
        Assert.Single(meeting.ActionItems);
        Assert.Equal("Zeynep", meeting.ActionItems[0].AssigneeHint);
    }

    [Fact]
    public async Task Create_WithEmptyTranscript_ReturnsBadRequest()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest(""))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenAnalysisFails_ReturnsUnprocessableEntityAndPersistsFailedMeeting()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _analysisService.ExceptionMessageToThrow = "Claude returned an analysis in an unexpected format.";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest("Some transcript text."))
        }.WithAuth(token);
        var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, createResponse.StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/meetings").WithAuth(token);
        var listResponse = await _client.SendAsync(listRequest);
        var meetings = await listResponse.Content.ReadFromJsonAsync<List<MeetingSummaryDto>>();

        Assert.Single(meetings!);
        Assert.Equal("Failed", meetings![0].Status);
    }

    [Fact]
    public async Task List_OnlyReturnsTheCurrentUsersMeetings()
    {
        var tokenA = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        var tokenB = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _analysisService.ResultToReturn = new MeetingAnalysisResult("Summary.", [], []);

        using var createForA = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest("Transcript for user A."))
        }.WithAuth(tokenA);
        await _client.SendAsync(createForA);

        using var listForB = new HttpRequestMessage(HttpMethod.Get, "/api/meetings").WithAuth(tokenB);
        var responseForB = await _client.SendAsync(listForB);
        var meetingsForB = await responseForB.Content.ReadFromJsonAsync<List<MeetingSummaryDto>>();

        Assert.Empty(meetingsForB!);
    }

    [Fact]
    public async Task GetById_ForAnotherUsersMeeting_ReturnsNotFound()
    {
        var tokenA = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        var tokenB = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _analysisService.ResultToReturn = new MeetingAnalysisResult("Summary.", [], []);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest("Transcript for user A."))
        }.WithAuth(tokenA);
        var createResponse = await _client.SendAsync(createRequest);
        var meeting = await createResponse.Content.ReadFromJsonAsync<MeetingDetailDto>();

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/meetings/{meeting!.Id}").WithAuth(tokenB);
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateJiraTickets_WithoutJiraSettingsConfigured_ReturnsBadRequest()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        _analysisService.ResultToReturn = new MeetingAnalysisResult(
            "Summary.", [], [new ActionItemAnalysis("Do the thing.", "Zeynep", "Medium", true, "Title", "Desc", null)]);

        var meetingId = await CreateMeetingAsync(token);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/meetings/{meetingId}/jira-tickets")
        {
            Content = JsonContent.Create(new CreateJiraTicketsRequest([]))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJiraTickets_WithUnambiguousUserMatch_AssignsTheTicket()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        await ConfigureJiraSettingsAsync(token);
        _jiraClient.SearchResults["Zeynep"] = [new JiraUserSummary("account-123", "Zeynep Aydın")];
        _analysisService.ResultToReturn = new MeetingAnalysisResult(
            "Summary.", [], [new ActionItemAnalysis("Fix the bug.", "Zeynep", "Urgent", true, "Fix bug", "Details", null)]);

        var meetingId = await CreateMeetingAsync(token);
        var actionItemId = await GetFirstActionItemIdAsync(token, meetingId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/meetings/{meetingId}/jira-tickets")
        {
            Content = JsonContent.Create(new CreateJiraTicketsRequest([actionItemId]))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<JiraTicketResultDto>>();
        Assert.Equal("Zeynep Aydın", results![0].AssignedDisplayName);
        Assert.Equal("account-123", _jiraClient.CreateIssueCalls[0].AssigneeAccountId);
    }

    [Fact]
    public async Task CreateJiraTickets_WithNoMatchingUser_LeavesTicketUnassigned()
    {
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(_client);
        await ConfigureJiraSettingsAsync(token);
        // No entry in SearchResults for "Ahmet" => FakeJiraClient returns an empty match list.
        _analysisService.ResultToReturn = new MeetingAnalysisResult(
            "Summary.", [], [new ActionItemAnalysis("Update the docs.", "Ahmet", "Low", true, "Update docs", "Details", null)]);

        var meetingId = await CreateMeetingAsync(token);
        var actionItemId = await GetFirstActionItemIdAsync(token, meetingId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/meetings/{meetingId}/jira-tickets")
        {
            Content = JsonContent.Create(new CreateJiraTicketsRequest([actionItemId]))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<JiraTicketResultDto>>();
        Assert.Null(results![0].AssignedDisplayName);
        Assert.Null(_jiraClient.CreateIssueCalls[0].AssigneeAccountId);
    }

    private async Task<Guid> CreateMeetingAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new CreateMeetingRequest("Some transcript text."))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);
        var meeting = await response.Content.ReadFromJsonAsync<MeetingDetailDto>();
        return meeting!.Id;
    }

    private async Task<Guid> GetFirstActionItemIdAsync(string token, Guid meetingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/meetings/{meetingId}").WithAuth(token);
        var response = await _client.SendAsync(request);
        var meeting = await response.Content.ReadFromJsonAsync<MeetingDetailDto>();
        return meeting!.ActionItems[0].Id;
    }

    private async Task ConfigureJiraSettingsAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/settings")
        {
            Content = JsonContent.Create(new UpdateSettingsRequest(
                null, "https://example.atlassian.net", "you@example.com", "fake-token", "TEST", "Task"))
        }.WithAuth(token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
