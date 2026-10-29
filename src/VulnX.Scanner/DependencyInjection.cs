using Microsoft.Extensions.DependencyInjection;
using VulnX.Application.Abstractions;
using VulnX.Scanner.Engine;
using VulnX.Scanner.Modules;
using VulnX.Scanner.Security;

namespace VulnX.Scanner;

public static class DependencyInjection
{
    public static IServiceCollection AddScanner(this IServiceCollection services)
    {
        services.AddSingleton<ITargetValidator, TargetValidator>();
        services.AddSingleton<IScanModule, SecurityHeadersModule>();
        services.AddSingleton<IScanModule, TlsModule>();
        services.AddSingleton<IScanModule, CookieModule>();
        services.AddSingleton<IScanModule, CorsModule>();
        services.AddSingleton<IScanModule, InfoDisclosureModule>();
        services.AddSingleton<IScanModule, HtmlContentModule>();
        services.AddSingleton<IScanEngine, ScanEngine>();
        return services;
    }
}