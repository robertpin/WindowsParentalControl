using System.Windows.Controls;
using System.Windows.Input;
using ParentalControl.Admin.Helpers;

namespace ParentalControl.Admin.Views;

public partial class UserDetailView : UserControl
{
    public UserDetailView()
    {
        InitializeComponent();
    }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => DataGridScrollHelper.HandlePreviewMouseWheel(sender, e);
}
