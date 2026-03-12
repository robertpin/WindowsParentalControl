using CommunityToolkit.Mvvm.ComponentModel;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class UserRow : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _sid = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isRestricted;

    [ObservableProperty]
    private bool _isAdmin;

    public string UserType => IsAdmin ? "Administrator" : "Standard User";

    [ObservableProperty]
    private int? _dailyMinutes;

    [ObservableProperty]
    private string? _scheduleStart;

    [ObservableProperty]
    private string? _scheduleEnd;

    [ObservableProperty]
    private int _todayMinutesUsed;

    public static UserRow ForAdmin(string sid, string username)
    {
        return new UserRow
        {
            Id = -1,
            Sid = sid,
            Username = username,
            IsRestricted = false,
            IsAdmin = true
        };
    }

    public static UserRow FromDatabase(User user)
    {
        var row = new UserRow
        {
            Id = user.Id,
            Sid = user.Sid,
            Username = user.Username,
            IsRestricted = user.IsRestricted,
            IsAdmin = false
        };

        var limit = LimitRepository.GetByUserId(user.Id);
        if (limit is not null)
        {
            row.DailyMinutes = limit.DailyMinutes;
            row.ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            row.ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = UsageRepository.GetUsage(user.Id, today);
        if (usage is not null)
        {
            row.TodayMinutesUsed = usage.MinutesUsed;
        }

        return row;
    }

    public void RefreshLimits()
    {
        var limit = LimitRepository.GetByUserId(Id);
        if (limit is not null)
        {
            DailyMinutes = limit.DailyMinutes;
            ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }
        else
        {
            DailyMinutes = null;
            ScheduleStart = null;
            ScheduleEnd = null;
        }
    }
}
