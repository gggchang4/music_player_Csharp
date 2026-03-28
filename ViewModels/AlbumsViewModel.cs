using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

namespace MusicPlayerApp.ViewModels
{
    public class AlbumsViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        private readonly UserService _userService;
        private Dictionary<int, bool> _favoriteStatus = new Dictionary<int, bool>();
        
        private ObservableCollection<Album> _albums;
        private Album _selectedAlbum;
        private ObservableCollection<Song> _albumSongs;
        private bool _isLoading;
        private bool _isAlbumDetailVisible;
        
        public ObservableCollection<Album> Albums
        {
            get => _albums;
            set => Set(ref _albums, value);
        }
        
        public Album SelectedAlbum
        {
            get => _selectedAlbum;
            set
            {
                if (Set(ref _selectedAlbum, value) && value != null)
                {
                    LoadAlbumSongs(value.Id);
                    IsAlbumDetailVisible = true;
                }
            }
        }
        
        public ObservableCollection<Song> AlbumSongs
        {
            get => _albumSongs;
            set => Set(ref _albumSongs, value);
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
        
        public bool IsAlbumDetailVisible
        {
            get => _isAlbumDetailVisible;
            set => Set(ref _isAlbumDetailVisible, value);
        }
        
        // 命令
        public ICommand RefreshCommand { get; }
        public ICommand PlaySongCommand { get; }
        public ICommand PlayAllSongsCommand { get; }
        public ICommand BackToAlbumsCommand { get; }
        public ICommand SelectAlbumCommand { get; }
        public ICommand ToggleFavoriteAlbumCommand { get; }
        
        public AlbumsViewModel(MediaLibraryService libraryService, MediaPlayerService playerService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _userService = ServiceLocator.Instance.GetService<UserService>();
            
            // 初始化集合
            _albums = new ObservableCollection<Album>();
            _albumSongs = new ObservableCollection<Song>();
            
            // 初始化命令
            RefreshCommand = new RelayCommand(() => Task.Run(LoadAlbumsAsync));
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            PlayAllSongsCommand = new RelayCommand(PlayAllSongs);
            BackToAlbumsCommand = new RelayCommand(BackToAlbums);
            SelectAlbumCommand = new RelayCommand<Album>(album => SelectedAlbum = album);
            ToggleFavoriteAlbumCommand = new RelayCommand<Album>(ToggleFavoriteAlbum);
            
            // 注册消息
            Messenger.Default.Register<NotificationMessage>(this, HandleMessage);
            
            // 加载数据
            Task.Run(LoadAlbumsAsync);
        }
        
        // 处理消息
        private void HandleMessage(NotificationMessage message)
        {
            // 处理收藏变更消息
            if (message.Notification == "FavoriteAlbumsChanged")
            {
                // 刷新专辑收藏状态
                CheckFavoriteStatusAsync();
            }
        }
        
        // 加载专辑列表
        private async Task LoadAlbumsAsync()
        {
            try
            {
                IsLoading = true;
                
                var albums = await _libraryService.GetAllAlbumsAsync();
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    Albums.Clear();
                    foreach (var album in albums)
                    {
                        Albums.Add(album);
                    }
                });
                
                // 触发属性变更通知，确保UI更新
                OnPropertyChanged(nameof(Albums));
                
                if (Albums.Count == 0)
                {
                    MessageBox.Show("没有找到任何专辑。请导入音乐文件后再试。", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                // 检查专辑收藏状态
                await CheckFavoriteStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载专辑失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // 检查所有专辑的收藏状态
        private async Task CheckFavoriteStatusAsync()
        {
            try
            {
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                    return;

                _favoriteStatus.Clear();

                // 检查每个专辑的收藏状态
                foreach (var album in Albums)
                {
                    bool isFavorite = await _libraryService.IsAlbumFavoritedAsync(currentUser.Id, album.Id);
                    _favoriteStatus[album.Id] = isFavorite;
                }

                // 通知UI更新
                OnPropertyChanged(nameof(IsAlbumFavorited));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "检查专辑收藏状态失败");
            }
        }
        
        // 判断专辑是否已收藏
        public bool IsAlbumFavorited(int albumId)
        {
            return _favoriteStatus.ContainsKey(albumId) && _favoriteStatus[albumId];
        }
        
        // 切换专辑收藏状态
        private async void ToggleFavoriteAlbum(Album album)
        {
            if (album == null)
                return;

            try
            {
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    // 尝试自动登录一个默认用户
                    try
                    {
                        currentUser = await _userService.LoginAsync("DefaultUser", "password");
                    }
                    catch
                    {
                        // 如果登录失败，提示用户但不显示详细错误
                        MessageBox.Show("无法收藏专辑，请确保有可用账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                // 检查专辑是否已收藏
                bool isFavorite = await _libraryService.IsAlbumFavoritedAsync(currentUser.Id, album.Id);

                if (isFavorite)
                {
                    // 如果已收藏，则取消收藏
                    await _libraryService.RemoveFromFavoriteAlbumsAsync(currentUser.Id, album.Id);
                    _favoriteStatus[album.Id] = false;
                    App.Logger.Info($"专辑已从收藏中移除: 用户ID={currentUser.Id}, 专辑ID={album.Id}, 专辑标题={album.Title}");
                }
                else
                {
                    // 如果未收藏，则添加到收藏
                    try 
                    {
                        App.Logger.Info($"尝试添加专辑到收藏: 用户ID={currentUser.Id}, 专辑ID={album.Id}, 专辑标题={album.Title}");
                        await _libraryService.AddToFavoriteAlbumsAsync(currentUser.Id, album.Id);
                        _favoriteStatus[album.Id] = true;
                        App.Logger.Info($"专辑添加到收藏成功: 用户ID={currentUser.Id}, 专辑ID={album.Id}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, $"添加专辑到收藏失败: 用户ID={currentUser.Id}, 专辑ID={album.Id}");
                        MessageBox.Show($"添加专辑到收藏失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                // 通知UI更新
                OnPropertyChanged(nameof(IsAlbumFavorited));
                
                // 发送消息通知收藏视图更新
                App.Logger.Info("发送FavoriteAlbumsChanged消息通知UI更新");
                Messenger.Default.Send(new NotificationMessage("FavoriteAlbumsChanged"));
                
                // 显示操作成功的提示
                string message = isFavorite ? "已从收藏中移除" : "已添加到收藏";
                MessageBox.Show($"专辑 '{album.Title}' {message}", "收藏操作", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"操作收藏失败: {ex.Message}");
                MessageBox.Show($"操作收藏失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 加载专辑的所有歌曲
        private async void LoadAlbumSongs(int albumId)
        {
            try
            {
                IsLoading = true;
                
                // 获取专辑所有歌曲
                var songs = await _libraryService.GetAlbumSongsAsync(albumId);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    AlbumSongs.Clear();
                    foreach (var song in songs)
                    {
                        AlbumSongs.Add(song);
                    }
                    // 通知UI更新歌曲数量
                    OnPropertyChanged(nameof(AlbumSongs));
                });
                
                // 检查当前专辑的收藏状态
                if (_selectedAlbum != null)
                {
                    var currentUser = _userService.CurrentUser;
                    if (currentUser != null)
                    {
                        bool isFavorite = await _libraryService.IsAlbumFavoritedAsync(currentUser.Id, _selectedAlbum.Id);
                        _favoriteStatus[_selectedAlbum.Id] = isFavorite;
                        OnPropertyChanged(nameof(IsCurrentAlbumFavorited));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载专辑歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // 判断当前专辑是否已收藏
        public bool IsCurrentAlbumFavorited
        {
            get
            {
                if (_selectedAlbum == null)
                    return false;
                    
                return _favoriteStatus.ContainsKey(_selectedAlbum.Id) && _favoriteStatus[_selectedAlbum.Id];
            }
        }
        
        // 播放单首歌曲
        private async void PlaySong(Song song)
        {
            if (song == null)
                return;
            
            try
            {
                // 将当前专辑的所有歌曲设为播放列表
                int songIndex = AlbumSongs.IndexOf(song);
                if (songIndex >= 0)
                {
                    _playerService.SetPlaylist(new List<Song>(AlbumSongs), songIndex);
                }
                
                // 播放选中的歌曲
                await _playerService.PlayAsync(song);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 播放专辑的所有歌曲
        private async void PlayAllSongs()
        {
            if (AlbumSongs.Count == 0)
                return;
            
            try
            {
                // 设置播放列表为当前专辑的所有歌曲
                _playerService.SetPlaylist(new List<Song>(AlbumSongs), 0);
                
                // 开始播放
                await _playerService.PlayAsync(AlbumSongs[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 返回专辑列表
        private void BackToAlbums()
        {
            IsAlbumDetailVisible = false;
            SelectedAlbum = null;
        }
        
        // 刷新数据
        public void Refresh()
        {
            Task.Run(LoadAlbumsAsync);
        }
        
        // 清理
        public override void Cleanup()
        {
            Messenger.Default.Unregister(this);
            base.Cleanup();
        }
    }
} 