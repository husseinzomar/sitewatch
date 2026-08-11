namespace SiteWatch.Core.Entities;

public enum CheckType
{
    PageLoad,
    CheckoutFlow,
    // Appended, never inserted: this enum is stored as int in Postgres
    // (.HasConversion<int>()), so reordering would silently corrupt the
    // meaning of existing PageLoad/CheckoutFlow rows.
    AdminDashboardCheck
}

public class Check
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public CheckType Type { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Site Site { get; set; } = null!;
    public ICollection<CheckResult> CheckResults { get; set; } = new List<CheckResult>();
}
