using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Admin.Services;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly Action<UserRow> _navigateToDetail;

    [ObservableProperty]
    private ObservableCollection<UserRow> _standardUsers = [];

    [ObservableProperty]
    private ObservableCollection<UserRow> _adminUsers = [];

    [ObservableProperty]
    private ObservableCollection<EventRecord> _eventRecords = [];

    public Dictionary<string, string> SidToUsername { get; private set; } = new();

    public DashboardViewModel(Action<UserRow> navigateToDetail)
    {
        _navigateToDetail = navigateToDetail;
    }

    public void LoadAll()
    {
        RefreshUsers();
        RefreshEvents();
    }

    [RelayCommand]
    private void RefreshUsers()
    {
        try
        {
            var systemUsers = UserDiscovery.GetAllLocalUsers();

            foreach (var (sid, username, isAdmin) in systemUsers)
            {
                if (isAdmin) continue;
                var existing = UserRepository.GetBySid(sid);
                UserRepository.Upsert(sid, username, existing?.IsRestricted ?? false);
            }

            var dbUsers = UserRepository.GetAll();
            var standard = dbUsers.Select(UserRow.FromDatabase).ToList();
            var admins = systemUsers
                .Where(u => u.IsAdmin)
                .Select(u => UserRow.ForAdmin(u.Sid, u.Username))
                .ToList();

            StandardUsers = new ObservableCollection<UserRow>(standard);
            AdminUsers = new ObservableCollection<UserRow>(admins);

            SidToUsername = standard.Concat(admins).ToDictionary(u => u.Sid, u => u.Username);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RefreshEvents()
    {
        var events = EventRepository.GetEvents();
        foreach (var evt in events)
        {
            evt.Username = SidToUsername.TryGetValue(evt.UserSid, out var name) ? name : evt.UserSid;
        }
        EventRecords = new ObservableCollection<EventRecord>(events);
    }

    [RelayCommand]
    private void NavigateToUser(UserRow user)
    {
        _navigateToDetail(user);
    }
}
