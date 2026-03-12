namespace ParentalControl.Core.Models;

public sealed class LimitConfig
{
    public int UserId { get; set; }
    public int DailyMinutes { get; set; }
    public TimeOnly ScheduleStart { get; set; }
    public TimeOnly ScheduleEnd { get; set; }
}
