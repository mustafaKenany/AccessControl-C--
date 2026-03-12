using System.Windows;
using AccessControlPro.Application.DTOs;

namespace AccessControlPro.WPF.Views;

public partial class SelectDoorDialog : Window
{
    public List<DoorDto> SelectedDoors { get; private set; } = new();

    public SelectDoorDialog(List<DoorDto> doors)
    {
        InitializeComponent();
        DoorList.ItemsSource = doors;
        // Select all by default
        foreach (var door in doors)
            DoorList.SelectedItems.Add(door);
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        SelectedDoors = DoorList.SelectedItems.Cast<DoorDto>().ToList();
        if (SelectedDoors.Count > 0)
            DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
