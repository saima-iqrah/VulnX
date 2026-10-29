namespace VulnX.Application.Common;

/// Expected, user-facing failure (validation, auth, not-found...). Mapped to ProblemDetails by the API.
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
