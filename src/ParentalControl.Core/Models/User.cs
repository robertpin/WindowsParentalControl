namespace ParentalControl.Core.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Sid { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsRestricted { get; set; }
}
