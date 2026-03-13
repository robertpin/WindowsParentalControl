namespace ParentalControl.Core.Models;

public enum EventType
{
    LOGIN,
    LOGOUT,
    SLEEP,
    WAKE,
    LIMIT_REACHED,
    FORCED_LOGOUT,
    LOGIN_DENIED,
    CLOCK_TAMPER
}
