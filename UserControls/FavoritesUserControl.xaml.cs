using System.Windows.Controls;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.UserControls
{
    /// <summary>
    /// FavoritesUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class FavoritesUserControl : UserControl
    {
        public FavoritesUserControl()
        {
            InitializeComponent();
            DataContext = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<FavoritesViewModel>();
        }
    }
} 