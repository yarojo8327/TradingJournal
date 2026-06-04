using Application.WPF.ViewModels.Main;

namespace Application.WPF.Views.Main;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
