using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AiMeetingAssistant.Core.Dtos.Jira;
using AiMeetingAssistant.Core.Services;

namespace AiMeetingAssistant.Infrastructure.Jira;

public class JiraClient(HttpClient httpClient) : IJiraClient
{
    public async Task<JiraConnectionTestResult> TestConnectionAsync(
        string baseUrl,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(baseUrl, "/rest/api/3/myself"));
        request.Headers.Authorization = BuildAuthHeader(email, apiToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<JiraMyselfResponse>(cancellationToken: cancellationToken);
            return new JiraConnectionTestResult(true, $"Connected as {body?.DisplayName ?? email}.");
        }

        return new JiraConnectionTestResult(false, await DescribeErrorAsync(response, cancellationToken));
    }

    public async Task<JiraCreateIssueResult> CreateIssueAsync(
        string baseUrl,
        string email,
        string apiToken,
        string projectKey,
        string issueType,
        string summary,
        string description,
        CancellationToken cancellationToken = default)
    {
        var requestBody = new JiraCreateIssueRequest(
            new JiraCreateIssueFields(
                new JiraProjectRef(projectKey),
                summary,
                new JiraIssueTypeRef(issueType),
                new JiraDescriptionDoc(
                    "doc",
                    1,
                    [new JiraDocParagraph("paragraph", [new JiraDocText("text", description)])])));

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(baseUrl, "/rest/api/3/issue"))
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Authorization = BuildAuthHeader(email, apiToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<JiraCreateIssueResponse>(cancellationToken: cancellationToken);
            if (body?.Key is null)
            {
                return new JiraCreateIssueResult(false, null, null, "Jira did not return an issue key.");
            }

            return new JiraCreateIssueResult(true, body.Key, $"{baseUrl.TrimEnd('/')}/browse/{body.Key}", null);
        }

        return new JiraCreateIssueResult(false, null, null, await DescribeErrorAsync(response, cancellationToken));
    }

    private static AuthenticationHeaderValue BuildAuthHeader(string email, string apiToken)
    {
        var raw = Encoding.UTF8.GetBytes($"{email}:{apiToken}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
    }

    private static string CombineUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}{path}";

    private static async Task<string> DescribeErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Invalid email or API token.";
        }

        JiraErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<JiraErrorResponse>(cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            // Jira didn't return a JSON error body; fall through to the generic message below.
        }

        var messages = new List<string>();
        if (error?.ErrorMessages is { Count: > 0 })
        {
            messages.AddRange(error.ErrorMessages);
        }
        if (error?.Errors is { Count: > 0 })
        {
            messages.AddRange(error.Errors.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        return messages.Count > 0
            ? string.Join(" ", messages)
            : $"Jira request failed with status {(int)response.StatusCode}.";
    }
}
