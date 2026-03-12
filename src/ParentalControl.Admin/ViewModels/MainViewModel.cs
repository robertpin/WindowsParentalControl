using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Admin.Services;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Dictionary<string, string> _sidToUsername = new();

    [ObservableProperty]
    private ObservableCollection<UserRow> _users = [];

    [ObservableProperty]
    private UserRow? _selectedUser;

    [ObservableProperty]
    private int _dailyMinutes = 120;

    [ObservableProperty]
    private string _scheduleStart = "08:00";

    [ObservableProperty]
    private string _scheduleEnd = "22:00";

    [ObservableProperty]
    private ObservableCollection<UsageRecord> _usageRecords = [];

    [ObservableProperty]
    private ObservableCollection<EventRecord> _eventRecords = [];

    [ObservableProperty]
    private DateOnly _usageFromDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-7));

    [ObservableProperty]
    private DateOnly _usageToDate = DateOnly.FromDateTime(DateTime.Now);

    public MainViewModel()
    {
        LoadUsers();
        RefreshEvents();
    }

    partial void OnSelectedUserChanged(UserRow? value)
    {
        if (value is null) return;
        if (value.IsAdmin)
        {
            SelectedUser = null;
            return;
        }
        LoadUserLimit();
        RefreshUsage();
    }

    [RelayCommand]
    private void LoadUsers()
    {
        try
        {
            var systemUsers = UserDiscovery.GetAllLocalUsers();

            // Upsert only non-admin users to the database
            foreach (var (sid, username, isAdmin) in systemUsers)
            {
                if (isAdmin) continue;

                var existing = UserRepository.GetBySid(sid);
                if (existing is null)
                {
                    UserRepository.Upsert(sid, username, false);
                }
                else
                {
                    UserRepository.Upsert(sid, username, existing.IsRestricted);
                }
            }

            // Build the grid: standard users from DB + admin users from discovery
            var dbUsers = UserRepository.GetAll();
            var rows = new List<UserRow>();
            rows.AddRange(dbUsers.Select(UserRow.FromDatabase));

            foreach (var (sid, username, isAdmin) in systemUsers)
            {
                if (isAdmin)
                {
                    rows.Add(UserRow.ForAdmin(sid, username));
                }
            }

            Users = new ObservableCollection<UserRow>(rows);

            // Build SID→Username lookup for event display
            _sidToUsername = Users.ToDictionary(u => u.Sid, u => u.Username);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ToggleRestricted()
    {
        if (SelectedUser is null) return;
        if (SelectedUser.IsAdmin) return;

        SelectedUser.IsRestricted = !SelectedUser.IsRestricted;
        UserRepository.SetRestricted(SelectedUser.Id, SelectedUser.IsRestricted);

        if (SelectedUser.IsRestricted)
        {
            var existingLimit = LimitRepository.GetByUserId(SelectedUser.Id);
            if (existingLimit is null)
            {
                LimitRepository.Upsert(new LimitConfig
                {
                    UserId = SelectedUser.Id,
                    DailyMinutes = 120,
                    ScheduleStart = new TimeOnly(8, 0),
                    ScheduleEnd = new TimeOnly(22, 0)
                });
            }
            SelectedUser.RefreshLimits();
            LoadUserLimit();
        }
    }

    [RelayCommand]
    private void SaveLimits()
    {
        if (SelectedUser is null) return;

        if (!TimeOnly.TryParse(ScheduleStart, out var start) ||
            !TimeOnly.TryParse(ScheduleEnd, out var end))
        {
            MessageBox.Show("Invalid time format. Use HH:mm (e.g., 08:00).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DailyMinutes < 1)
        {
            MessageBox.Show("Daily minutes must be at least 1.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LimitRepository.Upsert(new LimitConfig
        {
            UserId = SelectedUser.Id,
            DailyMinutes = DailyMinutes,
            ScheduleStart = start,
            ScheduleEnd = end
        });

        SelectedUser.RefreshLimits();

        MessageBox.Show("Limits saved successfully.", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RefreshUsage()
    {
        if (SelectedUser is null)
        {
            UsageRecords = [];
            return;
        }

        var records = UsageRepository.GetUsageRange(SelectedUser.Id, UsageFromDate, UsageToDate);
        UsageRecords = new ObservableCollection<UsageRecord>(records);
    }

    [RelayCommand]
    private void RefreshEvents()
    {
        var events = EventRepository.GetEvents();
        foreach (var evt in events)
        {
            evt.Username = _sidToUsername.TryGetValue(evt.UserSid, out var name) ? name : evt.UserSid;
        }
        EventRecords = new ObservableCollection<EventRecord>(events);
    }

    private void LoadUserLimit()
    {
        if (SelectedUser is null) return;

        if (SelectedUser.DailyMinutes.HasValue)
        {
            DailyMinutes = SelectedUser.DailyMinutes.Value;
            ScheduleStart = SelectedUser.ScheduleStart ?? "08:00";
            ScheduleEnd = SelectedUser.ScheduleEnd ?? "22:00";
        }
        else
        {
            DailyMinutes = 120;
            ScheduleStart = "08:00";
            ScheduleEnd = "22:00";
        }
    }
}
