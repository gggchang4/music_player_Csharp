using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using MusicPlayerApp.ViewModels;


namespace MusicPlayerApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 设置数据上下文
            DataContext = ServiceLocator.Instance.GetService<MainViewModel>();

            // 设置窗口关闭事件
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 释放媒体播放器资源
                await MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaPlayerService>().DisposeAsync();

                App.Logger.Info("应用程序正常关闭");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "关闭应用程序时出错");
            }
        }
        
        /// <summary>
        /// 处理播放列表鼠标点击事件，确保即使已选中的项也能响应点击
        /// </summary>
        private void PlaylistsListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 获取点击的列表项元素
                var item = ItemsControl.ContainerFromElement(PlaylistsListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (item != null)
                {
                    // 获取点击的播放列表
                    var playlist = item.DataContext as Playlist;
                    if (playlist != null)
                    {
                        // 获取视图模型
                        var viewModel = DataContext as MainViewModel;
                        if (viewModel != null)
                        {
                            // 确保即使是相同的项也强制触发导航
                            viewModel.ForceNavigateToPlaylist(playlist);
                            
                            // 标记事件已处理
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "处理播放列表点击事件失败");
            }
        }
    }
}