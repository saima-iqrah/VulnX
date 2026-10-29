using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Platform;
using VulnX.Domain.Entities;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Platform;

internal sealed class SystemSettings(AppDbContext db, IMemoryCache cache, IAuditLogger audit) : ISystemSettings
{
    private static string CacheKey(string key) => "setting:" + key;

    private async Task<string> GetRawAsync(string key, string fallback, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey(key), out string? hit) && hit is not null) return hit;
        var row = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        var value = row?.Value ?? fallback;
        cache.Set(CacheKey(key), value, TimeSpan.FromSeconds(30));
        return value;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        int.TryParse(await GetRawAsync(key, fallback.ToString(), ct), out var v) ? v : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) =>
        bool.TryParse(await GetRawAsync(key, fallback.ToString().ToLowerInvariant(), ct), out var v) ? v : fallback;

    public async Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return SettingDefinitions.All
            .Select(kv => new SettingDto(kv.Key, rows.TryGetValue(kv.Key, out var v) ? v : kv.Value.Default, kv.Value.Default, kv.Value.Description))
            .ToList();
    }

    public async Task UpdateAsync(Guid adminId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        foreach (var (key, value) in values)
        {
            var error = SettingDefinitions.Validate(key, value.Trim());
            if (error is not null) throw new AppException(400, error);
        }

        var existing = await db.SystemSettings.ToDictionaryAsync(s => s.Key, ct);
        foreach (var (key, raw) in values)
        {
            var value = raw.Trim().ToLowerInvariant();
            if (!existing.TryGetValue(key, out var row))
            {
                row = new SystemSetting { Key = key };
                db.SystemSettings.Add(row);
            }
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = adminId;
            cache.Remove(CacheKey(key));
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(adminId, "admin.settings_updated", "SystemSetting", null, new { keys = values.Keys.ToArray() }, ct);
    }
}