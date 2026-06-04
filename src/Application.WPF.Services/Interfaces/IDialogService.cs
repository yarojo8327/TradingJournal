namespace Application.WPF.Services.Interfaces;

public interface IDialogService
{
    void ShowError(string message, string title = "Error");
    void ShowWarning(string message, string title = "Warning");
    void ShowInformation(string message, string title = "Information");
    bool ShowConfirmation(string message, string title = "Confirm");
    string? ShowOpenFileDialog(string filter = "All Files (*.*)|*.*", string? initialDirectory = null);
    string? ShowSaveFileDialog(string filter = "All Files (*.*)|*.*", string? defaultFileName = null);
}
