using System.Windows;
using ParentalControl.Core.Data;

namespace ParentalControl.Admin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DatabaseManager.Initialize();
    }
}
