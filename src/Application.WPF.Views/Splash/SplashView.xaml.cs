using Application.WPF.ViewModels.Splash;

namespace Application.WPF.Views.Splash;

public partial class SplashView : System.Windows.Window
{
    public SplashView(SplashViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
