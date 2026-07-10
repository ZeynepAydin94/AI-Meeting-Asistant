using AiMeetingAssistant.Core.Dtos.Meetings;

namespace AiMeetingAssistant.Core.Services;

public interface IMeetingAnalysisService
{
    Task<MeetingAnalysisResult> AnalyzeAsync(string transcriptText, string? apiKeyOverride = null, CancellationToken cancellationToken = default);
}
