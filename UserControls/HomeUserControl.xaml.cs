using System.Windows.Controls;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.UserControls
{
    /// <summary>
    /// HomeUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class HomeUserControl : UserControl
    {
        public HomeUserControl()
        {
            InitializeComponent();
            DataContext = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<HomeViewModel>();
        }
    }
} 