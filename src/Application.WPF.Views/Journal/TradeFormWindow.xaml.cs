using Application.WPF.ViewModels.Journal;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Application.WPF.Views.Journal;

public partial class TradeFormWindow : Window
{
    private readonly TradeJournalViewModel _vm;

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
        // Si el usuario cierra la ventana con la X sin guardar, ejecuta Cancel
        if (_vm.IsFormVisible)
            _vm.CancelCommand.Execute(null);
        base.OnClosing(e);
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
