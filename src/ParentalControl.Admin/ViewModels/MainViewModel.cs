using CommunityToolkit.Mvvm.ComponentModel;

namespace ParentalControl.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardVm;

    [ObservableProperty]
    private ObservableObject _currentView = null!;

    public MainViewModel()
    {
        _dashboardVm = new DashboardViewModel(NavigateToUserDetail);
        CurrentView = _dashboardVm;
        _dashboardVm.LoadAll();
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
