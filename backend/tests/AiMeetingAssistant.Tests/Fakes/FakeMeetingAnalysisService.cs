using AiMeetingAssistant.Core.Dtos.Meetings;
using AiMeetingAssistant.Core.Services;
using AiMeetingAssistant.Infrastructure.Claude;

namespace AiMeetingAssistant.Tests.Fakes;

public class FakeMeetingAnalysisService : IMeetingAnalysisService
{
    public MeetingAnalysisResult? ResultToReturn { get; set; }
    public string? ExceptionMessageToThrow { get; set; }

    public Task<MeetingAnalysisResult> AnalyzeAsync(string transcriptText, string? apiKeyOverride = null, CancellationToken cancellationToken = default)
    {
        if (ExceptionMessageToThrow is not null)
        {
            throw new AnthropicApiException(ExceptionMessageToThrow);
        }

        return Task.FromResult(ResultToReturn ?? new MeetingAnalysisResult("Default summary.", [], []));
    }
}
