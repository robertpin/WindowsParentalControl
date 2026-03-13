using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class UserDetailViewModel : ObservableObject
{
    private readonly Dictionary<string, string> _sidToUsername;
    private readonly Action _navigateBack;

    public UserRow User { get; }

    [ObservableProperty]
    private int _dailyMinutes = 120;

    [ObservableProperty]
    private string _scheduleStart = "08:00";

    [ObservableProperty]
    private string _scheduleEnd = "22:00";

    [ObservableProperty]
    private int _editableUsageMinutes;

    [ObservableProperty]
    private ObservableCollection<EventRecord> _eventRecords = [];

    public UserDetailViewModel(UserRow user, Dictionary<string, string> sidToUsername, Action navigateBack)
    {
        User = user;
        _sidToUsername = sidToUsername;
        _navigateBack = navigateBack;
    }

    public void LoadAll()
    {
        RefreshUser();
        LoadLimitFields();
        RefreshEvents();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateBack();
    }

    [RelayCommand]
    private void RefreshUser()
    {
        User.RefreshUsage();
        User.RefreshLimits();
        LoadLimitFields();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void RefreshEvents()
    {
        var events = EventRepository.GetEvents(userSid: User.Sid);
        foreach (var evt in events)
        {
            evt.Username = _sidToUsername.TryGetValue(evt.UserSid, out var name) ? name : evt.UserSid;
        }
        EventRecords = new ObservableCollection<EventRecord>(events);
    }

    [RelayCommand]
    private void SetUsage()
    {
        if (EditableUsageMinutes < 0)
        {
            MessageBox.Show("Usage minutes cannot be negative.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        UsageRepository.SetUsage(User.Id, today, EditableUsageMinutes);
        User.RefreshUsage();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void SetLimits()
    {
        if (!TimeOnly.TryParse(ScheduleStart, out var start) ||
            !TimeOnly.TryParse(ScheduleEnd, out var end))
        {
            MessageBox.Show("Invalid time format. Use HH:mm (e.g., 08:00).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (start >= end)
        {
            MessageBox.Show("Schedule start must be before schedule end.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DailyMinutes < 1 || DailyMinutes > 1440)
        {
            MessageBox.Show("Daily minutes must be between 1 and 1440 (24 hours).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LimitRepository.Upsert(new LimitConfig
        {
            UserId = User.Id,
            DailyMinutes = DailyMinutes,
            ScheduleStart = start,
            ScheduleEnd = end
        });
        UserRepository.SetRestricted(User.Id, true);
        User.IsRestricted = true;
        User.RefreshLimits();

        MessageBox.Show("Limits saved successfully.", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RemoveLimits()
    {
        var result = MessageBox.Show(
            $"Remove all limits for {User.Username}? This will allow unrestricted access.",
            "Confirm Remove Limits", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        LimitRepository.Delete(User.Id);
        UserRepository.SetRestricted(User.Id, false);
        User.IsRestricted = false;
        User.RefreshLimits();

        DailyMinutes = 120;
        ScheduleStart = "08:00";
        ScheduleEnd = "22:00";
    }

    private void LoadLimitFields()
    {
        if (User.DailyMinutes.HasValue)
        {
            DailyMinutes = User.DailyMinutes.Value;
            ScheduleStart = User.ScheduleStart ?? "08:00";
            ScheduleEnd = User.ScheduleEnd ?? "22:00";
        }
        else
        {
            DailyMinutes = 120;
            ScheduleStart = "08:00";
            ScheduleEnd = "22:00";
        }
    }
}
