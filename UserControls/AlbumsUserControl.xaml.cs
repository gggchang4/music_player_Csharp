using System.Windows.Controls;
using MusicPlayerApp.ViewModels;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.UserControls
{
    /// <summary>
    /// AlbumsUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlbumsUserControl : UserControl
    {
        public AlbumsUserControl()
        {
            InitializeComponent();
            DataContext = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<AlbumsViewModel>();
        }
    }
} 