namespace VulnX.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// "Gemini" or "OpenAI". Empty = automatic: Gemini when only Gemini:ApiKey is set, otherwise OpenAI.
    public string Provider { get; set; } = string.Empty;

    /// Secret. Set it only in user-secrets / environment variables, never in code. Empty = built-in explanations only.
    public string ApiKey { get; set; } = string.Empty;
    /// Any OpenAI-compatible API (the default is OpenAI).
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
}