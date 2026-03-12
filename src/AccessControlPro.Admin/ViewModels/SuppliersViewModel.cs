using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class SuppliersViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private bool _isEditing;

    // Edit fields
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editContactPerson = "";

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public SuppliersViewModel(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var suppliers = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            foreach (var s in suppliers)
                Suppliers.Add(s);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        if (value != null)
        {
            EditName = value.Name;
            EditPhone = value.Phone;
            EditAddress = value.Address;
            EditContactPerson = value.ContactPerson;
            IsEditing = true;
        }
        else
        {
            ClearFields();
        }
    }

    private void ClearFields()
    {
        EditName = "";
        EditPhone = "";
        EditAddress = "";
        EditContactPerson = "";
        SelectedSupplier = null;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            CustomMessageBox.Show(Lang.PlayerNameRequired, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        try
        {
            if (IsEditing && SelectedSupplier != null)
            {
                SelectedSupplier.Name = EditName.Trim();
                SelectedSupplier.Phone = EditPhone.Trim();
                SelectedSupplier.Address = EditAddress.Trim();
                SelectedSupplier.ContactPerson = EditContactPerson.Trim();
                await _supplierService.UpdateAsync(SelectedSupplier);
            }
            else
            {
                await _supplierService.AddAsync(new SupplierDto
                {
                    Name = EditName.Trim(),
                    Phone = EditPhone.Trim(),
                    Address = EditAddress.Trim(),
                    ContactPerson = EditContactPerson.Trim()
                });
            }

            ClearFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSupplier == null) return;

        if (!CustomMessageBox.Confirm(
            Lang.IsArabic ? $"هل تريد حذف \"{SelectedSupplier.Name}\"?" : $"Delete \"{SelectedSupplier.Name}\"?",
            Lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            await _supplierService.DeleteAsync(SelectedSupplier.Id);
            ClearFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearFields();
    }
}
