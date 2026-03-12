namespace ParentalControl.Core.Models;

public sealed class EventRecord
{
    public DateTime Timestamp { get; set; }
    public string UserSid { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public string Details { get; set; } = string.Empty;
}
