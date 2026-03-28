using System.Windows.Controls;
using MusicPlayerApp.Services;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    /// <summary>
    /// ArtistsUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ArtistsUserControl : UserControl
    {
        private ArtistsViewModel _viewModel;

        public ArtistsUserControl()
        {
            InitializeComponent();
            
            // 获取必要的服务
            var libraryService = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaLibraryService>();
            var playerService = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaPlayerService>();
            
            // 创建并设置ViewModel
            _viewModel = new ArtistsViewModel(libraryService, playerService);
            DataContext = _viewModel;
        }
        
        // 提供刷新方法供主窗口调用
        public void Refresh()
        {
            _viewModel?.Refresh();
        }
    }
} 