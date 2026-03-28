using System;
using System.Windows.Controls;
using MusicPlayerApp.Models;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    public partial class PlaylistContentUserControl : UserControl
    {
        public PlaylistContentUserControl()
        {
            InitializeComponent();
        }
        
        public PlaylistContentUserControl(Playlist playlist) : this()
        {
            try
            {
                if (playlist == null)
                {
                    App.Logger.Error("PlaylistContentUserControl初始化失败：播放列表为null");
                    return;
                }
                
                // 创建并设置ViewModel
                var playlistViewModel = new PlaylistContentViewModel(playlist);
                DataContext = playlistViewModel;
                
                // 加载播放列表内容
                playlistViewModel.LoadPlaylistContentAsync();
                
                App.Logger.Info($"PlaylistContentUserControl初始化成功: {playlist.Title}");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "初始化PlaylistContentUserControl时发生错误");
            }
        }
        
        public void Refresh()
        {
            try
            {
                var viewModel = DataContext as PlaylistContentViewModel;
                viewModel?.RefreshCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "刷新PlaylistContentUserControl失败");
            }
        }
    }
} 