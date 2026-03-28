using System.Windows.Controls;
using MusicPlayerApp.ViewModels;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.UserControls
{
    /// <summary>
    /// FavoriteAlbumsUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class FavoriteAlbumsUserControl : UserControl
    {
        public FavoriteAlbumsUserControl()
        {
            InitializeComponent();
            
            // 不再在这里创建ViewModel，等待外部设置DataContext
            // 这样可以避免在XAML加载和数据绑定过程中可能发生的异常
            App.Logger.Info("FavoriteAlbumsUserControl已初始化");
        }
    }
} 