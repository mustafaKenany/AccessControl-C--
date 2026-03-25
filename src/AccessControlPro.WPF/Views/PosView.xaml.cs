using System.Windows;
using System.Windows.Controls;

namespace AccessControlPro.WPF.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void PosView_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto-focus the barcode input for scanner support
        BarcodeInputBox?.Focus();
    }
}
