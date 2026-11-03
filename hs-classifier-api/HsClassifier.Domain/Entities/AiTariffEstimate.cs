namespace HsClassifier.Domain.Entities;

/// <summary>
/// What the AI answered for one HS code about PPN, PPh 22 and FTA scheme names. Kept until an Admin clears it,
/// so the same code never costs a second AI request. It is never an official value: the tariff lookup only
/// shows it for fields the database has no rate for, and always labels it as an estimate.
/// </summary>
public class AiTariffEstimate
{
    public string Code { get; set; } = string.Empty;
    public decimal? PpnPercent { get; set; }
    public string? PpnBasis { get; set; }
    public decimal? Pph22Percent { get; set; }
    public string? Pph22Basis { get; set; }
    /// <summary>JSON array of scheme names, for example ["ATIGA","ACFTA"]. Names only, no rates.</summary>
    public string FtaSchemes { get; set; } = "[]";
    public string Model { get; set; } = string.Empty;
    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
