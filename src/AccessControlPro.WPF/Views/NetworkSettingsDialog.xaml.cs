using System.Net;
using System.Windows;

namespace AccessControlPro.WPF.Views;

public partial class NetworkSettingsDialog : Window
{
    public string IpAddress => IpTextBox.Text.Trim();
    public string SubnetMask => SubnetTextBox.Text.Trim();
    public string Gateway => GatewayTextBox.Text.Trim();

    public NetworkSettingsDialog(string currentIP, string currentSubnet, string currentGateway)
    {
        InitializeComponent();
        IpTextBox.Text = currentIP;
        SubnetTextBox.Text = currentSubnet;
        GatewayTextBox.Text = currentGateway;
        IpTextBox.Focus();
        IpTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IPAddress.TryParse(IpAddress, out _))
        {
            CustomMessageBox.Show("Invalid IP address format.", "Validation",
                MsgType.Warning, this);
            IpTextBox.Focus();
            return;
        }
        if (!IPAddress.TryParse(SubnetMask, out _))
        {
            CustomMessageBox.Show("Invalid subnet mask format.", "Validation",
                MsgType.Warning, this);
            SubnetTextBox.Focus();
            return;
        }
        if (!IPAddress.TryParse(Gateway, out _))
        {
            CustomMessageBox.Show("Invalid gateway format.", "Validation",
                MsgType.Warning, this);
            GatewayTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
