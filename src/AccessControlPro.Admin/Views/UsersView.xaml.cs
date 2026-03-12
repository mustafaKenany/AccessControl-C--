using System.Windows.Controls;
using AccessControlPro.Admin.ViewModels;

namespace AccessControlPro.Admin.Views;

public partial class UsersView : UserControl
{
    private bool _loaded;

    public UsersView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            if (DataContext is UsersViewModel vm)
                await vm.LoadUsersAsync();
        };
    }
}
