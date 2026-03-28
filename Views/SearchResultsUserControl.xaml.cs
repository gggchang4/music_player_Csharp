using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicPlayerApp.Services;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    public partial class SearchResultsUserControl : UserControl
    {
        private SearchResultsViewModel _viewModel;

        public SearchResultsUserControl(string searchQuery = "")
        {
            InitializeComponent();
            
            // 获取服务
            var libraryService = ServiceLocator.Instance.GetService<MediaLibraryService>();
            var playerService = ServiceLocator.Instance.GetService<MediaPlayerService>();
            var userService = ServiceLocator.Instance.GetService<UserService>();
            
            // 创建视图模型
            _viewModel = new SearchResultsViewModel(searchQuery, libraryService, playerService, userService);
            
            // 设置数据上下文
            DataContext = _viewModel;
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel != null)
            {
                _viewModel.ExecuteSearch();
                e.Handled = true;
            }
        }
    }
} 