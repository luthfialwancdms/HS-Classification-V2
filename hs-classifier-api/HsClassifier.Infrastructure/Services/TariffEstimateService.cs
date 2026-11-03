using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Services;

public record AiRateEstimate(decimal Percent, string? Basis);

/// <summary>What the screen and the exports show when the database has no rate. Always an estimate.</summary>
public class AiTariffEstimateInfo
{
    public string Label { get; set; } = TariffEstimateService.Label;
    public AiRateEstimate? Ppn { get; set; }
    public AiRateEstimate? Pph22 { get; set; }
    /// <summary>Scheme names only. The AI is never asked for FTA rates.</summary>
    public List<string> FtaSchemes { get; set; } = new();
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Optional AI estimate of PPN, PPh 22 and FTA scheme names for a code the database has no rate for.
/// Off until an Admin turns it on. It only fills a field the database leaves empty, never replaces an official
/// value, and keeps the answer per code until an Admin clears it.
/// </summary>
public class TariffEstimateService(HsClassifierDbContext db, IServiceScopeFactory scopes, IAiProvider ai,
    IAiUsageRecorder recorder, TimeProvider clock, ILogger<TariffEstimateService> logger)
{
    public const string SettingKey = "ai.tariff_estimate";
    public const string Label = "Estimasi AI, belum terverifikasi";

    public const string System =
        "You are an Indonesian customs assistant. For the HS code and goods description given, estimate the " +
        "Indonesian import PPN and PPh Pasal 22 impor rates and name the free trade agreement schemes that may " +
        "apply to goods of this heading. Return ONLY a JSON object: " +
        "{\"ppn\": {\"percent\": number, \"basis\": \"short legal basis\"} or null, " +
        "\"pph22\": {\"percent\": number, \"basis\": \"short legal basis\"} or null, " +
        "\"fta_schemes\": [scheme abbreviations, at most 5]}. " +
        "List only schemes Indonesia has in force whose product coverage plausibly includes this heading; do not list " +
        "every scheme, and return an empty list when unsure. " +
        "percent is a plain number, for example 11. Never give FTA tariff rates. Use null when unsure.";

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        await db.AppSettings.AsNoTracking().AnyAsync(s => s.Key == SettingKey && s.Value == "on", ct);

    public async Task SetEnabledAsync(bool enabled, string? by, CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (row is null) db.AppSettings.Add(row = new AppSetting { Key = SettingKey });
        row.Value = enabled ? "on" : "off";
        row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        row.UpdatedBy = by;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> CachedCountAsync(CancellationToken ct = default) => db.AiTariffEstimates.CountAsync(ct);

    public async Task<int> ClearCacheAsync(CancellationToken ct = default)
    {
        var rows = await db.AiTariffEstimates.ToListAsync(ct);
        db.AiTariffEstimates.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    /// <summary>
    /// Null when the switch is off, nothing is missing, the AI is unavailable or its answer is unusable.
    /// Only the fields <paramref name="official"/> leaves empty can come back filled.
    /// </summary>
    public async Task<AiTariffEstimateInfo?> EstimateAsync(string code, string? description, TariffResult official,
        CancellationToken ct = default)
    {
        bool needPpn = official.Ppn is null, needPph = official.Pph22 is null, needFta = official.FtaRates.Count == 0;
        if (!(needPpn || needPph || needFta)) return null;
        try
        {
            if (!await IsEnabledAsync(ct)) return null;
            var cached = await db.AiTariffEstimates.AsNoTracking().FirstOrDefaultAsync(e => e.Code == code, ct)
                         ?? await AskAsync(code, description, ct);
            return cached is null ? null : Shape(cached, needPpn, needPph, needFta);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // An estimate is a bonus; whatever goes wrong here must not break the tariff lookup.
            logger.LogWarning("Tariff estimate failed: {Error}", ex.GetType().Name);
            return null;
        }
    }

    private AiTariffEstimateInfo? Shape(AiTariffEstimate e, bool ppn, bool pph, bool fta)
    {
        var info = new AiTariffEstimateInfo
        {
            Model = e.Model,
            Ppn = ppn && e.PpnPercent is { } a ? new(a, e.PpnBasis) : null,
            Pph22 = pph && e.Pph22Percent is { } b ? new(b, e.Pph22Basis) : null,
            FtaSchemes = fta ? JsonSerializer.Deserialize<List<string>>(e.FtaSchemes) ?? [] : [],
        };
        return info.Ppn is null && info.Pph22 is null && info.FtaSchemes.Count == 0 ? null : info;
    }

    private async Task<AiTariffEstimate?> AskAsync(string code, string? description, CancellationToken ct)
    {
        var user = $"hs_code: {code}" + (string.IsNullOrWhiteSpace(description) ? "" : $"\ndescription: {description.Trim()}");
        var sw = Stopwatch.StartNew();
        AiChatResult chat;
        try { chat = await ai.ChatJsonAsync(System, user, ct); }
        catch (OperationCanceledException) { await Record(null, false, sw, AiCall.Cancelled); throw; }
        // No key means no request was made; the status card already shows that.
        if (chat.Call.ErrorCode == AiCall.NoKey) return null;
        if (chat.Unavailable) { await Record(chat.Call, false, sw); return null; }
        var (parsed, unreadable) = Parse(chat.Content);
        if (unreadable || parsed is null) { await Record(chat.Call, false, sw, AiCall.Parse); return null; }
        await Record(chat.Call, true, sw);

        var row = new AiTariffEstimate
        {
            Code = code,
            PpnPercent = parsed.Ppn?.Percent, PpnBasis = parsed.Ppn?.Basis,
            Pph22Percent = parsed.Pph22?.Percent, Pph22Basis = parsed.Pph22?.Basis,
            FtaSchemes = JsonSerializer.Serialize(parsed.FtaSchemes),
            Model = ai.Model,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        await SaveCacheAsync(row);
        return row;
    }

    /// <summary>Own scope: the caller's DbContext may hold half-finished changes that must not be saved early.</summary>
    private async Task SaveCacheAsync(AiTariffEstimate row)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var other = scope.ServiceProvider.GetRequiredService<HsClassifierDbContext>();
            other.AiTariffEstimates.Add(row);
            await other.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException)
        {
            // Another request cached the same code first; its answer is as good as this one.
        }
    }

    private async Task Record(AiCallInfo? call, bool success, Stopwatch sw, string? errorCode = null)
    {
        try
        {
            sw.Stop();
            await recorder.RecordAsync(AiUsageRecorder.Build(AiCallKind.TariffEstimate, ai.ProviderName, ai.Model, call,
                success, (int)sw.ElapsedMilliseconds, clock.GetUtcNow().UtcDateTime, errorCode));
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI usage logging failed: {Error}", ex.GetType().Name);
        }
    }

    // ---- parsing -----------------------------------------------------------------------------------------

    public record Parsed(AiRateEstimate? Ppn, AiRateEstimate? Pph22, List<string> FtaSchemes);

    private static readonly Regex SchemeName = new(@"^[A-Za-z0-9][A-Za-z0-9 \-/().&]{1,39}$");
    private const decimal MaxPpn = 25m, MaxPph22 = 15m;

    /// <summary>
    /// Unreadable = no JSON object in the text (for example a proxy's quota notice returned with HTTP 200).
    /// Values outside a plausible range are dropped, not corrected. Scheme names with odd characters are dropped.
    /// </summary>
    public static (Parsed? Result, bool Unreadable) Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return (null, true);
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end < start) return (null, true);
        try
        {
            using var doc = JsonDocument.Parse(content[start..(end + 1)]);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, true);
            var schemes = root.TryGetProperty("fta_schemes", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim())
                    .Where(s => SchemeName.IsMatch(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList()
                : [];
            return (new Parsed(Rate(root, "ppn", MaxPpn), Rate(root, "pph22", MaxPph22), schemes), false);
        }
        catch (JsonException) { return (null, true); }
    }

    private static AiRateEstimate? Rate(JsonElement root, string name, decimal max)
    {
        if (!root.TryGetProperty(name, out var o) || o.ValueKind != JsonValueKind.Object
            || !o.TryGetProperty("percent", out var p)) return null;
        decimal v;
        if (p.ValueKind == JsonValueKind.Number) { if (!p.TryGetDecimal(out v)) return null; }
        else if (p.ValueKind != JsonValueKind.String
                 || !decimal.TryParse(p.GetString()!.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return null;
        if (v < 0 || v > max) return null;
        var basis = o.TryGetProperty("basis", out var b) && b.ValueKind == JsonValueKind.String
            ? Regex.Replace(b.GetString()!, @"\s+", " ").Trim() : null;
        if (basis is { Length: > 300 }) basis = basis[..300];
        return new AiRateEstimate(v, string.IsNullOrEmpty(basis) ? null : basis);
    }
}
