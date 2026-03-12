namespace ParentalControl.Core.Models;

public sealed class UsageRecord
{
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public int MinutesUsed { get; set; }
}
