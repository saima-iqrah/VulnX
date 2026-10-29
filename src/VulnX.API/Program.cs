using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VulnX.API.Hosting;
using VulnX.API.Hubs;
using VulnX.API.Middleware;
using VulnX.Application.Abstractions;
using VulnX.Domain.Common;
using VulnX.Infrastructure;
using VulnX.Infrastructure.Auth;
using VulnX.Infrastructure.Persistence;
using VulnX.Infrastructure.Scans;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs outside development (easy to search in any log platform).
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "O";
    });
}

builder.Services.AddInfrastructure(builder.Configuration);

// Development only: F5 also starts the Vite dev server (launchSettings opens http://localhost:5173).
if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<DevFrontendLauncher>();

// ---- JWT authentication ----
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it via user-secrets or the Jwt__Key environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };

        // Browsers cannot send an Authorization header over WebSockets, so SignalR sends the token in the
        // query string. It is only accepted for the /hubs path.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
});

// ---- Rate limiting ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // every /api request: 300 / minute / IP (static files and the SignalR hub are not limited here)
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        if (!http.Request.Path.StartsWithSegments("/api"))
            return RateLimitPartition.GetNoLimiter("static");
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });

    // auth endpoints: 10 requests / minute / IP
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // public report verification: 20 / minute / IP
    o.AddPolicy("verify", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // AI assistant: 10 / minute / user
    o.AddPolicy("ai", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst("sub")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // starting scans: 5 / minute / user
    o.AddPolicy("scans", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst("sub")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IScanProgressNotifier, SignalRScanNotifier>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // TLS is terminated by the reverse proxy (see DEPLOY.md); the built React app is served from wwwroot.
    app.UseHsts();
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors("Frontend");
app.UseAuthentication();     // before the rate limiter so the per-user policies can see the user
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ScanHub>("/hubs/scans");
app.MapHealthChecks("/api/health");
app.MapMethods("/api/{**rest}", new[] { "GET", "POST", "PUT", "DELETE" }, () => Results.NotFound());
if (!app.Environment.IsDevelopment())
    app.MapFallbackToFile("index.html");   // React routes (/login, /app/...) are handled by the browser

// Apply migrations automatically in development, or in production when Database:MigrateOnStart=true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStart"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrationDb.Database.MigrateAsync();
}

// Roles + optional admin (Seed:Enabled) + scanner vulnerability definitions.
await DbSeeder.SeedAsync(app.Services, builder.Configuration);
await CatalogSeeder.SeedAsync(app.Services);

app.Run();

// Needed by WebApplicationFactory in integration tests.
public partial class Program { }