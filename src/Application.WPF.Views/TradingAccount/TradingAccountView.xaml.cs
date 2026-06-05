using Application.WPF.ViewModels.TradingAccount;
using System.Windows.Controls;

namespace Application.WPF.Views.TradingAccount;

public partial class TradingAccountView : UserControl
{
    public TradingAccountView()
    {
        InitializeComponent();
    }

    public TradingAccountView(TradingAccountViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
