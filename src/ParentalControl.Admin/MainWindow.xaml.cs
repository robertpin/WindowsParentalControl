using System.Windows;
using ParentalControl.Admin.ViewModels;

namespace ParentalControl.Admin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.StopServicePolling();
    }
}
