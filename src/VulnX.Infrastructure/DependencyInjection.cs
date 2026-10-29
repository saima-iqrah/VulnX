using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VulnX.Application.Abstractions;
using VulnX.Infrastructure.Ai;
using VulnX.Infrastructure.Auth;
using VulnX.Infrastructure.Identity;
using VulnX.Infrastructure.Persistence;
using VulnX.Infrastructure.Platform;
using VulnX.Infrastructure.Scans;
using VulnX.Infrastructure.Services;
using VulnX.Scanner;
using Microsoft.Extensions.Options;

namespace VulnX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it via user-secrets or the ConnectionStrings__Default environment variable.");

        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)));

        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 10;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.AllowedForNewUsers = true;
        })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddMemoryCache();
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        if (!string.IsNullOrWhiteSpace(config["Smtp:Host"]))
            services.AddSingleton<IAppEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IAppEmailSender, LoggingEmailSender>();

        // Scanner engine + background scan jobs
        services.AddScanner();
        services.AddSingleton<IScanQueue, ScanQueue>();
        services.AddScoped<IScanService, ScanService>();
        services.AddHostedService<ScanWorker>();

        // AI assistant (explanations, summary, chat)
        services.Configure<AiOptions>(config.GetSection(AiOptions.SectionName));
        services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<OpenAiCompatibleClient>(c => c.Timeout = TimeSpan.FromSeconds(180));
        services.AddHttpClient<GeminiClient>(c =>
        {
            c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            c.Timeout = TimeSpan.FromSeconds(180);
        });
        // Ai:Provider = "Gemini" or "OpenAI"; empty = Gemini when only Gemini:ApiKey is set, otherwise OpenAI.
        services.AddScoped<IAiClient>(sp =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            var useGemini = ai.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(ai.Provider) && string.IsNullOrWhiteSpace(ai.ApiKey) && !string.IsNullOrWhiteSpace(gemini.ApiKey));
            return useGemini ? sp.GetRequiredService<GeminiClient>() : sp.GetRequiredService<OpenAiCompatibleClient>();
        });
        services.AddScoped<IAiAssistant, AiAssistantService>();

        // Reports, notifications, admin console, settings, housekeeping
        services.AddScoped<ISystemSettings, SystemSettings>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdminService, AdminService>();
        //services.AddScoped<IDatabaseTools, DatabaseTools>();
        services.AddHostedService<MaintenanceWorker>();

        return services;
    }
}
