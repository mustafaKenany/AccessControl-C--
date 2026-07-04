using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
    private readonly ITimeGroupService _timeGroupService;
    private readonly IDoorService _doorService;
    private readonly CurrentUserService _currentUser;
    private readonly IQrPassService _qrPassService;
    private readonly IFinanceService _financeService;
    private const int PageSize = 100;
    private CancellationTokenSource? _searchCts;

    // Double-click guard for player-mutation commands. The Basmia session.log shows
    // staff repeatedly clicking Renew/AssignCard twice within 1-2 seconds when they
    // don't get instant visual feedback — causing duplicate transactions and double
    // re-uploads of the same card to the device. Tracking the EmployeeId set here
    // lets us silently swallow the second click until the first operation finishes.
    private readonly HashSet<int> _activeMutations = new();
    private readonly object _activeMutationsLock = new();

    private bool TryClaimMutation(int employeeId)
    {
        lock (_activeMutationsLock)
        {
            if (_activeMutations.Contains(employeeId)) return false;
            _activeMutations.Add(employeeId);
            return true;
        }
    }

    private void ReleaseMutation(int employeeId)
    {
        lock (_activeMutationsLock)
        {
            _activeMutations.Remove(employeeId);
        }
    }

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
    public bool CanSyncToDevice => _currentUser.HasPermission(AppPermission.PlayersSyncToDevice);
    public bool CanDailyPass => _currentUser.HasPermission(AppPermission.PlayersDailyPass);
    public bool CanBulkOperations => _currentUser.HasPermission(AppPermission.PlayersBulkOperations);
    public bool CanCreateMissingCards => _currentUser.HasPermission(AppPermission.PlayersCreateMissingCards);
    public bool CanPrintList => _currentUser.HasPermission(AppPermission.PlayersPrintList);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _withCardCount;

    [ObservableProperty]
    private int _withoutCardCount;

    // Live per-filter counts over the WHOLE dataset (respecting the current period + search) so the
    // gym owner sees his totals at a glance on every pill. Recomputed only when the context changes
    // (page 1), not on page navigation. Filter indexes: 1=Expiring 2=Renewed 3=Frozen 4=Expired 5=Active.
    [ObservableProperty] private int _allCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _expiringCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private int _frozenCount;
    [ObservableProperty] private int _renewedCount;

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

    public EmployeesViewModel(IEmployeeService employeeService, IDeviceService deviceService, ILookupService lookupService, ITimeGroupService timeGroupService, CurrentUserService currentUser, IQrPassService qrPassService, IFinanceService financeService, IDoorService doorService)
    {
        _employeeService = employeeService;
        _deviceService = deviceService;
        _lookupService = lookupService;
        _timeGroupService = timeGroupService;
        _doorService = doorService;
        _currentUser = currentUser;
        _qrPassService = qrPassService;
        _financeService = financeService;
    }

    [RelayCommand]
    private void CreateDailyPass()
    {
        var dialog = new DailyPassDialog(_qrPassService, _lookupService, _financeService, _employeeService)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog();
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
            ActivityLogger.LogAction("Employees", "AddEmployee", dialog.FullNameEn);
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
                Discount = dialog.Discount,
                AmountPaid = dialog.AmountPaid,
                StartDate = dialog.StartDate,
                EndDate = dialog.EndDate,
                Notes = dialog.Notes,
                MaxVisits = dialog.MaxVisits
            };

            var newId = await _employeeService.AddEmployeeAsync(dto);
            StatusMessage = Lang.AddPlayerSuccess;
            IsDataLoaded = true;
            await LoadPagedAsync();

            // Shortcut for the operator: if a card number was entered, jump STRAIGHT into Assign Card
            // (pre-filled + sync to the gate) right after registering — then the receipt. If no card
            // was entered, skip to the receipt. The Assign Card dialog is still cancellable.
            if (!string.IsNullOrWhiteSpace(dto.CardNo))
            {
                var created = await _employeeService.GetEmployeeByIdAsync(newId);
                if (created != null)
                    await AssignCardAsync(created);
            }
            else
            {
                CustomMessageBox.Show(Lang.AddPlayerSuccess, Lang.AddPlayer, MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }

            // Optional 80mm registration receipt
            if (CustomMessageBox.Confirm(
                    Lang.IsArabic ? "هل تريد طباعة إيصال التسجيل؟" : "Print registration receipt?",
                    Lang.AddPlayer))
            {
                var rname = Lang.IsArabic
                    ? (string.IsNullOrWhiteSpace(dto.FullNameAr) ? dto.FullNameEn : dto.FullNameAr)
                    : (string.IsNullOrWhiteSpace(dto.FullNameEn) ? dto.FullNameAr : dto.FullNameEn);
                ThermalReceipt.PrintSubscription(
                    Lang.IsArabic ? "إيصال تسجيل" : "Registration Receipt",
                    rname, dto.CardNo, dto.SubscriptionType,
                    dto.StartDate, dto.EndDate, dto.SubscriptionFee, dto.AmountPaid);
            }
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

        // Reload the full member INCLUDING the photo — the list DTOs omit the PhotoData blob for
        // speed, so editing off the list DTO would blank the photo on save.
        employee = await _employeeService.GetEmployeeByIdAsync(employee.Id) ?? employee;

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
        StatusMessage = Lang.IsArabic ? "جارٍ تحديث البيانات..." : "Updating player...";
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
                Discount = dialog.Discount,
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
            ActivityLogger.LogAction("Employees", "DeleteEmployee", $"{employee.FullNameEn} (ID:{employee.Id})");
            IsLoading = true;
            StatusMessage = Lang.IsArabic ? "جارٍ حذف اللاعب..." : "Deleting player...";
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

                if (errorMsg.Contains("PARTIAL_SUCCESS:"))
                {
                    // Player deleted from DB but hardware removal failed — show warning, not error
                    await LoadPagedAsync();
                    var warningMsg = errorMsg.Replace("PARTIAL_SUCCESS:", "");
                    CustomMessageBox.Show(warningMsg, Lang.Delete, MsgType.Warning,
                        System.Windows.Application.Current.MainWindow);
                }
                else if (errorMsg.Contains("Concurrency"))
                {
                    CustomMessageBox.Show("This player was modified by another user. Please refresh and try again.",
                        Lang.Delete, MsgType.Error, System.Windows.Application.Current.MainWindow);
                }
                else
                {
                    CustomMessageBox.Show(errorMsg, Lang.Delete, MsgType.Error,
                        System.Windows.Application.Current.MainWindow);
                }
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

        // Silently swallow rapid double-click on the same player's Assign button.
        // Without this guard, every second click in the Basmia session.log produced
        // a duplicate AddAccessCard call to the device.
        if (!TryClaimMutation(employee.Id)) return;
        var mutationKey = employee.Id;
        try
        {

        // Check if this is a card RE-assignment (player already has an active card)
        bool isReassignment = employee.Cards != null && employee.Cards.Any(c => c.IsActive);
        if (isReassignment)
        {
            // Permission check: only users with ReassignCard permission can re-assign
            if (!_currentUser.HasPermission(AppPermission.PlayersReassignCard))
            {
                var permMsg = Lang.IsArabic
                    ? "فقط المسؤولون يمكنهم إعادة تعيين البطاقات"
                    : "Only administrators can re-assign cards";
                CustomMessageBox.Show(permMsg, Lang.AssignCard, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
                return;
            }

            // Show warning dialog with current card info
            var activeCard = employee.Cards!.First(c => c.IsActive);
            bool numberChanged = !string.IsNullOrWhiteSpace(employee.CardNo)
                                 && !string.Equals(employee.CardNo.Trim(), activeCard.CardNumber, StringComparison.OrdinalIgnoreCase);
            var newLineAr = numberChanged ? $"الرقم الجديد الذي سيُعتمد: {employee.CardNo}\n" : "";
            var newLineEn = numberChanged ? $"New number to be assigned: {employee.CardNo}\n" : "";
            var tailAr = numberChanged
                ? "سيتم مسح البطاقة القديمة من البوابة واعتماد الرقم الجديد. هل أنت متأكد؟"
                : "هل أنت متأكد من إعادة التعيين؟ سيتم إعادة تعيين البطاقة بإعدادات جديدة.";
            var tailEn = numberChanged
                ? "The old card will be removed from the gate and the new number assigned. Are you sure?"
                : "Are you sure you want to re-assign? This will reset the card with new settings.";
            var warningMsg = Lang.IsArabic
                ? $"هذا اللاعب لديه بالفعل بطاقة نشطة:\n" +
                  $"الرقم الحالي: {activeCard.CardNumber}\n" +
                  newLineAr +
                  $"المرات الفعالة: {activeCard.EffectiveTimes}\n" +
                  $"المستخدم: {employee.UsedVisits}/{employee.MaxVisits}\n\n" +
                  tailAr
                : $"This player already has an active card:\n" +
                  $"Current number: {activeCard.CardNumber}\n" +
                  newLineEn +
                  $"Effective Times: {activeCard.EffectiveTimes}\n" +
                  $"Used: {employee.UsedVisits}/{employee.MaxVisits}\n\n" +
                  tailEn;

            var title = Lang.IsArabic ? "تحذير إعادة تعيين البطاقة" : "Card Re-assignment Warning";
            var confirmed = CustomMessageBox.Confirm(warningMsg, title, MsgType.Warning,
                System.Windows.Application.Current.MainWindow);
            if (!confirmed) return;
        }

        // Load devices to show in dialog
        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();
        var doors = (await _doorService.GetAllDoorsAsync()).ToList();

        var dialog = new AssignCardDialog(employee, devices, _timeGroupService, doors);
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

        ActivityLogger.LogAction("Employees", "AssignCard", $"{employee.FullNameEn} card={dialog.CardNumber}");
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
                var updatedEmployee = await _employeeService.GetEmployeeByIdAsync(employee.Id);
                // Sync the card we JUST assigned (match by number), not merely the "last" card — if the
                // member had other cards, LastOrDefault could push a stale card and leave the new one
                // un-synced, so its "gate sync" dot stayed red even though the assign reported success.
                var latestCard = updatedEmployee?.Cards?.FirstOrDefault(c => c.IsActive
                                     && string.Equals(c.CardNumber, dialog.CardNumber, StringComparison.OrdinalIgnoreCase))
                                 ?? updatedEmployee?.Cards?.LastOrDefault();

                if (selectedDeviceIds.Count > 0 && latestCard != null)
                {
                    // Sync card to devices SEQUENTIALLY (ping → SDK → DB)
                    int syncedCount = 0;
                    var syncErrors = new List<string>();

                    foreach (var deviceId in selectedDeviceIds)
                    {
                        try
                        {
                            StatusMessage = string.Format(Lang.SyncingCardToDevice, syncedCount + 1, selectedDeviceIds.Count) + " (please wait...)";
                            await _employeeService.SyncCardToDeviceAsync(latestCard.Id, deviceId);
                            syncedCount++;
                        }
                        catch (Exception ex)
                        {
                            var device = devices.FirstOrDefault(d => d.Id == deviceId);
                            syncErrors.Add($"- {device?.Name ?? $"Device {deviceId}"}: {ex.Message}");
                        }
                    }

                    if (syncedCount == 0)
                    {
                        // ALL devices failed → rollback: delete card from DB
                        try { await _employeeService.RemoveCardAsync(latestCard.Id); } catch { }
                        var errMsg = $"Card NOT assigned — failed to sync to all devices:\n\n{string.Join("\n", syncErrors)}";
                        CustomMessageBox.Show(errMsg, Lang.AssignCard, MsgType.Error,
                            System.Windows.Application.Current.MainWindow);
                    }
                    else if (syncErrors.Count > 0)
                    {
                        // Partial success
                        var syncMsg = string.Format(Lang.CardAssignedSyncErrors, syncedCount, selectedDeviceIds.Count, string.Join("\n", syncErrors));
                        CustomMessageBox.Show(syncMsg, Lang.AssignCard, MsgType.Warning,
                            System.Windows.Application.Current.MainWindow);
                    }
                    else
                    {
                        // All devices succeeded
                        StatusMessage = "✓ " + string.Format(Lang.CardAssignedSynced, syncedCount);
                        CustomMessageBox.Show(string.Format(Lang.CardAssignedSynced, syncedCount), Lang.AssignCard, MsgType.Success,
                            System.Windows.Application.Current.MainWindow);
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
        } // end of double-click try
        finally { ReleaseMutation(mutationKey); }
    }

    [RelayCommand]
    private async Task RemoveCardAsync(EmployeeDto? employee)
    {
        if (employee == null || employee.CardCount == 0) return;

        var cards = employee.Cards ?? new();
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

            ActivityLogger.LogAction("Employees", "UnfreezePlayer", $"{employee.FullNameEn} (ID:{employee.Id})");
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
                if (errorMsg.Contains("PARTIAL_SUCCESS:"))
                {
                    await LoadPagedAsync();
                    CustomMessageBox.Show(errorMsg.Replace("PARTIAL_SUCCESS:", ""), Lang.UnfreezePlayer, MsgType.Warning,
                        System.Windows.Application.Current.MainWindow);
                }
                else
                {
                    if (errorMsg.Contains("not found")) errorMsg = Lang.PlayerNotFoundDeleted;
                    CustomMessageBox.Show(errorMsg, Lang.UnfreezePlayer, MsgType.Error,
                        System.Windows.Application.Current.MainWindow);
                }
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

            ActivityLogger.LogAction("Employees", "FreezePlayer", $"{employee.FullNameEn} (ID:{employee.Id})");
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
                if (errorMsg.Contains("PARTIAL_SUCCESS:"))
                {
                    await LoadPagedAsync();
                    CustomMessageBox.Show(errorMsg.Replace("PARTIAL_SUCCESS:", ""), Lang.FreezePlayer, MsgType.Warning,
                        System.Windows.Application.Current.MainWindow);
                }
                else
                {
                    if (errorMsg.Contains("not found")) errorMsg = Lang.PlayerNotFoundDeleted;
                    CustomMessageBox.Show(errorMsg, Lang.FreezePlayer, MsgType.Error,
                        System.Windows.Application.Current.MainWindow);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Migration-default gate for the Renew flow. If <paramref name="employee"/> still has
    /// the sentinels MigrationService leaves behind (Phone="MIG-N" or SubscriptionType="Migrated"),
    /// shows a bilingual warning, opens the Edit dialog, saves the user's changes, then
    /// re-checks. Returns the updated employee if the record is now clean, or null if the
    /// user cancelled / didn't fix the placeholders. Caller should abort renewal on null.
    /// </summary>
    private async Task<EmployeeDto?> EnsureMigrationDefaultsResolvedAsync(EmployeeDto employee)
    {
        if (!IsMigrationDefault(employee)) return employee;

        var msg = string.Format(
            Lang.MigratedRenewBlockedMessage,
            string.IsNullOrEmpty(employee.Phone) ? "—" : employee.Phone,
            string.IsNullOrEmpty(employee.SubscriptionType) ? "—" : employee.SubscriptionType);
        var openEdit = CustomMessageBox.Confirm(
            msg,
            Lang.MigratedRenewBlockedTitle,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);
        if (!openEdit) return null;

        // Reload the full member (incl. photo blob, which list DTOs omit) so editing doesn't blank it.
        employee = await _employeeService.GetEmployeeByIdAsync(employee.Id) ?? employee;

        var editDialog = new AddEmployeeDialog(employee, _lookupService);
        editDialog.SetValidationService(_employeeService);
        editDialog.Owner = System.Windows.Application.Current.MainWindow;
        editDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        if (editDialog.ShowDialog() != true) return null;

        // Persist the edits via the same pipeline the Edit command uses so the activity log,
        // audit trail, and device sync all behave consistently. Reuse the existing reason
        // dialog so the audit log records "renewal-prep update".
        var reasonDialog = new EditReasonDialog();
        reasonDialog.Owner = System.Windows.Application.Current.MainWindow;
        reasonDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        if (reasonDialog.ShowDialog() != true) return null;

        IsLoading = true;
        try
        {
            var updated = new EmployeeDto
            {
                Id = employee.Id,
                FullNameEn = editDialog.FullNameEn,
                FullNameAr = editDialog.FullNameAr,
                CardNo = editDialog.CardNo,
                SubscriptionType = editDialog.SubscriptionType,
                Phone = editDialog.Phone,
                PhotoData = editDialog.PhotoData,
                Height = editDialog.PlayerHeight,
                Weight = editDialog.PlayerWeight,
                SubscriptionFee = editDialog.SubscriptionFee,
                Discount = editDialog.Discount,
                AmountPaid = editDialog.AmountPaid,
                StartDate = editDialog.StartDate,
                EndDate = editDialog.EndDate,
                Notes = editDialog.Notes,
                MaxVisits = editDialog.MaxVisits
            };
            await _employeeService.UpdateEmployeeAsync(updated, reasonDialog.Reason);
        }
        catch (Exception ex)
        {
            // Show the same friendly validation message the Edit command uses and abort the renewal
            // cleanly, instead of letting a duplicate-phone/card error crash to the global handler
            // (classic gym, 2026-07: migrated members sharing phone "0" → unhandled DUPLICATE_PHONE).
            var em = ex.Message;
            if (em.StartsWith("DUPLICATE_CARD:"))
                em = string.Format(Lang.DuplicateCardNo, em.Replace("DUPLICATE_CARD:", ""));
            else if (em.StartsWith("DUPLICATE_PHONE:"))
                em = string.Format(Lang.DuplicatePhone, em.Replace("DUPLICATE_PHONE:", ""));
            CustomMessageBox.Show(em, Lang.AddPlayer, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
            return null;
        }
        finally
        {
            IsLoading = false;
        }

        // Re-fetch the canonical row from the DB so we evaluate against persisted state.
        var refreshed = await _employeeService.GetEmployeeByIdAsync(employee.Id);
        if (refreshed == null) return null;

        if (IsMigrationDefault(refreshed))
        {
            CustomMessageBox.Show(
                Lang.MigratedRenewStillBlocked,
                Lang.MigratedRenewBlockedTitle,
                MsgType.Warning,
                System.Windows.Application.Current.MainWindow);
            return null;
        }

        return refreshed;
    }

    private static bool IsMigrationDefault(EmployeeDto e)
    {
        bool phoneIsDefault = !string.IsNullOrEmpty(e.Phone)
            && Regex.IsMatch(e.Phone, @"^MIG-\d+$", RegexOptions.IgnoreCase);
        bool subTypeIsDefault = string.Equals(e.SubscriptionType, "Migrated",
            StringComparison.OrdinalIgnoreCase);
        return phoneIsDefault || subTypeIsDefault;
    }

    [RelayCommand]
    private async Task RenewSubscriptionAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        // Silently swallow rapid double-click on the same player's Renew button —
        // Basmia's session.log showed multiple back-to-back renewal clicks creating
        // duplicate transactions.
        if (!TryClaimMutation(employee.Id)) return;
        var mutationKey = employee.Id;
        try
        {

        // Block renewal on still-migrated records. Migration sets sentinels (Phone="MIG-N",
        // SubscriptionType="Migrated") for records imported from the old DB without enough
        // info. Forcing the user to update via Edit dialog before renewal ensures the player
        // record actually reflects real data and matches what's in the device.
        employee = await EnsureMigrationDefaultsResolvedAsync(employee);
        if (employee == null) return;

        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();
        var doors = (await _doorService.GetAllDoorsAsync()).ToList();
        var dialog = new RenewSubscriptionDialog(employee, devices, _lookupService, doors);
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

        ActivityLogger.LogAction("Employees", "RenewSubscription", $"{employee.FullNameEn} type={dialog.SelectedSubscriptionType}");
        IsLoading = true;
        try
        {
            StatusMessage = Lang.RenewingSubscription + " (syncing to device, please wait...)";

            var success = await _employeeService.RenewSubscriptionAsync(
                employee.Id,
                dialog.SelectedSubscriptionType,
                dialog.SelectedMonths,
                dialog.SelectedCustomDays,
                dialog.NewFee,
                dialog.NewAmountPaid,
                dialog.DoorPermissions,
                dialog.EffectiveTimes,
                selectedDeviceIds.Count > 0 ? selectedDeviceIds : null,
                dialog.NewDiscount);

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
            if (errorMsg.Contains("PARTIAL_SUCCESS:"))
            {
                await LoadPagedAsync();
                CustomMessageBox.Show(
                    Lang.RenewSyncErrors,
                    Lang.RenewSubscription, MsgType.Warning,
                    System.Windows.Application.Current.MainWindow);
            }
            else
            {
                if (errorMsg.Contains("not found")) errorMsg = Lang.PlayerNotFoundDeleted;
                else if (errorMsg.Contains("Concurrency")) errorMsg = Lang.ConcurrencyError;
                else if (errorMsg.Contains("constraint")) errorMsg = Lang.DatabaseError;

                CustomMessageBox.Show(errorMsg, Lang.RenewSubscription, MsgType.Error,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        finally
        {
            IsLoading = false;
        }
        } // end of double-click try
        finally { ReleaseMutation(mutationKey); }
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
    private void PrintList()
    {
        if (Employees.Count == 0)
        {
            CustomMessageBox.Show(
                Lang.IsArabic ? "لا يوجد أعضاء للطباعة" : "No members to print.",
                Lang.NavPlayers, MsgType.Info, System.Windows.Application.Current.MainWindow);
            return;
        }
        try
        {
            MembersListPrinter.Print(new List<EmployeeDto>(Employees));
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.NavPlayers, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task PrintMemberCard(EmployeeDto? employee)
    {
        if (employee == null) return;
        // The ID card prints the photo — reload the full member (list DTOs omit the photo blob).
        employee = await _employeeService.GetEmployeeByIdAsync(employee.Id) ?? employee;
        var dialog = new MemberCardDialog(employee)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
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
    private async Task CreateMissingCardsAsync()
    {
        try
        {
            int missing = await _employeeService.CountActiveMembersMissingCardAsync();
            if (missing == 0)
            {
                CustomMessageBox.Show(
                    Lang.IsArabic ? "كل الأعضاء النشطين لديهم بطاقة فعّالة." : "All active members already have an active card.",
                    Lang.NavPlayers, MsgType.Info, System.Windows.Application.Current.MainWindow);
                return;
            }

            var title = Lang.IsArabic ? "إنشاء بطاقات للأعضاء النشطين" : "Create cards for active members";
            var confirmMsg = Lang.IsArabic
                ? $"يوجد {missing} عضو نشط بدون بطاقة فعّالة.\n\nسيتم إنشاء بطاقة لكل عضو لديه رقم بطاقة مخزّن (تُعلَّم \"غير مرفوعة للجهاز\").\nبعدها استخدم \"مزامنة كل اللاعبين للجهاز\" لدفعها للبوابة.\n\nهل تريد المتابعة؟"
                : $"{missing} active members have no active card.\n\nA card will be created for each one that has a stored card number (marked \"not on device\").\nThen use \"Sync all players to device\" to push them to the gate.\n\nProceed?";
            if (!CustomMessageBox.Confirm(confirmMsg, title, MsgType.Warning, System.Windows.Application.Current.MainWindow))
                return;

            IsLoading = true;
            var (created, skipped, total) = await _employeeService.CreateCardsForActiveMembersMissingCardAsync();
            IsLoading = false;

            await LoadPagedAsync();

            var resultMsg = Lang.IsArabic
                ? $"تم إنشاء {created} بطاقة." + (skipped > 0 ? $"\nتم تخطّي {skipped} عضو (بدون رقم بطاقة — عيّن لهم رقماً يدوياً)." : "")
                  + "\n\nالخطوة التالية: \"مزامنة كل اللاعبين للجهاز\" لدفعها للبوابة."
                : $"Created {created} cards." + (skipped > 0 ? $"\nSkipped {skipped} members (no card number — assign one manually)." : "")
                  + "\n\nNext step: \"Sync all players to device\" to push them to the gate.";
            CustomMessageBox.Show(resultMsg, title, MsgType.Info, System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            IsLoading = false;
            CustomMessageBox.Show(ex.Message, Lang.NavPlayers, MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SetFilterAsync(string indexStr)
    {
        if (!int.TryParse(indexStr, out var index)) return;
        SelectedFilterIndex = index;
        CurrentPage = 1;
        IsDataLoaded = true;
        await LoadPagedAsync();

        // Filter switching is the documented OOM trigger from the Basmia 2026-05-18
        // crash storm (~25 filter switches per minute leaked enough WPF Visual tree
        // refs to OOM after 3.5 days). Force a Gen2 collection here to release the
        // orphaned row containers the DataGrid holds across filter changes. Cheap
        // (~10-20 ms) and only on the relatively rare filter-change event, not on
        // page navigation within the same filter.
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);
        }
        catch { /* GC must never throw, but defensive */ }
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
            1 => (now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7), now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7).AddDays(7).AddSeconds(-1)),
            2 => (now.Date.AddDays(-(int)now.DayOfWeek - 7), now.Date.AddDays(-(int)now.DayOfWeek - 7).AddDays(7).AddSeconds(-1)),
            3 => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(-1)),
            4 => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1).AddSeconds(-1)),
            _ => (now.Date, now.Date.AddDays(1).AddSeconds(-1))
        };
    }

    [RelayCommand]
    private async Task BulkOperationAsync()
    {
        // Load devices for Upload All option
        var devices = (await _deviceService.GetAllDevicesAsync()).ToList();
        var dialog = new BulkOperationDialog(devices);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        // Handle Upload All to Device (operation 3)
        if (dialog.SelectedOperation == 3)
        {
            await UploadAllCardsToDevicesAsync(dialog.SelectedDeviceIds);
            return;
        }

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

    private async Task UploadAllCardsToDevicesAsync(List<int> deviceIds)
    {
        IsLoading = true;
        StatusMessage = Lang.BulkUploadProgress;
        try
        {
            var progress = new Progress<(int current, int total, string cardNumber)>(p =>
            {
                StatusMessage = $"{Lang.BulkUploadProgress} ({p.current}/{p.total}) - {p.cardNumber}";
            });

            var (uploaded, skipped, failed, total) = await Task.Run(() =>
                _employeeService.UploadAllCardsToDevicesAsync(deviceIds, progress));

            var msg = $"{Lang.BulkUploadComplete}\n\n" +
                      $"{Lang.BulkUploaded}: {uploaded}\n" +
                      $"{Lang.BulkSkipped}: {skipped}\n" +
                      $"{Lang.BulkFailedCount}: {failed}";

            StatusMessage = $"{Lang.BulkUploadComplete}: {uploaded} uploaded, {skipped} skipped, {failed} failed";
            CustomMessageBox.Show(msg, Lang.BulkOperations,
                failed > 0 ? MsgType.Warning : MsgType.Success,
                System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = Lang.BulkFailedCount;
            CustomMessageBox.Show($"{Lang.BulkFailedCount}: {ex.Message}", Lang.BulkOperations,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Best-effort per-filter counts (whole dataset). Reuses the existing paged methods with size=1 —
    // each returns the filter's full TotalCount from a fast indexed COUNT. Never blocks the list.
    private async Task LoadFilterCountsAsync(string? search)
    {
        try
        {
            var (from, to) = GetPeriodRange();
            AllCount = (await _employeeService.GetPagedAsync(1, 1, search)).TotalCount;
            ExpiringCount = (await _employeeService.GetFilteredPagedAsync(1, from, to, 1, 1, search)).TotalCount;
            RenewedCount  = (await _employeeService.GetFilteredPagedAsync(2, from, to, 1, 1, search)).TotalCount;
            FrozenCount   = (await _employeeService.GetFilteredPagedAsync(3, from, to, 1, 1, search)).TotalCount;
            ExpiredCount  = (await _employeeService.GetFilteredPagedAsync(4, from, to, 1, 1, search)).TotalCount;
            ActiveCount   = (await _employeeService.GetFilteredPagedAsync(5, from, to, 1, 1, search)).TotalCount;
        }
        catch { /* counts are a nicety — never fail the screen over them */ }
    }

    private async Task LoadPagedAsync()
    {
        ActivityLogger.LogAction("Employees", "LoadPage", $"page={CurrentPage} filter={SelectedFilterIndex} search={SearchText}");
        IsLoading = true;
        StatusMessage = Lang.Loading;
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
                // Card badges count the WHOLE dataset (not just this page) — otherwise "has card"
                // maxes out at the page size and "no card" reads 0 even when members lack cards.
                WithCardCount = await _employeeService.GetWithCardCountAsync(search);
                WithoutCardCount = totalCount - WithCardCount;

                Employees.Clear();
                foreach (var emp in list)
                    Employees.Add(emp);
            }
            else
            {
                // Filter mode — DB-level PAGED load so clicking a filter no longer pulls the whole
                // matching set at once (was slow on big gyms). CurrentPage is reset by the caller
                // (SetFilter / search change), not here, so the pager works across pages.
                var (from, to) = GetPeriodRange();
                var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
                var (items, totalCount, withCard) = await _employeeService.GetFilteredPagedAsync(
                    SelectedFilterIndex, from, to, CurrentPage, PageSize, search);
                var list = items.ToList();

                TotalCount = totalCount;
                TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
                WithCardCount = withCard;
                WithoutCardCount = totalCount - withCard;

                Employees.Clear();
                foreach (var emp in list)
                    Employees.Add(emp);
            }

            // Refresh the per-pill owner counts only when the context (filter/period/search/refresh
            // or a mutation) changed — those all reset to page 1; plain page navigation keeps them.
            if (CurrentPage == 1)
                await LoadFilterCountsAsync(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);
        }
        catch (Exception ex)
        {
            // Never surface the raw DB/SQL error to the user (information disclosure + confusing).
            // Log the technical detail; show a friendly, actionable message instead.
            ActivityLogger.LogAction("Employees", "LoadError", ex.Message);
            var msg = Lang.IsArabic
                ? "تعذّر تحميل قائمة اللاعبين. أعد المحاولة، وإذا استمرت المشكلة أعد تشغيل البرنامج أو تواصل مع الدعم."
                : "Could not load the players list. Please retry; if it persists, restart the app or contact support.";
            CustomMessageBox.Show(msg, Lang.NavPlayers, MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncPlayerToDeviceAsync(EmployeeDto? employee)
    {
        if (employee == null) return;

        if (string.IsNullOrWhiteSpace(employee.CardNo) && (employee.Cards == null || employee.Cards.Count == 0))
        {
            CustomMessageBox.Show(Lang.PlayerNoCard, Lang.SyncToDevice, MsgType.Warning,
                System.Windows.Application.Current.MainWindow);
            return;
        }

        IsLoading = true;
        StatusMessage = Lang.SyncToDevice + "...";
        try
        {
            // WiFi check
            if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
            {
                CustomMessageBox.Show(Lang.WifiWarning, Lang.SyncToDevice, MsgType.Warning,
                    System.Windows.Application.Current.MainWindow);
                return;
            }

            var devices = (await _deviceService.GetAllDevicesAsync()).ToList();
            if (devices.Count == 0)
            {
                CustomMessageBox.Show("No devices found.", Lang.SyncToDevice, MsgType.Warning,
                    System.Windows.Application.Current.MainWindow);
                return;
            }

            // Get updated employee with cards
            var updatedEmployee = await _employeeService.GetEmployeeByIdAsync(employee.Id);
            if (updatedEmployee == null) return;

            var activeCards = updatedEmployee.Cards?.Where(c => c.IsActive).ToList() ?? new();
            if (activeCards.Count == 0)
            {
                CustomMessageBox.Show(Lang.PlayerNoCard, Lang.SyncToDevice, MsgType.Warning,
                    System.Windows.Application.Current.MainWindow);
                return;
            }

            int totalSynced = 0, totalFailed = 0;
            var allErrors = new List<string>();

            foreach (var card in activeCards)
            {
                StatusMessage = $"Syncing card {card.CardNumber} to {devices.Count} device(s)...";
                var (synced, failed, total, errors) = await _employeeService.SyncCardToDevicesAsync(card.Id);
                totalSynced += synced;
                totalFailed += failed;
                allErrors.AddRange(errors);
                await Task.Delay(200);
            }

            if (totalFailed == 0 && totalSynced > 0)
            {
                StatusMessage = "✓ " + string.Format(Lang.PlayerSynced, totalSynced);
                CustomMessageBox.Show(string.Format(Lang.PlayerSynced, totalSynced),
                    Lang.SyncToDevice, MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else if (totalSynced > 0)
            {
                var msg = string.Format(Lang.PlayerSynced, totalSynced) + $"\n\nFailed: {totalFailed}\n" + string.Join("\n", allErrors);
                CustomMessageBox.Show(msg, Lang.SyncToDevice, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                var msg = Lang.PlayerSyncFailed + "\n\n" + string.Join("\n", allErrors);
                CustomMessageBox.Show(msg, Lang.SyncToDevice, MsgType.Error, System.Windows.Application.Current.MainWindow);
            }

            await LoadPagedAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.SyncToDevice, MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

}
