using Application.WPF.ViewModels.Journal;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Application.WPF.Views.Journal;

public partial class TradeJournalView : UserControl
{
    private TradeFormWindow? _formWindow;

    public TradeJournalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TradeJournalViewModel old)
            old.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is TradeJournalViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TradeJournalViewModel.IsFormVisible)) return;
        if (DataContext is not TradeJournalViewModel vm) return;

        if (vm.IsFormVisible)
        {
            // Diferir para que la UI del grid se actualice antes de abrir el diálogo
            Dispatcher.InvokeAsync(() =>
            {
                var owner = Window.GetWindow(this);
                _formWindow = new TradeFormWindow(vm) { Owner = owner };
                _formWindow.ShowDialog();
                _formWindow = null;
            });
        }
        else
        {
            _formWindow?.Close();
        }
    }
}
