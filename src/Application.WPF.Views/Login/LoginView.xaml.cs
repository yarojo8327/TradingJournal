using Application.WPF.ViewModels.Login;
using System.Windows.Controls;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace Application.WPF.Views.Login;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
            oldVm.ErrorsChanged -= OnViewModelErrorsChanged;

        if (e.NewValue is LoginViewModel newVm)
            newVm.ErrorsChanged += OnViewModelErrorsChanged;
    }

    private void OnViewModelErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e)
    {
        if (sender is not LoginViewModel vm) return;

        if (e.PropertyName == nameof(LoginViewModel.Password))
            PasswordErrors.ItemsSource = vm.GetErrors(nameof(LoginViewModel.Password))
                .Cast<DataAnnotationsValidationResult>()
                .Select(r => r.ErrorMessage);
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }
}
