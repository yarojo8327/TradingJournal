using Application.WPF.ViewModels.Evaluator;
using Application.WPF.Views.LotCalculator;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Application.WPF.Views.Evaluator;

public partial class TradingEvaluatorView : UserControl
{
    private LotCalculatorWindow? _lotCalculatorWindow;

    public TradingEvaluatorView() => InitializeComponent();

    /// <summary>Permite alternar la regla haciendo clic en cualquier parte de la tarjeta, no solo en el checkbox.</summary>
    private void OnRuleCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StrategyRuleChecklistItem item }) return;
        item.IsChecked = !item.IsChecked;
    }

    private void OnOpenLotCalculator(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TradingEvaluatorViewModel vm) return;

        if (_lotCalculatorWindow is not null)
        {
            _lotCalculatorWindow.Activate();
            return;
        }

        _lotCalculatorWindow = new LotCalculatorWindow(vm.LotCalculator)
        {
            Owner = Window.GetWindow(this)
        };
        _lotCalculatorWindow.Closed += (_, _) => _lotCalculatorWindow = null;
        _lotCalculatorWindow.Show();
    }
}
