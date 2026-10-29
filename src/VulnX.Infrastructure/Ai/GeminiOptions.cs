
namespace VulnX.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    // Must be a model supported by your Google AI Studio API key.
    public string Model { get; set; } = "gemini-3.5-flash";
}
