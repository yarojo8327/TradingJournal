using Application.WPF.Services.Interfaces;
using Microsoft.Win32;
using System.Windows;

namespace Application.WPF.Services.Dialog;

public class DialogService : IDialogService
{
    public void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowWarning(string message, string title = "Warning") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowInformation(string message, string title = "Information") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool ShowConfirmation(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? ShowOpenFileDialog(string filter = "All Files (*.*)|*.*", string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter = "All Files (*.*)|*.*", string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName ?? string.Empty };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
