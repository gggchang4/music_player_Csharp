using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicPlayerApp.Services;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    public partial class AllMusicUserControl : UserControl
    {
        private AllMusicViewModel _viewModel;

        public AllMusicUserControl()
        {
            InitializeComponent();

            // 初始化视图模型
            DataContext = new AllMusicViewModel(
                MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaLibraryService>(),
                MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaPlayerService>());
                
            // 监听DataContext变化
            DataContextChanged += AllMusicUserControl_DataContextChanged;
            Loaded += AllMusicUserControl_Loaded;
            
            // 在构造函数中强制使EmptyLibraryMessage暂时不可见，等待实际数据加载完成后再决定是否显示
            EmptyLibraryMessage.Visibility = Visibility.Collapsed;
        }
        
        private void AllMusicUserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 控件加载完成后，主动调用更新UI
            UpdateUIVisibility();
            
            // 额外检查，确保在有歌曲的情况下隐藏空库提示
            Dispatcher.BeginInvoke(new Action(() => {
                if (_viewModel != null && _viewModel.Songs != null && _viewModel.Songs.Count > 0)
                {
                    EmptyLibraryMessage.Visibility = Visibility.Collapsed;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void AllMusicUserControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // 解除旧的视图模型事件绑定
            if (_viewModel != null && _viewModel.Songs is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= Songs_CollectionChanged;
            }

            // 设置新的视图模型和事件绑定
            _viewModel = DataContext as AllMusicViewModel;
            if (_viewModel != null && _viewModel.Songs is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += Songs_CollectionChanged;
                UpdateUIVisibility();
            }
        }

        private void Songs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateUIVisibility();
        }

        private void UpdateUIVisibility()
        {
            if (_viewModel == null) return;

            // 在UI线程上更新UI元素
            Dispatcher.Invoke(() =>
            {
                bool hasSongs = _viewModel.Songs != null && _viewModel.Songs.Count > 0;
                bool isLoading = _viewModel.IsLoading;
                
                // 更新空库提示的可见性
                EmptyLibraryMessage.Visibility = (!hasSongs && !isLoading) 
                    ? System.Windows.Visibility.Visible 
                    : System.Windows.Visibility.Collapsed;
                    
                // 强制更新布局以确保变更生效
                EmptyLibraryMessage.UpdateLayout();
                SongsDataGrid.UpdateLayout();
            });
        }
        
        // 提供给外部调用的刷新方法
        public void Refresh()
        {
            if (_viewModel != null && _viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
            }
        }
        
        // 收藏按钮的点击处理程序
        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Info("收藏按钮被点击");
            // 注意：这个方法主要用于记录日志，实际的命令执行依赖于XAML绑定
            // 由于按钮点击会触发Command绑定，这里不需要再次执行命令
            
            // 确保事件不被路由到父元素
            e.Handled = true;
        }
    }
}