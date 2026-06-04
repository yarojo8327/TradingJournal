using Application.WPF.ViewModels.Register;
using System.Windows.Controls;

namespace Application.WPF.Views.Register;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RegisterViewModel oldVm)
            oldVm.ErrorsChanged -= OnViewModelErrorsChanged;

        if (e.NewValue is RegisterViewModel newVm)
            newVm.ErrorsChanged += OnViewModelErrorsChanged;
    }

    private void OnViewModelErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e)
    {
        if (sender is not RegisterViewModel vm) return;

        if (e.PropertyName == nameof(RegisterViewModel.Password))
            PasswordErrors.ItemsSource = vm.GetErrors(nameof(RegisterViewModel.Password))
                .Cast<System.ComponentModel.DataAnnotations.ValidationResult>()
                .Select(r => r.ErrorMessage);

        if (e.PropertyName == nameof(RegisterViewModel.ConfirmPassword))
            ConfirmPasswordErrors.ItemsSource = vm.GetErrors(nameof(RegisterViewModel.ConfirmPassword))
                .Cast<System.ComponentModel.DataAnnotations.ValidationResult>()
                .Select(r => r.ErrorMessage);
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm)
            vm.ConfirmPassword = ((PasswordBox)sender).Password;
    }
}
