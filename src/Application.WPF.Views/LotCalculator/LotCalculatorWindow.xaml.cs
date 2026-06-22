using Application.WPF.ViewModels.LotCalculator;
using System.Windows;
using System.Windows.Input;

namespace Application.WPF.Views.LotCalculator;

public partial class LotCalculatorWindow : Window
{
    public LotCalculatorWindow(LotCalculatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
