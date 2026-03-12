using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class EmployeesViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;
    private readonly IDeviceService _deviceService;
    private readonly ILookupService _lookupService;
    private readonly CurrentUserService _currentUser;
    private const int PageSize = 100;
    private CancellationTokenSource? _searchCts;

    public LanguageManager Lang => LanguageManager.Instance;

    // Permission-based action visibility
    public bool CanAdd => _currentUser.HasPermission(AppPermission.PlayersAdd);
    public bool CanEdit => _currentUser.HasPermission(AppPermission.PlayersEdit);
    public bool CanDelete => _currentUser.HasPermission(AppPermission.PlayersDelete);
    public bool CanAssignCard => _currentUser.HasPermission(AppPermission.PlayersAssignCard);
    public bool CanRemoveCard => _currentUser.HasPermission(AppPermission.PlayersRemoveCard);
    public bool CanFreeze => _currentUser.HasPermission(AppPermission.PlayersFreeze);
    public bool CanRenew => _currentUser.HasPermission(AppPermission.PlayersRenew);
    public bool CanViewReports => _currentUser.HasPermission(AppPermission.PlayersReports);

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

    // Filters (0=All, 1=Expiring, 2=Renewed, 3=Frozen, 4=Expired)
    [ObservableProperty]
    private int _selectedFilterIndex;

    [ObservableProperty]
    private int _selectedPeriodIndex; // 0=Today, 1=This Week, 2=Last Week, 3=This Month, 4=Last Month

    public ObservableCollection<EmployeeDto> Employees { get; } = new();

    public EmployeesViewModel(IEmployeeService employeeService, IDeviceService deviceService, ILookupService lookupService, CurrentUserService currentUser)
    {
        _employeeService = employeeService;
        _deviceService = deviceService;
        _lookupService = lookupService;
        _currentUser = currentUser;
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            _ = DebouncedSearchAsync();
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
                await LoadPagedAsync();
        }
        catch (TaskCanceledException) { }
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
        var dialog = new AddEmployeeDialog(_lookupService);
        dialog.SetValidationService(_employeeService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Adding new player...";

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
                Notes = dialog.Notes,
                MaxVisits = dialog.MaxVisits
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
            else if (msg.Contains("constraint"))
                msg = "Database constraint violation. Please check your input and try again.";

            CustomMessageBox.Show(msg, Lang.AddPlayer, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task EditEmployeeAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Step 1: Show edit dialog with current data from DB
        var dialog = new AddEmployeeDialog(employee, _lookupService);
        dialog.SetValidationService(_employeeService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

        if (dialog.ShowDialog() != true) return;

        // Step 2: Ask for edit reason AFTER changes are made
        var reasonDialog = new EditReasonDialog();
        reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
        reasonDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        if (reasonDialog.ShowDialog() != true) return;

        IsLoading = true;
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
                Notes = dialog.Notes,
                MaxVisits = dialog.MaxVisits
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
            var errorMsg = ex.Message;
            if (errorMsg.Contains("foreign key") || errorMsg.Contains("constraint"))
                errorMsg = "Player data is inconsistent in database. This player may have been deleted. Please refresh and try again.";
            else if (errorMsg.Contains("Concurrency"))
                errorMsg = "This player was modified by another user. Please refresh the list and try again.";

            CustomMessageBox.Show(errorMsg, Lang.Edit, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Ask for delete reason
        var reasonDialog = new DeleteReasonDialog();
        reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
        reasonDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        if (reasonDialog.ShowDialog() != true) return;

        var confirmed = CustomMessageBox.Confirm(
            Lang.ConfirmDeletePlayer,
            Lang.Delete,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);

        if (confirmed)
        {
            IsLoading = true;
            try
            {
                await _employeeService.SoftDeleteEmployeeAsync(employee.Id, reasonDialog.Reason);
                await LoadPagedAsync();
                StatusMessage = Lang.DeletePlayerSuccess;
                CustomMessageBox.Show(Lang.DeletePlayerSuccess, Lang.Delete, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (errorMsg.Contains("foreign key") || errorMsg.Contains("constraint"))
                    errorMsg = "Cannot delete player: This player has associated records in the system. Soft delete recorded instead.";
                else if (errorMsg.Contains("Concurrency"))
                    errorMsg = "This player was modified by another user. Please refresh and try again.";

                CustomMessageBox.Show(errorMsg, Lang.Delete, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
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
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

        if (dialog.ShowDialog() != true) return;

        // Step 1: Check WiFi and verify selected devices are connected
        var selectedDeviceIds = dialog.SelectedDeviceIds;
        if (selectedDeviceIds.Count > 0)
        {
            // WiFi check
            if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
            {
                CustomMessageBox.Show(Lang.WifiWarning,
                    Lang.AssignCard, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                return;
            }

            IsLoading = true;
            try
            {
                StatusMessage = Lang.VerifyingDevices;

                var disconnectedDevices = new List<string>();
                foreach (var deviceId in selectedDeviceIds)
                {
                    var device = devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device == null) continue;

                    var reachable = await NetworkHelper.PingDeviceAsync(device.IP);
                    if (!reachable)
                        disconnectedDevices.Add($"- {device.Name} ({device.IP})");
                }

                if (disconnectedDevices.Count > 0)
                {
                    var msg = Lang.DevicesOfflineList + "\n\n" + string.Join("\n", disconnectedDevices);
                    CustomMessageBox.Show(msg, Lang.AssignCard, MsgType.Warning,
                        System.Windows.Application.Current.MainWindow);
                    StatusMessage = Lang.SomeDevicesOffline;
                    return;
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        IsLoading = true;
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
                if (selectedDeviceIds.Count > 0)
                {
                    var updatedEmployee = await _employeeService.GetEmployeeByIdAsync(employee.Id);
                    var latestCard = updatedEmployee?.Cards.LastOrDefault();
                    if (latestCard != null)
                    {
                        int syncedCount = 0;
                        var syncErrors = new List<string>();

                        foreach (var deviceId in selectedDeviceIds)
                        {
                            try
                            {
                                StatusMessage = string.Format(Lang.SyncingCardToDevice, syncedCount + 1, selectedDeviceIds.Count);
                                await _employeeService.SyncCardToDeviceAsync(latestCard.Id, deviceId);
                                syncedCount++;
                            }
                            catch (Exception ex)
                            {
                                var device = devices.FirstOrDefault(d => d.Id == deviceId);
                                syncErrors.Add($"- {device?.Name ?? $"Device {deviceId}"}: {ex.Message}");
                            }
                        }

                        if (syncErrors.Count > 0)
                        {
                            var syncMsg = string.Format(Lang.CardAssignedSyncErrors, syncedCount, selectedDeviceIds.Count, string.Join("\n", syncErrors));
                            CustomMessageBox.Show(syncMsg, Lang.AssignCard, MsgType.Warning,
                                System.Windows.Application.Current.MainWindow);
                        }
                        else
                        {
                            StatusMessage = "✓ " + string.Format(Lang.CardAssignedSynced, syncedCount);
                            CustomMessageBox.Show(string.Format(Lang.CardAssignedSynced, syncedCount), Lang.AssignCard, MsgType.Success,
                                System.Windows.Application.Current.MainWindow);
                        }
                    }
                }
                else
                {
                    StatusMessage = Lang.AssignCardSuccess;
                    CustomMessageBox.Show(Lang.AssignCardSuccess, Lang.AssignCard, MsgType.Success,
                        System.Windows.Application.Current.MainWindow);
                }

                await LoadPagedAsync();
            }
        }
        catch (Exception ex)
        {
            var errorMsg = ex.Message;
            if (errorMsg.Contains("foreign key"))
                errorMsg = Lang.PlayerDbInconsistent;
            else if (errorMsg.Contains("Timeout"))
                errorMsg = Lang.DeviceTimeout;

            CustomMessageBox.Show(errorMsg, Lang.AssignCard, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
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
            IsLoading = true;
            try
            {
                StatusMessage = string.Format(Lang.RemovingCard, cardToRemove.CardNumber);
                await _employeeService.RemoveCardAsync(cardToRemove.Id);
                StatusMessage = Lang.RemoveCardSuccess;
                await LoadPagedAsync();
                CustomMessageBox.Show(string.Format(Lang.CardRemovedSuccess, cardToRemove.CardNumber), Lang.RemoveCard, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (errorMsg.Contains("foreign key") || errorMsg.Contains("constraint"))
                    errorMsg = Lang.CardRemoveConstraint;
                else if (errorMsg.Contains("not found"))
                    errorMsg = string.Format(Lang.CardNotFoundRemoved, cardToRemove.CardNumber);

                CustomMessageBox.Show(errorMsg, Lang.RemoveCard, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task FreezePlayerAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();

        if (employee.IsFrozen)
        {
            // Unfreeze
            var confirmed = CustomMessageBox.Confirm(
                Lang.ConfirmUnfreeze,
                Lang.UnfreezePlayer,
                MsgType.Info,
                System.Windows.Application.Current.MainWindow);

            if (!confirmed) return;

            // Re-enable on all devices by default for unfreeze
            List<int>? selectedDeviceIds = devices.Count > 0
                ? devices.Select(d => d.Id).ToList()
                : null;

            IsLoading = true;
            try
            {
                StatusMessage = Lang.UnfreezingPlayer;
                var success = await _employeeService.UnfreezePlayerAsync(employee.Id, selectedDeviceIds);
                if (success)
                {
                    StatusMessage = Lang.UnfreezeSuccess;
                    await LoadPagedAsync();
                    CustomMessageBox.Show(Lang.UnfreezeSuccess, Lang.UnfreezePlayer, MsgType.Success,
                        System.Windows.Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (errorMsg.Contains("not found"))
                    errorMsg = Lang.PlayerNotFoundDeleted;
                CustomMessageBox.Show(errorMsg, Lang.UnfreezePlayer, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
        }
        else
        {
            // Freeze - ask for reason and select devices
            var reasonDialog = new FreezeReasonDialog(devices);
            reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
            reasonDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            if (reasonDialog.ShowDialog() != true) return;

            var selectedDeviceIds = reasonDialog.SelectedDeviceIds;

            IsLoading = true;
            try
            {
                StatusMessage = Lang.FreezingAccount;
                var success = await _employeeService.FreezePlayerAsync(employee.Id, reasonDialog.Reason, selectedDeviceIds);
                if (success)
                {
                    StatusMessage = Lang.FreezeSuccess;
                    await LoadPagedAsync();
                    CustomMessageBox.Show(Lang.FreezeSuccess, Lang.FreezePlayer, MsgType.Success,
                        System.Windows.Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (errorMsg.Contains("not found"))
                    errorMsg = Lang.PlayerNotFoundDeleted;
                CustomMessageBox.Show(errorMsg, Lang.FreezePlayer, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RenewSubscriptionAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();
        var dialog = new RenewSubscriptionDialog(employee, devices, _lookupService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

        if (dialog.ShowDialog() != true) return;

        // Step 1: Check WiFi and device connectivity before renewing
        var selectedDeviceIds = dialog.SelectedDeviceIds;
        if (selectedDeviceIds.Count > 0)
        {
            // WiFi check
            if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
            {
                CustomMessageBox.Show(Lang.WifiWarning,
                    Lang.RenewSubscription, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                return;
            }

            IsLoading = true;
            try
            {
                StatusMessage = Lang.VerifyingDevices;

                var disconnectedDevices = new List<string>();
                foreach (var deviceId in selectedDeviceIds)
                {
                    var device = devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device == null) continue;

                    var reachable = await NetworkHelper.PingDeviceAsync(device.IP);
                    if (!reachable)
                        disconnectedDevices.Add($"- {device.Name} ({device.IP})");
                }

                if (disconnectedDevices.Count > 0)
                {
                    var msg = Lang.DevicesOfflineList + "\n\n" +
                              string.Join("\n", disconnectedDevices) +
                              "\n\n" + Lang.DevicesOfflineRenew;

                    var continueAnyway = CustomMessageBox.Confirm(
                        msg,
                        Lang.RenewSubscription,
                        MsgType.Warning,
                        System.Windows.Application.Current.MainWindow);

                    if (!continueAnyway)
                    {
                        StatusMessage = Lang.RenewalCancelled;
                        IsLoading = false;
                        return;
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        IsLoading = true;
        try
        {
            StatusMessage = Lang.RenewingSubscription;

            StatusMessage = Lang.RenewingSubscription;

            var success = await _employeeService.RenewSubscriptionAsync(
                employee.Id,
                dialog.SelectedSubscriptionType,
                dialog.SelectedMonths,
                dialog.SelectedCustomDays,
                dialog.NewFee,
                dialog.NewAmountPaid,
                dialog.DoorPermissions,
                dialog.EffectiveTimes,
                selectedDeviceIds.Count > 0 ? selectedDeviceIds : null);

            if (success)
            {
                await LoadPagedAsync();
                StatusMessage = "✓ " + Lang.RenewedSuccessfully;

                // Show renewal receipt
                var renewed = await _employeeService.GetEmployeeByIdAsync(employee.Id);
                if (renewed != null)
                {
                    var receiptName = LanguageManager.Instance.IsArabic
                        ? renewed.FullNameAr : renewed.FullNameEn;
                    var receipt = new RenewalReceiptDialog(
                        receiptName, renewed.CardNo, dialog.SelectedSubscriptionType,
                        dialog.SelectedMonths, dialog.SelectedCustomDays,
                        renewed.StartDate, renewed.EndDate,
                        dialog.NewFee, dialog.NewAmountPaid);
                    receipt.Owner = System.Windows.Application.Current.MainWindow;
                    receipt.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                    receipt.ShowDialog();
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = ex.Message;
            if (errorMsg.Contains("not found"))
                errorMsg = Lang.PlayerNotFoundDeleted;
            else if (errorMsg.Contains("Concurrency"))
                errorMsg = Lang.ConcurrencyError;
            else if (errorMsg.Contains("constraint"))
                errorMsg = Lang.DatabaseError;

            CustomMessageBox.Show(errorMsg, Lang.RenewSubscription, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ViewProfile(EmployeeDto? employee)
    {
        if (employee == null) return;
        var dialog = new PlayerProfileDialog(_employeeService, employee.Id);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        dialog.ShowDialog();
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
        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task SetFilterAsync(string indexStr)
    {
        if (!int.TryParse(indexStr, out var index)) return;
        SelectedFilterIndex = index;
        CurrentPage = 1;
        IsDataLoaded = true;
        await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task SetPeriodAsync(string indexStr)
    {
        if (!int.TryParse(indexStr, out var index)) return;
        SelectedPeriodIndex = index;
        CurrentPage = 1;
        await LoadPagedAsync();
    }

    private (DateTime from, DateTime to) GetPeriodRange()
    {
        var now = DateTime.Now;
        return SelectedPeriodIndex switch
        {
            0 => (now.Date, now.Date.AddDays(1).AddSeconds(-1)),
            1 => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(-(int)now.DayOfWeek).AddDays(7).AddSeconds(-1)),
            2 => (now.Date.AddDays(-(int)now.DayOfWeek - 7), now.Date.AddDays(-(int)now.DayOfWeek - 7).AddDays(7).AddSeconds(-1)),
            3 => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(-1)),
            4 => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1).AddSeconds(-1)),
            _ => (now.Date, now.Date.AddDays(1).AddSeconds(-1))
        };
    }

    [RelayCommand]
    private async Task BulkOperationAsync()
    {
        var dialog = new BulkOperationDialog();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        // Get target player IDs
        var ids = new List<int>();
        var today = DateTime.Today;

        switch (dialog.SelectedTarget)
        {
            case 0: // All active (not frozen)
                var all = await _employeeService.GetAllEmployeesAsync();
                ids = all.Where(e => !e.IsFrozen).Select(e => e.Id).ToList();
                break;
            case 1: // Expiring in 7 days
                var expiring = await _employeeService.GetExpiringAsync(today, today.AddDays(7));
                ids = expiring.Select(e => e.Id).ToList();
                break;
            case 2: // Expired
                var expired = await _employeeService.GetExpiredPlayersAsync();
                ids = expired.Select(e => e.Id).ToList();
                break;
            case 3: // Frozen
                var frozen = await _employeeService.GetFrozenPlayersAsync();
                ids = frozen.Select(e => e.Id).ToList();
                break;
        }

        if (ids.Count == 0)
        {
            CustomMessageBox.Show(Lang.BulkNoPlayers, Lang.BulkOperations, MsgType.Info,
                System.Windows.Application.Current.MainWindow);
            return;
        }

        var confirmMsg = string.Format(Lang.BulkConfirm, ids.Count);
        if (!CustomMessageBox.Confirm(confirmMsg, Lang.BulkOperations, MsgType.Warning,
            System.Windows.Application.Current.MainWindow))
            return;

        IsLoading = true;
        try
        {
            (int success, int failed) result;
            switch (dialog.SelectedOperation)
            {
                case 0: // Freeze
                    result = await _employeeService.BulkFreezeAsync(ids, dialog.FreezeReason);
                    break;
                case 1: // Unfreeze
                    result = await _employeeService.BulkUnfreezeAsync(ids);
                    break;
                case 2: // Extend
                    result = await _employeeService.BulkExtendAsync(ids, dialog.ExtendDays);
                    break;
                default:
                    return;
            }

            await LoadPagedAsync();
            CustomMessageBox.Show(
                $"{Lang.BulkComplete}\n{Lang.BulkSuccessCount}: {result.success}\n{Lang.BulkFailedCount}: {result.failed}",
                Lang.BulkOperations, MsgType.Success,
                System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.BulkOperations, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPagedAsync()
    {
        IsLoading = true;
        try
        {
            if (SelectedFilterIndex == 0)
            {
                // Normal paged loading
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
            else
            {
                // Filter mode — load from service methods
                var (from, to) = GetPeriodRange();
                IEnumerable<EmployeeDto> results = SelectedFilterIndex switch
                {
                    1 => await _employeeService.GetExpiringAsync(from, to),
                    2 => await _employeeService.GetRenewedAsync(from, to),
                    3 => await _employeeService.GetFrozenPlayersAsync(),
                    4 => await _employeeService.GetExpiredPlayersAsync(),
                    _ => []
                };

                var list = results.ToList();

                // Apply search filter on results
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    list = list.Where(e =>
                        e.FullNameEn.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        e.FullNameAr.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        (e.CardNo ?? "").Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        (e.Phone ?? "").Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                TotalCount = list.Count;
                TotalPages = 1;
                CurrentPage = 1;
                WithCardCount = list.Count(e => e.CardCount > 0);
                WithoutCardCount = list.Count(e => e.CardCount == 0);

                Employees.Clear();
                foreach (var emp in list)
                    Employees.Add(emp);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
