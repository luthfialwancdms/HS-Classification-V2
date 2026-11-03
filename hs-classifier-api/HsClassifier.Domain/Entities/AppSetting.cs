namespace HsClassifier.Domain.Entities;

/// <summary>A small server-side switch an Admin can change at runtime (for example "ai.tariff_estimate").</summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    /// <summary>UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
