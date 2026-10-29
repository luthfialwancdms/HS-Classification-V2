using System.Globalization;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Seeding;

/// <summary>
/// Seeds BtkiTariffLines from Hscode.csv (semicolon-delimited).
/// Run once after InitialCreate migration.
/// ponytail: single-pass bulk insert, no ORM tracking for speed.
/// </summary>
public class BtkiCsvSeeder(HsClassifierDbContext db, ILogger<BtkiCsvSeeder> logger)
{
    public async Task SeedAsync(string csvPath, CancellationToken ct = default)
    {
        if (await db.NomenclatureVersions.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding BTKI data from {Path}...", csvPath);

        // Create version record
        var version = new NomenclatureVersion
        {
            Name = "BTKI 2022",
            BtkiEdition = "PMK 26/2022",
            RegulationNumber = "PMK 26/2022",
            HsEdition = "2022",
            EffectiveFrom = new DateTime(2022, 4, 1),
            State = ImportState.Active,
            IsActive = true,
            CreatedBy = "seed",
        };
        db.NomenclatureVersions.Add(version);
        await db.SaveChangesAsync(ct);

        var lines = new List<BtkiTariffLine>();
        int row = 0, skipped = 0;

        using var reader = new StreamReader(csvPath, System.Text.Encoding.UTF8);
        string? header = await reader.ReadLineAsync(ct); // skip header

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            row++;
            var cols = ParseCsvLine(line);
            if (cols.Length < 11) { skipped++; continue; }

            var rawCode = cols[1].Trim();
            if (rawCode.Length != 8 || !rawCode.All(char.IsDigit)) { skipped++; continue; }

            var status = cols[10].Trim();
            var code = $"{rawCode[..4]}.{rawCode[4..6]}.{rawCode[6..]}";
            var code4 = cols[4].Trim();
            var code6 = cols[5].Trim();

            string? parentCode = null;
            if (code6.Length == 6)
                parentCode = $"{code6[..4]}.{code6[4..]}";
            else if (code4.Length == 4)
                parentCode = code4;

            lines.Add(new BtkiTariffLine
            {
                VersionId = version.Id,
                Code = code,
                ParentCode = parentCode,
                ChapterCode = rawCode[..2],
                HeadingCode = rawCode[..4],
                SubheadingCode = code6.Length == 6 ? $"{code6[..4]}.{code6[4..]}" : null,
                IndentLevel = code6.Length == 6 ? 3 : 2,
                DescriptionId = cols[2].Trim().Trim('"'),
                DescriptionEn = cols[3].Trim().Trim('"'),
                IngestionMethod = IngestionMethod.CsvTemplate,
                SourceId = $"csv:Hscode.csv:row_{row}",
                IsActive = status == "A",
            });

            // Batch insert every 1000 rows
            if (lines.Count >= 1000)
            {
                await db.BtkiTariffLines.AddRangeAsync(lines, ct);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Inserted {Count} rows...", row);
                lines.Clear();
            }
        }

        if (lines.Count > 0)
        {
            await db.BtkiTariffLines.AddRangeAsync(lines, ct);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Seeding complete. Total: {Total}, Skipped: {Skipped}", row, skipped);
    }

    /// <summary>
    /// Adds the 4-digit heading and 6-digit subheading rows (official text from the
    /// IdDescription4Digit / IdDescription6Digit columns of Hscode.csv) to the active version.
    /// Without them KUM HS 1 had no heading text to compare against.
    /// Idempotent: only codes not already present are inserted, so it also backfills existing DBs.
    /// </summary>
    public async Task SeedHierarchyAsync(string csvPath, CancellationToken ct = default)
    {
        var version = await db.NomenclatureVersions.FirstOrDefaultAsync(v => v.IsActive, ct);
        if (version is null) return;

        // Rebuild rows this method generated earlier (derivation rules may have improved);
        // hierarchy rows from DOCX/template imports are left untouched.
        await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.Code.Length < 10
                && (t.SourceId!.StartsWith("csv:Hscode.csv:") || t.SourceId.StartsWith("csv:hs_headings_seed.csv:")))
            .ExecuteDeleteAsync(ct);
        var existing = (await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.Code.Length < 10)
            .Select(t => t.Code).ToListAsync(ct)).ToHashSet();

        // code → (id, en, active, row); first row with text wins, active if any child is active
        var nodes = new Dictionary<string, (string Id, string? En, bool Active, string Source)>();
        static string Clean(string s) => s.Trim().Trim('"').Trim();

        using var reader = new StreamReader(csvPath, System.Text.Encoding.UTF8);
        await reader.ReadLineAsync(ct); // header
        int row = 0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            row++;
            var cols = ParseCsvLine(line);
            if (cols.Length < 11) continue;
            var active = cols[10].Trim() == "A";

