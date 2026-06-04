using Application.WPF.ViewModels.Profile;
using System.ComponentModel;
using System.Windows.Controls;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace Application.WPF.Views.Profile;

public partial class ProfileView : UserControl
{
    public ProfileView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProfileViewModel oldVm)
            oldVm.ErrorsChanged -= OnErrorsChanged;

        if (e.NewValue is ProfileViewModel newVm)
            newVm.ErrorsChanged += OnErrorsChanged;
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        if (sender is not ProfileViewModel vm) return;

        if (e.PropertyName == nameof(ProfileViewModel.NewPassword))
            NewPasswordErrors.ItemsSource = vm.GetErrors(nameof(ProfileViewModel.NewPassword))
                .Cast<DataAnnotationsValidationResult>().Select(r => r.ErrorMessage);

        if (e.PropertyName == nameof(ProfileViewModel.ConfirmNewPassword))
            ConfirmNewPasswordErrors.ItemsSource = vm.GetErrors(nameof(ProfileViewModel.ConfirmNewPassword))
                .Cast<DataAnnotationsValidationResult>().Select(r => r.ErrorMessage);
    }

    private void CurrentPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
            vm.CurrentPassword = ((PasswordBox)sender).Password;
    }

    private void NewPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
            vm.NewPassword = ((PasswordBox)sender).Password;
    }

    private void ConfirmNewPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
            vm.ConfirmNewPassword = ((PasswordBox)sender).Password;
    }
}
