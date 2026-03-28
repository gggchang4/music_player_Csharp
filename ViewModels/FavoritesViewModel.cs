using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using RelayCommand = GalaSoft.MvvmLight.Command.RelayCommand;
using System.Windows;

namespace MusicPlayerApp.ViewModels
{
    public class FavoritesViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _mediaLibraryService;
        private readonly MediaPlayerService _mediaPlayerService;
        private readonly UserService _userService;

        private ObservableCollection<Song> _favoriteSongs;
        public ObservableCollection<Song> FavoriteSongs
        {
            get => _favoriteSongs;
            set => Set(ref _favoriteSongs, value);
        }

        private Song _selectedSong;
        public Song SelectedSong
        {
            get => _selectedSong;
            set => Set(ref _selectedSong, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        // 命令
        public ICommand PlaySongCommand { get; private set; }
        public ICommand RemoveFavoriteCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public FavoritesViewModel(MediaLibraryService mediaLibraryService, MediaPlayerService mediaPlayerService, UserService userService)
        {
            _mediaLibraryService = mediaLibraryService;
            _mediaPlayerService = mediaPlayerService;
            _userService = userService;

            FavoriteSongs = new ObservableCollection<Song>();

            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            RemoveFavoriteCommand = new RelayCommand<Song>(RemoveFavorite);
            RefreshCommand = new RelayCommand(async () => { await LoadFavoriteSongsAsync(); });

            // 注册消息
            Messenger.Default.Register<NotificationMessage>(this, HandleMessage);

            // 立即在UI线程加载收藏，避免异步加载导致的问题
            App.Current.Dispatcher.BeginInvoke(new Action(async () => 
            {
                try
                {
                    App.Logger.Info("FavoritesViewModel构造函数中开始加载数据");
                    await LoadFavoriteSongsAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "FavoritesViewModel初始化加载失败");
                }
            }));
        }

        private void HandleMessage(NotificationMessage message)
        {
            // 处理收藏变更消息
            if (message.Notification == "FavoritesChanged")
            {
                Task.Run(async () => await LoadFavoriteSongsAsync());
            }
        }

        private void PlaySong(Song song)
        {
            if (song == null)
                return;

            _mediaPlayerService.PlaySong(song);
        }

        private async void RemoveFavorite(Song song)
        {
            if (song == null)
                return;

            try
            {
                App.Logger.Info($"正在移除收藏歌曲: {song.Title} (ID: {song.Id})");
                
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    App.Logger.Warn("移除收藏歌曲失败: 无登录用户");
                    return;
                }

                // 从界面上先移除歌曲，使UI响应更快
                App.Current.Dispatcher.Invoke(() =>
                {
                    FavoriteSongs.Remove(song);
                    OnPropertyChanged(nameof(FavoriteSongs));
                });

                // 后台执行移除操作
                await _mediaLibraryService.RemoveFromFavoritesAsync(currentUser.Id, song.Id);
                
                App.Logger.Info($"成功移除收藏歌曲: {song.Title} (ID: {song.Id})");
                
                // 通知其他视图收藏状态已更改
                Messenger.Default.Send(new NotificationMessage("FavoritesChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"移除收藏歌曲失败: {song.Title} (ID: {song.Id})");
                
                // 在失败时刷新列表，确保UI显示正确状态
                try
                {
                    await Task.Delay(500); // 短暂延迟以避免立即再次请求数据库
                    await LoadFavoriteSongsAsync();
                }
                catch { /* 忽略刷新错误 */ }
                
                MessageBox.Show($"移除收藏失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFavoriteSongsAsync()
        {
            // 如果已经在加载中，则不重复加载
            if (IsLoading)
            {
                App.Logger.Info("已经有数据加载过程在进行中，跳过重复请求");
                return;
            }
            
            try
            {
                IsLoading = true;
                App.Logger.Info("开始加载收藏歌曲数据");
                
                // 获取用户ID
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    App.Logger.Warn("加载收藏歌曲失败：当前无登录用户");
                    return;
                }
                
                // 获取收藏歌曲
                var songs = await _mediaLibraryService.GetFavoriteSongsAsync(currentUser.Id);
                
                // 在UI线程更新数据
                App.Current.Dispatcher.Invoke(() =>
                {
                    FavoriteSongs.Clear();
                    foreach (var song in songs)
                    {
                        FavoriteSongs.Add(song);
                    }
                    
                    // 通知UI更新
                    OnPropertyChanged(nameof(FavoriteSongs));
                });
                
                // 记录结果
                App.Logger.Info(songs.Count > 0 
                    ? $"成功加载{songs.Count}首收藏歌曲" 
                    : "未找到收藏歌曲");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载收藏歌曲失败");
                
                // 仅在UI线程上显示错误消息
                App.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"加载收藏歌曲失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 刷新收藏列表
        public void Refresh()
        {
            try
            {
                App.Logger.Info("手动刷新收藏列表");
                Task.Run(async () => await LoadFavoriteSongsAsync());
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "刷新收藏列表失败");
            }
        }

        // 清理
        public void Cleanup()
        {
            Messenger.Default.Unregister(this);
            base.Cleanup();
        }
    }
} 