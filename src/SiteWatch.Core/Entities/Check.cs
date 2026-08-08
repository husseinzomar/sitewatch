namespace SiteWatch.Core.Entities;

public enum CheckType
{
    PageLoad,
    CheckoutFlow
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
