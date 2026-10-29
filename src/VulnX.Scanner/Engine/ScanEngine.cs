using Microsoft.Extensions.Logging;
using VulnX.Application.Abstractions;
using VulnX.Domain.Enums;
using VulnX.Scanner.Http;

namespace VulnX.Scanner.Engine;

public sealed record Finding(
    string Code, string AffectedUrl, string Evidence,
    Severity? SeverityOverride = null, Confidence Confidence = Confidence.High, string Module = "");

public sealed class ScanContext
{
    public required ValidatedTarget Target { get; init; }
    public required SafeHttpClient Http { get; init; }
    public HttpSnapshot? Root { get; set; }
    public string CurrentModule { get; set; } = string.Empty;
    public List<Finding> Findings { get; } = new();

    public void Add(Finding finding) => Findings.Add(finding with { Module = CurrentModule });
}

public interface IScanModule
{
    string Name { get; }
    Task RunAsync(ScanContext ctx, CancellationToken ct);
}

public sealed record ProfileSettings(int MaxRequests, int RequestTimeoutSeconds, int TotalTimeoutSeconds, int DelayMs, string[] Modules);

public static class ScanProfiles
{
    public const string Headers = "Security Headers";
    public const string Tls = "TLS / HTTPS";
    public const string Cookies = "Cookies";
    public const string Cors = "CORS";
    public const string Info = "Information Disclosure";
    public const string Html = "HTML Content";

    public static ProfileSettings For(ScanProfile profile) => profile switch
    {
        ScanProfile.Quick => new ProfileSettings(12, 10, 60, 250, new[] { Headers, Tls, Cookies }),
        ScanProfile.Standard => new ProfileSettings(30, 10, 120, 250, new[] { Headers, Tls, Cookies, Cors, Info, Html }),
        _ => new ProfileSettings(60, 15, 240, 400, new[] { Headers, Tls, Cookies, Cors, Info, Html })
    };
}

public sealed record ModuleOutcome(string Name, ModuleStatus Status, string? Error);
public sealed record ScanOutcome(IReadOnlyList<Finding> Findings, IReadOnlyList<ModuleOutcome> Modules);

public sealed class ScanTargetUnreachableException(string message) : Exception(message);

public interface IScanEngine
{
    Task<ScanOutcome> RunAsync(ValidatedTarget target, ScanProfile profile,
        Func<string, ModuleStatus, int, string?, Task> onModule, CancellationToken ct);
}

public sealed class ScanEngine(IEnumerable<IScanModule> modules, ILogger<ScanEngine> logger) : IScanEngine
{
    public async Task<ScanOutcome> RunAsync(ValidatedTarget target, ScanProfile profile,
        Func<string, ModuleStatus, int, string?, Task> onModule, CancellationToken ct)
    {
        var settings = ScanProfiles.For(profile);

        using var total = CancellationTokenSource.CreateLinkedTokenSource(ct);
        total.CancelAfter(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds));   // overall time limit

        using var http = new SafeHttpClient(target.Hostname, settings.MaxRequests,
            TimeSpan.FromSeconds(settings.RequestTimeoutSeconds), TimeSpan.FromMilliseconds(settings.DelayMs));
        var ctx = new ScanContext { Target = target, Http = http };

        // One shared request for the target page; every module reuses it.
        try { ctx.Root = await http.FetchAsync(target.Url, ct: total.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new ScanTargetUnreachableException("The target could not be reached: " + Short(ex)); }

        var selected = settings.Modules.Select(name => modules.First(m => m.Name == name)).ToList();
        var outcomes = new List<ModuleOutcome>();

        for (var i = 0; i < selected.Count; i++)
        {
            var module = selected[i];
            var before = i * 100 / selected.Count;
            var after = (i + 1) * 100 / selected.Count;

            if (total.IsCancellationRequested)
            {
                outcomes.Add(new ModuleOutcome(module.Name, ModuleStatus.Skipped, "Scan time limit reached"));
                await onModule(module.Name, ModuleStatus.Skipped, after, "Scan time limit reached");
                continue;
            }

            await onModule(module.Name, ModuleStatus.Running, before, null);
            ctx.CurrentModule = module.Name;

            ModuleOutcome result;
            try
            {
                await module.RunAsync(ctx, total.Token);
                result = new ModuleOutcome(module.Name, ModuleStatus.Completed, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) when (total.IsCancellationRequested)
            {
                result = new ModuleOutcome(module.Name, ModuleStatus.Failed, "Scan time limit reached");
            }
            catch (ScanBudgetExceededException)
            {
                result = new ModuleOutcome(module.Name, ModuleStatus.Skipped, "Request budget exhausted");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scanner module {Module} failed", module.Name);
                result = new ModuleOutcome(module.Name, ModuleStatus.Failed, Short(ex));
            }

            outcomes.Add(result);
            await onModule(result.Name, result.Status, after, result.Error);
        }

        return new ScanOutcome(ctx.Findings, outcomes);
    }

    private static string Short(Exception ex)
    {
        var message = (ex.InnerException ?? ex).Message;
        return message.Length > 200 ? message[..200] : message;
    }
}