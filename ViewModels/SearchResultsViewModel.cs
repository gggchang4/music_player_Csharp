using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public class SearchResultsViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        private readonly UserService _userService;

        private string _searchQuery;
        private bool _isLoading;
        private ObservableCollection<Song> _songs;
        private ObservableCollection<Album> _albums;
        private ObservableCollection<Artist> _artists;
        private Dictionary<int, bool> _favoriteStatus = new Dictionary<int, bool>();

        // 属性
        public string SearchQuery
        {
            get => _searchQuery;
            set => Set(ref _searchQuery, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set => Set(ref _songs, value);
        }

        public ObservableCollection<Album> Albums
        {
            get => _albums;
            set => Set(ref _albums, value);
        }

        public ObservableCollection<Artist> Artists
        {
            get => _artists;
            set => Set(ref _artists, value);
        }

        public bool HasNoResults
        {
            get => !IsLoading && Songs.Count == 0 && Albums.Count == 0 && Artists.Count == 0 && !string.IsNullOrWhiteSpace(SearchQuery);
        }

        // 命令
        public ICommand PlaySongCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NavigateToArtistCommand { get; }
        public ICommand NavigateToAlbumCommand { get; }
        public ICommand BackCommand { get; }

        public SearchResultsViewModel(string query, MediaLibraryService libraryService, MediaPlayerService playerService, UserService userService)
        {
            _searchQuery = query;
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));

            // 初始化集合
            Songs = new ObservableCollection<Song>();
            Albums = new ObservableCollection<Album>();
            Artists = new ObservableCollection<Artist>();

            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            AddToPlaylistCommand = new RelayCommand<Song>(AddToPlaylist);
            ToggleFavoriteCommand = new RelayCommand<Song>(ToggleFavorite);
            SearchCommand = new RelayCommand(ExecuteSearch);
            NavigateToArtistCommand = new RelayCommand<Artist>(NavigateToArtist);
            NavigateToAlbumCommand = new RelayCommand<Album>(NavigateToAlbum);
            BackCommand = new RelayCommand(NavigateBack);

            // 注册消息
            Messenger.Default.Register<NotificationMessage>(this, HandleMessage);

            // 执行搜索
            ExecuteSearch();
        }

        private void HandleMessage(NotificationMessage message)
        {
            if (message.Notification == "FavoritesChanged")
            {
                // 当收藏状态改变时，刷新收藏状态
                CheckFavoriteStatusAsync();
            }
        }

        public void ExecuteSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
                return;

            SearchAsync();
        }

        private async void SearchAsync()
        {
            try
            {
                IsLoading = true;
                OnPropertyChanged(nameof(HasNoResults));

                // 清空现有结果
                Songs.Clear();
                Albums.Clear();
                Artists.Clear();

                // 搜索歌曲
                var songs = await _libraryService.SearchSongsAsync(SearchQuery);
                foreach (var song in songs)
                {
                    Songs.Add(song);
                }

                // 搜索专辑
                var albums = await _libraryService.SearchAlbumsAsync(SearchQuery);
                foreach (var album in albums)
                {
                    Albums.Add(album);
                }

                // 搜索艺术家
                var artists = await _libraryService.SearchArtistsAsync(SearchQuery);
                foreach (var artist in artists)
                {
                    Artists.Add(artist);
                }

                // 检查收藏状态
                await CheckFavoriteStatusAsync();

                // 通知HasNoResults属性更新
                OnPropertyChanged(nameof(HasNoResults));

                // 如果没有结果
                if (Songs.Count == 0 && Albums.Count == 0 && Artists.Count == 0)
                {
                    MessageBox.Show($"没有找到与 \"{SearchQuery}\" 相关的内容", "搜索结果", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"搜索失败: {SearchQuery}");
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoResults));
            }
        }

        private async Task CheckFavoriteStatusAsync()
        {
            try
            {
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                    return;

                _favoriteStatus.Clear();

                // 检查每首歌曲的收藏状态
                foreach (var song in Songs)
                {
                    bool isFavorite = await _libraryService.IsSongFavoritedAsync(currentUser.Id, song.Id);
                    _favoriteStatus[song.Id] = isFavorite;
                }

                // 通知UI更新
                OnPropertyChanged(nameof(IsSongFavorited));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "检查歌曲收藏状态失败");
            }
        }

        // 判断歌曲是否已收藏
        public bool IsSongFavorited(int songId)
        {
            return _favoriteStatus.ContainsKey(songId) && _favoriteStatus[songId];
        }
        
        // 获取歌曲的收藏图标
        public string GetFavoriteIcon(int songId)
        {
            return IsSongFavorited(songId) ? "Favorite" : "FavoriteBorder";
        }
        
        // 获取歌曲的收藏颜色
        public string GetFavoriteColor(int songId)
        {
            return IsSongFavorited(songId) ? "#FF5252" : "#AAAAAA";
        }

        private async void PlaySong(Song song)
        {
            if (song == null)
                return;

            try
            {
                // 找到当前歌曲在列表中的索引
                int songIndex = Songs.IndexOf(song);
                if (songIndex >= 0)
                {
                    // 设置当前播放列表为搜索结果中的歌曲，从选中的歌曲开始播放
                    _playerService.SetPlaylist(Songs.ToList(), songIndex);
                }
                
                // 播放选中的歌曲
                await _playerService.PlayAsync(song);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"播放歌曲失败: {song.Title}");
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddToPlaylist(Song song)
        {
            if (song == null)
                return;

            try
            {
                App.Logger.Info($"尝试添加歌曲到播放列表: {song.Title} (ID: {song.Id})");
                
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
                        MessageBox.Show("无法添加到播放列表，请确保有可用账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                // 获取用户的播放列表
                var playlists = await _libraryService.GetUserPlaylistsAsync(currentUser.Id);

                // 显示播放列表选择对话框
                var dialog = new MusicPlayerApp.Views.SelectPlaylistDialog(playlists, song);
                dialog.Owner = Application.Current.MainWindow;
                
                try 
                {
                    var dialogResult = dialog.ShowDialog();
                
                    if (dialogResult.HasValue && dialogResult.Value)
                    {
                        // 不需要额外的消息提示，因为SelectPlaylistDialog中已经处理了成功消息
                        App.Logger.Info($"歌曲 \"{song.Title}\" 成功添加到播放列表");
                    }
                }
                catch (Exception dialogEx)
                {
                    App.Logger.Error(dialogEx, "显示播放列表选择对话框时发生错误");
                    MessageBox.Show($"添加到播放列表时发生错误: {dialogEx.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"添加歌曲 \"{song.Title}\" 到播放列表失败");
                MessageBox.Show($"添加到播放列表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ToggleFavorite(Song song)
        {
            if (song == null)
                return;

            try
            {
                App.Logger.Info($"尝试切换歌曲喜欢状态: {song.Title} (ID: {song.Id})");
                
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    // 尝试自动登录一个默认用户
                    try
                    {
                        App.Logger.Info("当前无用户，尝试自动登录默认用户");
                        currentUser = await _userService.LoginAsync("DefaultUser", "password");
                        App.Logger.Info($"登录成功: {currentUser.Username} (ID: {currentUser.Id})");
                    }
                    catch (Exception loginEx)
                    {
                        App.Logger.Error(loginEx, "尝试登录默认用户失败");
                        // 如果登录失败，提示用户但不显示详细错误
                        MessageBox.Show("无法添加到我喜欢的音乐，请确保有可用账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                bool isFavorite = await _libraryService.IsSongFavoritedAsync(currentUser.Id, song.Id);
                
                App.Logger.Info($"歌曲 {song.Title} (ID: {song.Id}) 当前收藏状态: {isFavorite}");
                
                // 获取"我喜欢的音乐"播放列表
                var favoritesPlaylist = await _libraryService.GetOrCreateFavoritesPlaylistAsync(currentUser.Id);
                
                if (isFavorite)
                {
                    // 如果已在"我喜欢的音乐"中，则移除
                    await _libraryService.RemoveFromFavoritesAsync(currentUser.Id, song.Id);
                    _favoriteStatus[song.Id] = false;
                    
                    // 重新加载收藏状态并刷新UI
                    await CheckFavoriteStatusAsync();
                    
                    App.Logger.Info($"已从我喜欢的音乐中移除歌曲: {song.Title} (ID: {song.Id})");
                }
                else
                {
                    // 如果不在"我喜欢的音乐"中，则添加
                    await _libraryService.AddToFavoritesAsync(currentUser.Id, song.Id);
                    _favoriteStatus[song.Id] = true;
                    
                    // 重新加载收藏状态并刷新UI
                    await CheckFavoriteStatusAsync();
                    
                    App.Logger.Info($"已将歌曲添加到我喜欢的音乐: {song.Title} (ID: {song.Id})");
                    
                    // 立即同步到"我喜欢的音乐"播放列表，确保实时更新
                    try
                    {
                        await _libraryService.SyncFavoritesToPlaylistAsync(currentUser.Id);
                    }
                    catch (Exception syncEx)
                    {
                        App.Logger.Error(syncEx, "同步收藏歌曲到播放列表失败");
                        // 不向用户提示此错误，因为歌曲已成功添加到收藏中
                    }
                }
                
                // 发送通知消息，以便其他视图更新
                Messenger.Default.Send(new NotificationMessage("FavoritesChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"处理喜欢状态失败: {ex.Message}");
                MessageBox.Show($"添加到我喜欢的音乐失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToArtist(Artist artist)
        {
            if (artist == null)
                return;

            // 发送导航消息
            Messenger.Default.Send(artist, "NavigateToArtist");
        }

        private void NavigateToAlbum(Album album)
        {
            if (album == null)
                return;

            // 发送导航消息
            Messenger.Default.Send(album, "NavigateToAlbum");
        }

        // 返回前一个页面
        private void NavigateBack()
        {
            // 发送消息返回到主页
            Messenger.Default.Send(new NotificationMessage("NavigateToAllMusic"));
        }

        // 清理
        public override void Cleanup()
        {
            Messenger.Default.Unregister(this);
            base.Cleanup();
        }
    }
} 