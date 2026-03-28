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
using System.Collections.Generic;

namespace MusicPlayerApp.ViewModels
{
    public class FavoriteAlbumsViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _mediaLibraryService;
        private readonly MediaPlayerService _mediaPlayerService;
        private readonly UserService _userService;

        private ObservableCollection<Album> _favoriteAlbums;
        public ObservableCollection<Album> FavoriteAlbums
        {
            get => _favoriteAlbums;
            set 
            {
                if (Set(ref _favoriteAlbums, value))
                {
                    // 当FavoriteAlbums集合变化时，更新状态属性
                    OnPropertyChanged(nameof(HasFavorites));
                    OnPropertyChanged(nameof(HasNoFavorites));
                }
            }
        }

        // 判断是否有收藏专辑的属性
        public bool HasFavorites => FavoriteAlbums != null && FavoriteAlbums.Count > 0 && !IsLoading;
        
        // 判断是否没有收藏专辑的属性
        public bool HasNoFavorites => FavoriteAlbums != null && FavoriteAlbums.Count == 0 && !IsLoading;

        private Album _selectedAlbum;
        public Album SelectedAlbum
        {
            get => _selectedAlbum;
            set => Set(ref _selectedAlbum, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                if (Set(ref _isLoading, value))
                {
                    // 当加载状态变化时，更新UI状态属性
                    OnPropertyChanged(nameof(HasFavorites));
                    OnPropertyChanged(nameof(HasNoFavorites));
                }
            }
        }

        // 命令
        public ICommand PlayAlbumCommand { get; private set; }
        public ICommand RemoveFavoriteCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public FavoriteAlbumsViewModel(MediaLibraryService mediaLibraryService, MediaPlayerService mediaPlayerService, UserService userService)
        {
            App.Logger.Info("正在初始化FavoriteAlbumsViewModel");
            
            _mediaLibraryService = mediaLibraryService;
            _mediaPlayerService = mediaPlayerService;
            _userService = userService;

            FavoriteAlbums = new ObservableCollection<Album>();

            // 初始化命令
            PlayAlbumCommand = new RelayCommand<Album>(PlayAlbum);
            RemoveFavoriteCommand = new RelayCommand<Album>(RemoveFavorite);
            RefreshCommand = new RelayCommand(async () => { await LoadFavoriteAlbumsAsync(); });

            // 注册消息，使用弱引用避免内存泄漏
            Messenger.Default.Register<NotificationMessage>(this, HandleMessage);
            
            App.Logger.Info("FavoriteAlbumsViewModel初始化完成，正在加载数据");

            // 通过Dispatcher安全地加载数据
            App.Current.Dispatcher.InvokeAsync(async () => {
                try {
                    await LoadFavoriteAlbumsAsync();
                }
                catch (Exception ex) {
                    App.Logger.Error(ex, "初始化时加载收藏专辑失败");
                }
            });
        }

        private void HandleMessage(NotificationMessage message)
        {
            // 处理收藏变更消息
            if (message.Notification == "FavoriteAlbumsChanged")
            {
                Task.Run(async () => await LoadFavoriteAlbumsAsync());
            }
        }

        private async void PlayAlbum(Album album)
        {
            if (album == null)
                return;

            try
            {
                // 获取专辑中的所有歌曲
                var songs = await _mediaLibraryService.GetAlbumSongsAsync(album.Id);
                if (songs.Count > 0)
                {
                    // 设置当前播放列表并播放第一首歌曲
                    _mediaPlayerService.SetPlaylist(songs);
                    await _mediaPlayerService.PlayAsync(songs[0]);
                }
                else
                {
                    App.Logger.Warn($"专辑 '{album.Title}' 没有可播放的歌曲");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放专辑失败");
            }
        }

        private async void RemoveFavorite(Album album)
        {
            if (album == null)
                return;

            var currentUser = _userService.CurrentUser;
            if (currentUser == null)
                return;

            try
            {
                await _mediaLibraryService.RemoveFromFavoriteAlbumsAsync(currentUser.Id, album.Id);
                FavoriteAlbums.Remove(album);
                // 通知其他视图收藏已更改
                Messenger.Default.Send(new NotificationMessage("FavoriteAlbumsChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "从收藏中移除专辑失败");
            }
        }

        private async Task LoadFavoriteAlbumsAsync()
        {
            // 检查是否在UI线程上，如果不是则切换到UI线程
            if (!App.Current.Dispatcher.CheckAccess())
            {
                await App.Current.Dispatcher.InvokeAsync(async () => await LoadFavoriteAlbumsAsync());
                return;
            }

            App.Logger.Info("开始加载收藏专辑...");
            var currentUser = _userService.CurrentUser;
            if (currentUser == null)
            {
                App.Logger.Warn("无法加载收藏专辑：当前用户为空");
                try 
                {
                    // 尝试自动登录
                    currentUser = await _userService.LoginAsync("DefaultUser", "password");
                    App.Logger.Info($"已自动登录用户: {currentUser?.Username}");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "自动登录失败，无法加载收藏专辑");
                    IsLoading = false;
                    return;
                }
            }

            // 设置正在加载标志
            IsLoading = true;
            App.Logger.Info($"开始加载用户 {currentUser.Username}(ID={currentUser.Id}) 的收藏专辑");

            try
            {
                // 避免在UI线程执行可能阻塞的数据库操作
                List<Album> favorites = null;
                
                // 在后台线程加载数据
                await Task.Run(async () => {
                    try {
                        favorites = await _mediaLibraryService.GetFavoriteAlbumsAsync(currentUser.Id);
                        App.Logger.Info($"获取到收藏专辑数量: {favorites.Count}");
                    }
                    catch (Exception ex) {
                        App.Logger.Error(ex, "在后台线程加载收藏专辑失败");
                        throw; // 重新抛出异常以便外部捕获
                    }
                });
                
                // 安全地在UI线程上更新ObservableCollection
                if (favorites != null)
                {
                    // 确保在UI线程上更新集合
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        FavoriteAlbums.Clear();
                        
                        // 检查favorites列表是否为null，防止空引用异常
                        if (favorites != null)
                        {
                            foreach (var album in favorites)
                            {
                                try
                                {
                                    FavoriteAlbums.Add(album);
                                    App.Logger.Debug($"添加专辑到UI: {album.Title}");
                                }
                                catch (Exception ex)
                                {
                                    // 记录单个专辑添加失败，但继续执行
                                    App.Logger.Error(ex, $"添加专辑[{album.Id}]到UI失败");
                                }
                            }
                        }
                        
                        // 强制通知UI刷新
                        OnPropertyChanged(nameof(FavoriteAlbums));
                        OnPropertyChanged(nameof(HasFavorites));
                        OnPropertyChanged(nameof(HasNoFavorites));
                    });
                }
                
                App.Logger.Info($"收藏专辑加载完成，共 {FavoriteAlbums.Count} 张专辑");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载收藏专辑失败");
                
                // 在UI线程显示错误消息
                App.Current.Dispatcher.Invoke(() => 
                {
                    try
                    {
                        MessageBox.Show($"加载收藏专辑失败: {ex.Message}\n\n详细信息: {ex.InnerException?.Message}", 
                            "加载错误", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                    }
                    catch
                    {
                        // 忽略在显示消息框时可能发生的异常
                    }
                });
            }
            finally
            {
                // 确保设置IsLoading在UI线程上执行
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    
                    // 确保UI状态属性是最新的
                    OnPropertyChanged(nameof(HasFavorites));
                    OnPropertyChanged(nameof(HasNoFavorites));
                    App.Logger.Info("收藏专辑加载状态复位完成");
                });
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