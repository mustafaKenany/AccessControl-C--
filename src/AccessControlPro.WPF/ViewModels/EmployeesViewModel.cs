using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class EmployeesViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;
    private readonly IDeviceService _deviceService;
    private const int PageSize = 100;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _withCardCount;

    [ObservableProperty]
    private int _withoutCardCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private EmployeeDto? _selectedEmployee;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _isDataLoaded;

    public ObservableCollection<EmployeeDto> Employees { get; } = new();

    public EmployeesViewModel(IEmployeeService employeeService, IDeviceService deviceService)
    {
        _employeeService = employeeService;
        _deviceService = deviceService;
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            _ = LoadPagedAsync();
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        CurrentPage = 1;
        IsDataLoaded = true;
        await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsDataLoaded)
            await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPagedAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPagedAsync();
        }
    }

    [RelayCommand]
    private async Task AddEmployeeAsync()
    {
        var dialog = new AddEmployeeDialog();
        dialog.SetValidationService(_employeeService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        try
        {
            var dto = new EmployeeDto
            {
                FullNameEn = dialog.FullNameEn,
                FullNameAr = dialog.FullNameAr,
                CardNo = dialog.CardNo,
                SubscriptionType = dialog.SubscriptionType,
                Phone = dialog.Phone,
                PhotoData = dialog.PhotoData,
                Height = dialog.PlayerHeight,
                Weight = dialog.PlayerWeight,
                SubscriptionFee = dialog.SubscriptionFee,
                AmountPaid = dialog.AmountPaid,
                StartDate = dialog.StartDate,
                EndDate = dialog.EndDate,
                Notes = dialog.Notes
            };

            await _employeeService.AddEmployeeAsync(dto);
            StatusMessage = Lang.AddPlayerSuccess;
            IsDataLoaded = true;
            await LoadPagedAsync();
            CustomMessageBox.Show(Lang.AddPlayerSuccess, Lang.AddPlayer, MsgType.Success,
                System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.StartsWith("DUPLICATE_CARD:"))
                msg = string.Format(Lang.DuplicateCardNo, msg.Replace("DUPLICATE_CARD:", ""));
            else if (msg.StartsWith("DUPLICATE_PHONE:"))
                msg = string.Format(Lang.DuplicatePhone, msg.Replace("DUPLICATE_PHONE:", ""));

            CustomMessageBox.Show(msg, Lang.AddPlayer, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task EditEmployeeAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Ask for edit reason first
        var reasonDialog = new EditReasonDialog();
        reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
        if (reasonDialog.ShowDialog() != true) return;

        var dialog = new AddEmployeeDialog(employee);
        dialog.SetValidationService(_employeeService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        try
        {
            var dto = new EmployeeDto
            {
                Id = employee.Id,
                FullNameEn = dialog.FullNameEn,
                FullNameAr = dialog.FullNameAr,
                CardNo = dialog.CardNo,
                SubscriptionType = dialog.SubscriptionType,
                Phone = dialog.Phone,
                PhotoData = dialog.PhotoData,
                Height = dialog.PlayerHeight,
                Weight = dialog.PlayerWeight,
                SubscriptionFee = dialog.SubscriptionFee,
                AmountPaid = dialog.AmountPaid,
                StartDate = dialog.StartDate,
                EndDate = dialog.EndDate,
                Notes = dialog.Notes
            };

            var success = await _employeeService.UpdateEmployeeAsync(dto, reasonDialog.Reason);
            if (success)
            {
                StatusMessage = Lang.EditPlayerSuccess;
                await LoadPagedAsync();
                CustomMessageBox.Show(Lang.EditPlayerSuccess, Lang.Edit, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.StartsWith("DUPLICATE_CARD:"))
                msg = string.Format(Lang.DuplicateCardNo, msg.Replace("DUPLICATE_CARD:", ""));
            else if (msg.StartsWith("DUPLICATE_PHONE:"))
                msg = string.Format(Lang.DuplicatePhone, msg.Replace("DUPLICATE_PHONE:", ""));

            CustomMessageBox.Show(msg, Lang.Edit, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Ask for delete reason
        var reasonDialog = new DeleteReasonDialog();
        reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
        if (reasonDialog.ShowDialog() != true) return;

        var confirmed = CustomMessageBox.Confirm(
            Lang.ConfirmDeletePlayer,
            Lang.Delete,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);

        if (confirmed)
        {
            await _employeeService.SoftDeleteEmployeeAsync(employee.Id, reasonDialog.Reason);
            await LoadPagedAsync();
            CustomMessageBox.Show(Lang.DeletePlayerSuccess, Lang.Delete, MsgType.Success,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task AssignCardAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Load devices to show in dialog
        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();

        var dialog = new AssignCardDialog(employee, devices);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        try
        {
            var cardDto = new AccessCardDto
            {
                CardNumber = dialog.CardNumber,
                CardPassword = dialog.CardPassword,
                CardType = dialog.CardType,
                OpenMode = dialog.OpenMode,
                DoorPermissions = dialog.DoorPermissions,
                EffectiveTimes = dialog.EffectiveTimes,
                TimePeriodIndex = dialog.TimePeriodIndex,
                HolidayEnabled = dialog.HolidayEnabled,
                ValidFrom = dialog.ValidFrom,
                ValidTo = dialog.ValidTo,
                IsActive = true
            };

            var success = await _employeeService.AssignCardAsync(employee.Id, cardDto);
            if (success)
            {
                // Auto-sync card to all selected devices
                var selectedDeviceIds = dialog.SelectedDeviceIds;
                if (selectedDeviceIds.Count > 0)
                {
                    var updatedEmployee = await _employeeService.GetEmployeeByIdAsync(employee.Id);
                    var latestCard = updatedEmployee?.Cards.LastOrDefault();
                    if (latestCard != null)
                    {
                        foreach (var deviceId in selectedDeviceIds)
                        {
                            await _employeeService.SyncCardToDeviceAsync(latestCard.Id, deviceId);
                        }
                    }
                }

                StatusMessage = Lang.AssignCardSuccess;
                await LoadPagedAsync();
                CustomMessageBox.Show(Lang.AssignCardSuccess, Lang.AssignCard, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.AssignCard, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task RemoveCardAsync(EmployeeDto? employee)
    {
        if (employee == null || employee.CardCount == 0) return;

        var cards = employee.Cards;
        if (cards.Count == 0) return;

        AccessCardDto cardToRemove;
        if (cards.Count == 1)
        {
            cardToRemove = cards[0];
        }
        else
        {
            var options = new System.Text.StringBuilder();
            options.AppendLine(Lang.SelectCardToRemove);
            for (int i = 0; i < cards.Count; i++)
                options.AppendLine($"  {i + 1} - {cards[i].CardNumber}");

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                options.ToString(), Lang.RemoveCard, "1");

            if (string.IsNullOrWhiteSpace(input)) return;
            if (!int.TryParse(input, out int idx) || idx < 1 || idx > cards.Count) return;
            cardToRemove = cards[idx - 1];
        }

        var confirmed = CustomMessageBox.Confirm(
            $"{Lang.ConfirmRemoveCard}\n{cardToRemove.CardNumber}",
            Lang.RemoveCard,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);

        if (confirmed)
        {
            await _employeeService.RemoveCardAsync(cardToRemove.Id);
            StatusMessage = Lang.RemoveCardSuccess;
            await LoadPagedAsync();
        }
    }

    [RelayCommand]
    private async Task FreezePlayerAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        if (employee.IsFrozen)
        {
            // Unfreeze
            var confirmed = CustomMessageBox.Confirm(
                Lang.ConfirmUnfreeze,
                Lang.UnfreezePlayer,
                MsgType.Info,
                System.Windows.Application.Current.MainWindow);

            if (!confirmed) return;

            try
            {
                var success = await _employeeService.UnfreezePlayerAsync(employee.Id);
                if (success)
                {
                    await LoadPagedAsync();
                    CustomMessageBox.Show(Lang.UnfreezeSuccess, Lang.UnfreezePlayer, MsgType.Success,
                        System.Windows.Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, Lang.UnfreezePlayer, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        else
        {
            // Freeze - ask for reason using styled dialog
            var reasonDialog = new FreezeReasonDialog();
            reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
            if (reasonDialog.ShowDialog() != true) return;

            try
            {
                var success = await _employeeService.FreezePlayerAsync(employee.Id, reasonDialog.Reason);
                if (success)
                {
                    await LoadPagedAsync();
                    CustomMessageBox.Show(Lang.FreezeSuccess, Lang.FreezePlayer, MsgType.Success,
                        System.Windows.Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, Lang.FreezePlayer, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
        }
    }

    [RelayCommand]
    private async Task RenewSubscriptionAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        var dialog = new RenewSubscriptionDialog(employee);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        try
        {
            var success = await _employeeService.RenewSubscriptionAsync(
                employee.Id,
                dialog.SelectedSubscriptionType,
                dialog.SelectedMonths,
                dialog.NewFee,
                dialog.NewAmountPaid);

            if (success)
            {
                await LoadPagedAsync();
                CustomMessageBox.Show(Lang.RenewSuccess, Lang.RenewSubscription, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.RenewSubscription, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private void ViewCards(EmployeeDto? employee)
    {
        if (employee == null || employee.CardCount == 0)
        {
            CustomMessageBox.Show(Lang.NoCardsAssigned, Lang.NavCards, MsgType.Info,
                System.Windows.Application.Current.MainWindow);
            return;
        }

        var dialog = new ViewCardsDialog(employee);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    private async Task LoadPagedAsync()
    {
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var (items, totalCount) = await _employeeService.GetPagedAsync(CurrentPage, PageSize, search);
            var list = items.ToList();

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            WithCardCount = list.Count(e => e.CardCount > 0);
            WithoutCardCount = list.Count(e => e.CardCount == 0);

            Employees.Clear();
            foreach (var emp in list)
                Employees.Add(emp);
        }
        catch
        {
            // Empty state on first run or DB not ready
        }
        finally
        {
            IsLoading = false;
        }
    }
}