            void Add(string code, string id, string en)
            {
                if (id.Length == 0) return;
                if (nodes.TryGetValue(code, out var n)) { if (active && !n.Active) nodes[code] = n with { Active = true }; }
                else nodes[code] = (id, en.Length == 0 ? null : en, active,
                    $"csv:Hscode.csv:row_{row}:{code.Length switch { 4 => "heading", 6 => "group", _ => "subheading" }}");
            }

            var c4 = cols[4].Trim();
            var text4 = Clean(cols[6]);
            if (c4.Length == 4 && c4.All(char.IsDigit)) Add(c4, text4, Clean(cols[7]));

            // Parent text of the line: a 6-digit subheading ("8471.30") or a one-dash group
            // ("8429.5" = "- Sekop mekanik, ekskavator dan shovel loader :"). The CSV gives the
            // group as a 5-digit code, or leaves it empty and only provides the "- ... :" header text.
            var c6 = cols[5].Trim();
            var text6 = Clean(cols[8]);
            var rawCode = cols[1].Trim();
            string? parentCode =
                c6.Length == 6 && c6.All(char.IsDigit) ? $"{c6[..4]}.{c6[4..]}" :
                c6.Length == 5 && c6.All(char.IsDigit) ? $"{c6[..4]}.{c6[4]}" :
                c6.Length == 0 && text6.EndsWith(':') && rawCode.Length == 8 ? $"{rawCode[..4]}.{rawCode[4]}" :
                null;
            // Some rows repeat the heading text (sometimes in English) in the 6-digit column — not a subheading
            var en4 = Clean(cols[7]);
            if (parentCode is not null && text6 != text4 && text6 != en4) Add(parentCode, text6, Clean(cols[9]));
        }

        // One-dash group texts (5-digit, e.g. 84713 "- Mesin pengolah data otomatis digital portabel ... :")
        // missing from Hscode.csv are taken from data/hs_headings_seed.csv. Only 5-digit rows are used:
        // its 6-digit rows include hand-written paraphrases, not official text.
        var groupSeed = Path.Combine(Path.GetDirectoryName(csvPath)!, "data", "hs_headings_seed.csv");
        if (File.Exists(groupSeed))
        {
            using var gr = new StreamReader(groupSeed, System.Text.Encoding.UTF8);
            await gr.ReadLineAsync(ct);
            int grow = 0;
            while (await gr.ReadLineAsync(ct) is { } gline)
            {
                grow++;
                var g = ParseCsvLine(gline, ',');
                if (g.Length < 4 || g[1].Trim().Length != 5 || !g[1].Trim().All(char.IsDigit)) continue;
                var code = $"{g[1].Trim()[..4]}.{g[1].Trim()[4]}";
                if (!nodes.ContainsKey(code) && Clean(g[2]).Length > 0)
                    nodes[code] = (Clean(g[2]), Clean(g[3]) is { Length: > 0 } en ? en : null, true,
                        $"csv:hs_headings_seed.csv:row_{grow + 1}:group");
            }
        }

        var toAdd = nodes.Where(n => !existing.Contains(n.Key)).Select(n =>
        {
            var heading = n.Key[..4];
            var level = n.Key.Length switch { 4 => "heading", 6 => "group", _ => "subheading" };
            return new BtkiTariffLine
            {
                VersionId = version.Id,
                Code = n.Key,
                ParentCode = level == "heading" ? null : heading,
                ChapterCode = heading[..2],
                HeadingCode = heading,
                SubheadingCode = level == "subheading" ? n.Key : null,
                IndentLevel = level == "heading" ? 0 : 1,
                DescriptionId = n.Value.Id,
                DescriptionEn = n.Value.En,
                IngestionMethod = IngestionMethod.CsvTemplate,
                SourceId = n.Value.Source,
                IsActive = n.Value.Active,
            };
        }).ToList();

        if (toAdd.Count == 0) return;
        foreach (var batch in toAdd.Chunk(1000))
        {
            await db.BtkiTariffLines.AddRangeAsync(batch, ct);
            await db.SaveChangesAsync(ct);
        }
        logger.LogInformation("Hierarchy seeded: {Headings} headings, {Groups} one-dash groups, {Subheadings} subheadings.",
            toAdd.Count(t => t.Code.Length == 4), toAdd.Count(t => t.Code.Length == 6), toAdd.Count(t => t.Code.Length == 7));
    }

    private static readonly System.Text.RegularExpressions.Regex MfnPercent =
        new(@"^\s*(\d{1,3})(?:[.,](\d{1,2}))?\s*%\s*$");

    /// <summary>
    /// "5,00%" → 5.00. Returns null for anything that is not a clean 0–100 percentage
    /// (OCR noise like "ii,00%", specific rates, missing %), so no rate is ever invented.
    /// </summary>
    public static decimal? ParseMfnPercent(string? raw)
    {
        var m = MfnPercent.Match(raw ?? "");
        if (!m.Success) return null;
        var value = decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                    + (m.Groups[2].Success ? decimal.Parse("0." + m.Groups[2].Value, CultureInfo.InvariantCulture) : 0);
        return value <= 100 ? value : null;
    }

    /// <summary>
    /// MFN (bea masuk) from data/btki_2022_full.csv (column bea_masuk_mfn) for every 8-digit tariff
    /// line of the active version. Rebuilds the rows it created earlier; unparseable values are skipped
    /// and reported, so the UI shows "tidak tersedia" instead of a wrong rate.
    /// </summary>
    public async Task SeedMfnRatesAsync(string btkiFullCsv, CancellationToken ct = default)
    {
        var version = await db.NomenclatureVersions.FirstOrDefaultAsync(v => v.IsActive, ct);
        if (version is null || !File.Exists(btkiFullCsv)) return;

        const string sourcePrefix = "csv:btki_2022_full.csv:";
        await db.MfnRates.Where(r => r.VersionId == version.Id && r.SourceId!.StartsWith(sourcePrefix))
            .ExecuteDeleteAsync(ct);

        var lineIds = (await db.BtkiTariffLines
                .Where(t => t.VersionId == version.Id && t.Code.Length == 10)
                .Select(t => new { t.Code, t.Id }).ToListAsync(ct))
            .DistinctBy(t => t.Code).ToDictionary(t => t.Code, t => t.Id);
        var already = (await db.MfnRates.Where(r => r.VersionId == version.Id)
            .Select(r => r.TariffLineId).ToListAsync(ct)).ToHashSet();

        var rates = new List<MfnRate>();
        int row = 0, unparsed = 0, notInDb = 0;
        using var reader = new StreamReader(btkiFullCsv, System.Text.Encoding.UTF8);
        var header = ParseCsvLine(await reader.ReadLineAsync(ct) ?? "", ',').Select(h => h.Trim().Trim('"')).ToList();
        int codeCol = header.IndexOf("formatted_code"), mfnCol = header.IndexOf("bea_masuk_mfn");
        if (codeCol < 0 || mfnCol < 0) { logger.LogWarning("MFN source has no formatted_code/bea_masuk_mfn column."); return; }

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            row++;
            var cols = ParseCsvLine(line, ',');
            if (cols.Length <= Math.Max(codeCol, mfnCol)) continue;
            var code = cols[codeCol].Trim().Trim('"');
            if (code.Length != 10) continue;                       // only 8-digit tariff lines carry duty
            if (!lineIds.TryGetValue(code, out var lineId)) { notInDb++; continue; }
            if (already.Contains(lineId)) continue;                 // keep rates from DOCX/template imports
            var value = ParseMfnPercent(cols[mfnCol].Trim().Trim('"'));
            if (value is null) { unparsed++; continue; }

            already.Add(lineId);
            rates.Add(new MfnRate
            {
                TariffLineId = lineId, VersionId = version.Id, RateValue = value.Value,
                RateUnit = RateUnit.Percent, EffectiveFrom = version.EffectiveFrom,
                SourceId = $"{sourcePrefix}row_{row + 1}:mfn", IsActive = true,
            });
        }

        foreach (var chunk in rates.Chunk(1000))
        {
            await db.MfnRates.AddRangeAsync(chunk, ct);
            await db.SaveChangesAsync(ct);
        }
        logger.LogInformation("MFN seeded: {Inserted} rates; skipped {Unparsed} unparseable, {NotInDb} codes not in active version.",
            rates.Count, unparsed, notInDb);
    }

    /// <summary>
    /// Creates the first Admin when no Admin exists. Credentials come from config
    /// (Seed:AdminEmail / Seed:AdminPassword, e.g. env vars or user-secrets) — never hardcoded.
    /// </summary>
    public async Task EnsureAdminAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (await db.AppUsers.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive, ct)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            logger.LogWarning("No Admin user exists. Set Seed:AdminEmail and Seed:AdminPassword (min 8 chars) to create one.");
            return;
        }

        var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
        {
            existing.Role = UserRole.Admin; existing.IsActive = true;
            existing.PasswordHash = Services.PasswordHashing.Hash(password);
        }
        else
        {
            db.AppUsers.Add(new AppUser
            {
                Email = email, PasswordHash = Services.PasswordHashing.Hash(password),
                Role = UserRole.Admin, IsActive = true,
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin user {Email} provisioned.", email);
    }

    private static string[] ParseCsvLine(string line, char delimiter = ';')
    {
        // Simple delimiter split, handles quoted fields containing the delimiter
        var result = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == '"') { inQuote = !inQuote; current.Append(c); }
            else if (c == delimiter && !inQuote) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
