namespace TheCafePOS_WPF.Models;

public class Customer
{
    public string Phone { get; set; } = "";
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public decimal TotalSpent { get; set; }
    public int VisitCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastVisit { get; set; }
    // Tiers by lifetime spend, similar to common cafe loyalty programs.
    public string Tier => TotalSpent >= 5_000_000 ? "Kim cương" : TotalSpent >= 2_000_000 ? "Vàng" : TotalSpent >= 500_000 ? "Bạc" : "Mới";
}

// Automatic time-window discount ("giờ vàng"); applied without manager approval.
public class Promotion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Giờ vàng";
    public bool IsActive { get; set; }
    public int Percent { get; set; } = 10;
    public TimeSpan Start { get; set; } = new(14, 0, 0);
    public TimeSpan End { get; set; } = new(16, 0, 0);
    // Empty = every day.
    public List<DayOfWeek> Days { get; set; } = new();
    public bool AppliesAt(DateTime at) => IsActive && Percent is > 0 and < 100 && at.TimeOfDay >= Start && at.TimeOfDay < End && (Days.Count == 0 || Days.Contains(at.DayOfWeek));
}
