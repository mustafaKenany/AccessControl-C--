using System.Windows.Controls;

namespace AccessControlPro.WPF.Controls;

/// <summary>
/// A big on-screen numeric keypad so a non-typing operator can fill phone / card / fee fields by
/// tapping (touch screens, or just easier than hunting the keyboard). It writes into whichever
/// <see cref="TargetBox"/> the host dialog points it at — set that on each field's GotFocus.
/// </summary>
public partial class NumericKeypad : UserControl
{
    /// <summary>The text box the keys currently type into. The host sets this on field focus.</summary>
    public TextBox? TargetBox { get; set; }

    public NumericKeypad()
    {
        InitializeComponent();
    }

    private void Digit_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TargetBox == null || sender is not Button b || b.Content is not string digit) return;
        TargetBox.Text += digit;
        MoveCaretToEnd();
    }

    private void Backspace_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TargetBox == null || string.IsNullOrEmpty(TargetBox.Text)) return;
        TargetBox.Text = TargetBox.Text[..^1];
        MoveCaretToEnd();
    }

    private void Clear_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (TargetBox == null) return;
        TargetBox.Text = string.Empty;
        MoveCaretToEnd();
    }

    private void MoveCaretToEnd()
    {
        if (TargetBox == null) return;
        TargetBox.Focus();
        TargetBox.CaretIndex = TargetBox.Text.Length;
    }
}
