using AiMeetingAssistant.Infrastructure.Claude;
using AiMeetingAssistant.Tests.Fakes;
using Xunit;

namespace AiMeetingAssistant.Tests.Unit;

public class MeetingAnalysisServiceTests
{
    private const string WellFormed = """
        {
            "summary": "The team discussed the release plan.",
            "key_decisions": ["Ship on Friday."],
            "action_items": [
                {
                    "description": "Write release notes.",
                    "owner_hint": "Zeynep",
                    "priority": "Medium",
                    "requires_jira_ticket": true,
                    "suggested_ticket_title": "Write release notes",
                    "suggested_ticket_description": "Draft and publish release notes.",
                    "due_date": "2026-07-25"
                }
            ]
        }
        """;

    [Fact]
    public async Task AnalyzeAsync_WithWellFormedResponse_SucceedsOnFirstAttempt()
    {
        var client = new FakeAnthropicClient(WellFormed);
        var sut = new MeetingAnalysisService(client);

        var result = await sut.AnalyzeAsync("some transcript");

        Assert.Equal(1, client.CallCount);
        Assert.Equal("The team discussed the release plan.", result.Summary);
        Assert.Single(result.KeyDecisions);
        Assert.Single(result.ActionItems);
        Assert.Equal("Zeynep", result.ActionItems[0].OwnerHint);
        Assert.Equal(new DateOnly(2026, 7, 25), result.ActionItems[0].DueDate);
    }

    [Fact]
    public async Task AnalyzeAsync_WithKeyDecisionsAsObjectArray_FlattensToStrings()
    {
        // Reproduces the live bug: Claude sometimes returns key_decisions as objects
        // instead of plain strings.
        const string malformed = """
            {
                "summary": "Summary text.",
                "key_decisions": [{"decision": "Ship on Friday.", "owner": "team"}],
                "action_items": []
            }
            """;
        var client = new FakeAnthropicClient(malformed);
        var sut = new MeetingAnalysisService(client);

        var result = await sut.AnalyzeAsync("some transcript");

        Assert.Equal(1, client.CallCount);
        Assert.Equal(["Ship on Friday."], result.KeyDecisions);
    }

    [Fact]
    public async Task AnalyzeAsync_WithKeyDecisionsAsBareString_WrapsIntoSingleElementArray()
    {
        // Reproduces the live bug: Claude sometimes returns key_decisions as a single string
        // instead of an array at all.
        const string malformed = """
            {
                "summary": "Summary text.",
                "key_decisions": "Ship on Friday.",
                "action_items": []
            }
            """;
        var client = new FakeAnthropicClient(malformed);
        var sut = new MeetingAnalysisService(client);

        var result = await sut.AnalyzeAsync("some transcript");

        Assert.Equal(["Ship on Friday."], result.KeyDecisions);
    }

    [Fact]
    public async Task AnalyzeAsync_WithUnparsableActionItems_RetriesAndSucceedsOnNextAttempt()
    {
        // First response reproduces the live bug: action_items came back as a string containing
        // leaked tool-call-like syntax, which fails to deserialize into the expected object list.
        const string leakedSyntax = """
            {
                "summary": "Summary text.",
                "key_decisions": ["Ship on Friday."],
                "action_items": "<parameter name=\"0\">{\"description\":\"broken\"}"
            }
            """;
        var client = new FakeAnthropicClient(leakedSyntax, WellFormed);
        var sut = new MeetingAnalysisService(client);

        var result = await sut.AnalyzeAsync("some transcript");

        Assert.Equal(2, client.CallCount);
        Assert.Equal("The team discussed the release plan.", result.Summary);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullActionItems_TreatsAsIncompleteAndRetries()
    {
        // Reproduces the live bug: action_items came back as null, which deserialized "successfully"
        // but crashed downstream code that assumed a non-null collection.
        const string nullActionItems = """
            {
                "summary": "Summary text.",
                "key_decisions": ["Ship on Friday."],
                "action_items": null
            }
            """;
        var client = new FakeAnthropicClient(nullActionItems, WellFormed);
        var sut = new MeetingAnalysisService(client);

        var result = await sut.AnalyzeAsync("some transcript");

        Assert.Equal(2, client.CallCount);
        Assert.NotNull(result.ActionItems);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenEveryAttemptFails_ThrowsAfterMaxAttempts()
    {
        const string alwaysBroken = """{"summary":"S","key_decisions":"d","action_items":null}""";
        var client = new FakeAnthropicClient(alwaysBroken, alwaysBroken, alwaysBroken);
        var sut = new MeetingAnalysisService(client);

        await Assert.ThrowsAsync<AnthropicApiException>(() => sut.AnalyzeAsync("some transcript"));
        Assert.Equal(3, client.CallCount);
    }
}
