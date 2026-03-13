using System.ServiceProcess;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParentalControl.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServiceName = "ParentalControl.Service";

    private readonly DashboardViewModel _dashboardVm;
    private readonly DispatcherTimer _serviceTimer;

    [ObservableProperty]
    private ObservableObject _currentView = null!;

    [ObservableProperty]
    private string _serviceStatusText = "Service: ...";

    [ObservableProperty]
    private SolidColorBrush _serviceStatusColor = Brushes.Gray;

    [ObservableProperty]
    private bool _isServiceRunning;

    public MainViewModel()
    {
        _dashboardVm = new DashboardViewModel(NavigateToUserDetail);
        CurrentView = _dashboardVm;
        _dashboardVm.LoadAll();

        UpdateServiceStatus();
        _serviceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _serviceTimer.Tick += (_, _) => UpdateServiceStatus();
        _serviceTimer.Start();
    }

    public void StopServicePolling() => _serviceTimer.Stop();

    private void UpdateServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
            {
                ServiceStatusText = "Service: Up";
                ServiceStatusColor = Brushes.Green;
                IsServiceRunning = true;
            }
            else
            {
                ServiceStatusText = "Service: Down";
                ServiceStatusColor = Brushes.Red;
                IsServiceRunning = false;
            }
        }
        catch (InvalidOperationException)
        {
            ServiceStatusText = "Service: Down";
            ServiceStatusColor = Brushes.Red;
            IsServiceRunning = false;
        }
    }

    private void NavigateToUserDetail(UserRow user)
    {
        var detailVm = new UserDetailViewModel(user, _dashboardVm.SidToUsername, NavigateBack);
        CurrentView = detailVm;
        detailVm.LoadAll();
    }

    private void NavigateBack()
    {
        _dashboardVm.LoadAll();
        CurrentView = _dashboardVm;
    }
}
