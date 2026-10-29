using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VulnX.Infrastructure.Ai;

internal sealed record AiTurn(string Role, string Content);

internal interface IAiClient
{
    bool IsConfigured { get; }
    /// Returns null when the AI is not configured or the call failed (the caller then uses the built-in explanation).
    Task<string?> CompleteAsync(string system, IReadOnlyList<AiTurn> turns, CancellationToken ct);
}

internal sealed class OpenAiCompatibleClient(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiCompatibleClient> logger) : IAiClient
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<string?> CompleteAsync(string system, IReadOnlyList<AiTurn> turns, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        var o = options.Value;

        var messages = new List<object> { new { role = "system", content = system } };
        messages.AddRange(turns.Select(t => (object)new { role = t.Role, content = t.Content }));

        using var request = new HttpRequestMessage(HttpMethod.Post, o.BaseUrl.TrimEnd('/') + "/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        request.Content = JsonContent.Create(new { model = o.Model, messages });

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI provider returned {Status}: {Body}", (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "AI request failed");
            return null;
        }
    }
}