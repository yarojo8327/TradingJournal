using Application.WPF.ViewModels.Journal;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Application.WPF.Views.Journal;

public partial class TradeFormWindow : Window
{
    private readonly TradeJournalViewModel _vm;
    private bool _suppressTimeChanged;

    public TradeFormWindow(TradeJournalViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TradeJournalViewModel.IsFormVisible) && !_vm.IsFormVisible)
            Dispatcher.InvokeAsync(Close);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_vm.IsFormVisible)
            _vm.CancelCommand.Execute(null);
        base.OnClosing(e);
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    // ── Time auto-format HH:mm ────────────────────────────────────────────

    private void OnTimePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Only allow digits
        e.Handled = !char.IsDigit(e.Text, 0);
    }

    private void OnEntryTimeTextChanged(object sender, TextChangedEventArgs e)
        => AutoFormatTime(EntryTimeBox);

    private void OnExitTimeTextChanged(object sender, TextChangedEventArgs e)
        => AutoFormatTime(ExitTimeBox);

    private void AutoFormatTime(TextBox box)
    {
        if (_suppressTimeChanged) return;
        _suppressTimeChanged = true;
        try
        {
            var digits = new string(box.Text.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) { box.Text = string.Empty; return; }

            string formatted;
            if (digits.Length <= 2)
            {
                formatted = digits;
            }
            else
            {
                var hh = digits[..2];
                var mm = digits.Length >= 4 ? digits[2..4] : digits[2..];
                formatted = $"{hh}:{mm}";
            }

            var caret = box.CaretIndex;
            box.Text = formatted;
            // Place caret after the colon when it was just inserted
            box.CaretIndex = Math.Min(formatted.Length, caret + (formatted.Length > box.Text.Length ? 1 : 0));
            box.CaretIndex = formatted.Length;
        }
        finally
        {
            _suppressTimeChanged = false;
        }
    }
}
