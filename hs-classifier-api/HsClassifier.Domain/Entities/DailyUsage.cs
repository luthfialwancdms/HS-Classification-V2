namespace HsClassifier.Domain.Entities;

/// <summary>
/// Classifications a user started on one calendar day (day boundary follows a fixed UTC offset, see
/// <c>DailyQuotaService</c>). Composite key (UserId, Day): a second insert for the same day fails instead of
/// double counting. Count is a concurrency token so two racing requests cannot both pass the limit.
/// </summary>
public class DailyUsage
{
    public int UserId { get; set; }
    public DateOnly Day { get; set; }
    public int Count { get; set; }
}
