using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VulnX.Domain.Entities;
using VulnX.Infrastructure.Persistence;
using VulnX.Scanner.Engine;

namespace VulnX.Infrastructure.Scans;

public static class CatalogSeeder
{
    /// Inserts or updates the vulnerability definitions from the scanner catalog (safe to run on every start).
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.VulnerabilityDefinitions.ToDictionaryAsync(d => d.Code, ct);

        foreach (var e in VulnerabilityCatalog.All)
        {
            if (!existing.TryGetValue(e.Code, out var d))
            {
                d = new VulnerabilityDefinition { Code = e.Code };
                db.VulnerabilityDefinitions.Add(d);
            }
            d.Name = e.Name;
            d.Category = e.Category;
            d.Description = e.Description;
            d.DefaultSeverity = e.Severity;
            d.References = e.References.ToList();
        }
        await db.SaveChangesAsync(ct);
    }
}