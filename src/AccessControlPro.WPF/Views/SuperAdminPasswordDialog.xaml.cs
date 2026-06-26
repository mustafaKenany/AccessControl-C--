using System.Windows;

namespace AccessControlPro.WPF.Views;

public partial class SuperAdminPasswordDialog : Window
{
    public SuperAdminPasswordDialog(string actionDescription)
    {
        InitializeComponent();
        ActionText.Text = actionDescription;
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Helpers.SuperAdminGate.Verify(PasswordBox.Password))
        {
            DialogResult = true;
        }
        else
        {
            ErrorText.Text = "كلمة المرور غير صحيحة / Incorrect password";
            ErrorText.Visibility = Visibility.Visible;
            PasswordBox.Clear();
            PasswordBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
