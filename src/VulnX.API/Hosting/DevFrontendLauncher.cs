using System.Diagnostics;
using System.Net.Sockets;

namespace VulnX.API.Hosting;

/// Development only: starts the Vite dev server (npm run dev) together with the API, so one F5 runs both.
public sealed class DevFrontendLauncher(IWebHostEnvironment env, ILogger<DevFrontendLauncher> logger) : IHostedService
{
    private Process? _process;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var dir = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "frontend"));

        if (!Directory.Exists(dir))
        {
            logger.LogInformation("Frontend folder not found at {Dir}; skipping.", dir);
            return Task.CompletedTask;
        }
        if (!Directory.Exists(Path.Combine(dir, "node_modules")))
        {
            logger.LogWarning("Frontend packages are missing. Run 'npm install' once inside {Dir}.", dir);
            return Task.CompletedTask;
        }
        if (IsPortInUse(5173))
        {
            logger.LogInformation("Port 5173 is already in use; assuming the frontend is already running.");
            return Task.CompletedTask;
        }

        try
        {
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npm",
                Arguments = OperatingSystem.IsWindows() ? "/c npm run dev" : "run dev",
                WorkingDirectory = dir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (_process is not null)
            {
                _process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) logger.LogInformation("[frontend] {Line}", e.Data); };
                _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) logger.LogWarning("[frontend] {Line}", e.Data); };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not start the frontend dev server.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        }
        return Task.CompletedTask;
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", port);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
