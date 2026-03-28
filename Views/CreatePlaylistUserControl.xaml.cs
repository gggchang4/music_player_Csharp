using System;
using System.Windows.Controls;
using MusicPlayerApp.Services;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    public partial class CreatePlaylistUserControl : UserControl
    {
        public CreatePlaylistUserControl()
        {
            InitializeComponent();
            
            // 设置DataContext为CreatePlaylistViewModel
            try
            {
                DataContext = ServiceLocator.Instance.GetService<CreatePlaylistViewModel>();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "初始化CreatePlaylistViewModel时出错");
                // 如果获取ViewModel失败，创建一个新的实例
                var libraryService = ServiceLocator.Instance.GetService<MediaLibraryService>();
                var userService = ServiceLocator.Instance.GetService<UserService>();
                
                DataContext = new CreatePlaylistViewModel(libraryService, userService);
            }
        }
    }
} 