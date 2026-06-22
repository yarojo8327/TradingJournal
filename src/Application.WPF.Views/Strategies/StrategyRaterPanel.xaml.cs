using Application.WPF.ViewModels.Strategies;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Application.WPF.Views.Strategies;

public partial class StrategyRaterPanel : UserControl
{
    public StrategyRaterPanel() => InitializeComponent();

    /// <summary>
    /// Permite deseleccionar un chip ya seleccionado haciendo clic de nuevo en el mismo número.
    /// </summary>
    private void OnRatingClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox lb) return;
        if (lb.Tag is not StrategyRuleRatingItem item) return;

        var clicked = ItemsControl.ContainerFromElement(lb, e.OriginalSource as DependencyObject)
                      as ListBoxItem;

        if (clicked?.Content is int value && item.Rating == value)
        {
            item.Rating = null;
            lb.UnselectAll();
            e.Handled = true;
        }
    }
}
