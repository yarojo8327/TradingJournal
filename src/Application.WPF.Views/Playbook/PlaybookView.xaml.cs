using Application.WPF.ViewModels.Playbook;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Application.WPF.Views.Playbook;

public partial class PlaybookView : UserControl
{
    private PlaybookFormWindow?   _formWindow;
    private PlaybookViewerWindow? _viewerWindow;

    public PlaybookView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PlaybookViewModel old)
            old.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is PlaybookViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not PlaybookViewModel vm) return;

        // Abrir/cerrar ventana de formulario (crear/editar)
        if (e.PropertyName == nameof(PlaybookViewModel.IsFormVisible))
        {
            if (vm.IsFormVisible)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var owner = Window.GetWindow(this);
                    _formWindow = new PlaybookFormWindow(vm) { Owner = owner };
                    _formWindow.ShowDialog();
                    _formWindow = null;
                });
            }
            else
            {
                _formWindow?.Close();
            }
        }

        // Abrir/cerrar ventana de visualización (solo lectura)
        if (e.PropertyName == nameof(PlaybookViewModel.ViewingEntry))
        {
            if (vm.ViewingEntry is not null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var owner = Window.GetWindow(this);
                    _viewerWindow = new PlaybookViewerWindow(vm.ViewingEntry)
                    {
                        Owner = owner
                    };
                    _viewerWindow.Closed += (_, _) => vm.CloseViewerCommand.Execute(null);
                    _viewerWindow.ShowDialog();
                    _viewerWindow = null;
                });
            }
            else
            {
                _viewerWindow?.Close();
            }
        }
    }
}
