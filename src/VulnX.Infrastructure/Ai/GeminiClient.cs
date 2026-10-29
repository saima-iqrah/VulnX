using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VulnX.Infrastructure.Ai;

internal sealed class GeminiClient(
    HttpClient http,
    IOptions<GeminiOptions> options,
    ILogger<GeminiClient> logger) : IAiClient
{
    // ---- Throttle: Gemini 2.5 Flash Lite free tier 10 RPM, tai 9 rakhlam margin er jonno ----
    // Model change korle (jemon billing on kore 3.6 Flash), MaxPerMinute = notun RPM - 1 koro.
    // static, jate GeminiClient er shob instance same limit share kore.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Queue<DateTime> Calls = new();
    private const int MaxPerMinute = 9;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private static async Task ThrottleAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            while (true)
            {
                var now = DateTime.UtcNow;

                // Purano (60s er age) call gulo window theke bad dao.
                while (Calls.Count > 0 && now - Calls.Peek() >= Window)
                    Calls.Dequeue();

                if (Calls.Count < MaxPerMinute)
                {
                    Calls.Enqueue(now);
                    return;
                }

                // Limit full, shobcheye purano call expire howa porjonto wait.
                var wait = Window - (now - Calls.Peek());
                if (wait < TimeSpan.FromMilliseconds(50))
                    wait = TimeSpan.FromMilliseconds(50);

                await Task.Delay(wait, ct);
            }
        }
        finally
        {
            Gate.Release();
        }
    }
    // ---- Throttle shesh ----

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.ApiKey) &&
        !string.IsNullOrWhiteSpace(options.Value.Model);

    public async Task<string?> CompleteAsync(
        string system,
        IReadOnlyList<AiTurn> turns,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Gemini is not configured.");
            return null;
        }

        var contents = ToContents(turns);

        if (contents.Count == 0)
            return null;

        var settings = options.Value;
        var model = settings.Model.Trim();

        if (model.StartsWith(
            "models/", StringComparison.OrdinalIgnoreCase))
        {
            model = model["models/".Length..];
        }

        var url =
            $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

        var systemPrompt = system + """

            LANGUAGE INSTRUCTIONS:
            - Always respond to the user's latest message.
            - Understand Banglish: Bangla written using English letters.
            - If the user writes in Banglish, reply in natural Banglish.
            - If the user writes in Bengali script, reply in Bengali script.
            - If the user writes in English, reply in English.
            - Understand informal spelling, abbreviations, and common
              Banglish spelling variations.
            - Do not ignore a message just because it is written in Banglish.
            - Keep the answer clear and helpful.
            - If the request is unclear, ask a short clarification question.
            """;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Prottek actual HTTP call er age rate limit check.
                await ThrottleAsync(ct);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post, url);

                request.Headers.Add(
                    "x-goog-api-key", settings.ApiKey);

                request.Content = JsonContent.Create(new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = systemPrompt }
                        }
                    },
                    contents,
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 4096
                    }
                });

                using var response =
                    await http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var body =
                        await response.Content.ReadAsStringAsync(ct);

                    using var doc = JsonDocument.Parse(body);

                    if (!doc.RootElement.TryGetProperty(
                            "candidates", out var candidates) ||
                        candidates.GetArrayLength() == 0)
                    {
                        logger.LogWarning(
                            "Gemini returned no candidates.");
                        return null;
                    }

                    if (!candidates[0].TryGetProperty(
                            "content", out var content) ||
                        !content.TryGetProperty(
                            "parts", out var parts))
                    {
                        logger.LogWarning(
                            "Gemini returned an unexpected response.");
                        return null;
                    }

                    var result = string.Join(
                        "\n",
                        parts.EnumerateArray()
                            .Where(p =>
                                p.TryGetProperty("text", out _))
                            .Select(p =>
                                p.GetProperty("text").GetString())
                            .Where(t =>
                                !string.IsNullOrWhiteSpace(t)));

                    if (string.IsNullOrWhiteSpace(result))
                    {
                        logger.LogWarning(
                            "Gemini returned an empty text response.");
                        return null;
                    }

                    return result;
                }

                var status = (int)response.StatusCode;
                var errorBody =
                    await response.Content.ReadAsStringAsync(ct);

                // 1000 char, jate quotaMetric / quotaId dekha jay.
                logger.LogWarning(
                    "Gemini returned HTTP {Status}: {Body}",
                    status,
                    errorBody.Length > 1000
                        ? errorBody[..1000]
                        : errorBody);

                // 429 e retry korle quota aro jole, tai retry nai.
                // Shudhu server-side (5xx) error e retry hobe.
                if (status < 500)
                    return null;

                if (attempt == 2)
                    return null;
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    ex, "Gemini HTTP request failed.");

                if (attempt == 2)
                    return null;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex, "Gemini returned invalid JSON.");
                return null;
            }

            // Exponential backoff: 1 second, then 2 seconds.
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }

        return null;
    }

    private static List<object> ToContents(
        IReadOnlyList<AiTurn> turns)
    {
        var merged = new List<(string Role, string Text)>();

        foreach (var turn in turns)
        {
            if (string.IsNullOrWhiteSpace(turn.Content))
                continue;

            var role = turn.Role == "assistant"
                ? "model"
                : "user";

            if (merged.Count > 0 &&
                merged[^1].Role == role)
            {
                merged[^1] = (
                    role,
                    merged[^1].Text + "\n\n" + turn.Content);
            }
            else
            {
                merged.Add((role, turn.Content));
            }
        }

        // Gemini expects the first content turn to be from the user.
        while (merged.Count > 0 &&
               merged[0].Role == "model")
        {
            merged.RemoveAt(0);
        }

        return merged
            .Select(m => (object)new
            {
                role = m.Role,
                parts = new[]
                {
                    new { text = m.Text }
                }
            })
            .ToList();
    }
}