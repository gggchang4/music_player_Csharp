using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MusicPlayerApp.ViewModels
{
    public class AlbumDetailsViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        private readonly UserService _userService;

        private Album _album;
        private ObservableCollection<Song> _songs;
        private bool _isLoading;

        public Album Album
        {
            get => _album;
            set => Set(ref _album, value);
        }

        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set => Set(ref _songs, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        // 格式化后的歌曲数量文本
        public string SongCountText => Songs?.Count > 0 ? $"{Songs.Count} 首歌曲" : "暂无歌曲";

        // 命令
        public ICommand PlaySongCommand { get; }
        public ICommand PlayAlbumCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand NavigateToArtistCommand { get; }
        public ICommand BackCommand { get; }

        public AlbumDetailsViewModel(Album album, MediaLibraryService libraryService, MediaPlayerService playerService, UserService userService)
        {
            _album = album ?? throw new ArgumentNullException(nameof(album));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));

            // 初始化集合
            Songs = new ObservableCollection<Song>();

            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            PlayAlbumCommand = new RelayCommand(PlayAlbum);
            AddToPlaylistCommand = new RelayCommand<Song>(AddToPlaylist);
            ToggleFavoriteCommand = new RelayCommand<Song>(ToggleFavorite);
            NavigateToArtistCommand = new RelayCommand<Artist>(NavigateToArtist);
            BackCommand = new RelayCommand(NavigateBack);

            // 加载专辑详情
            LoadAlbumDetailsAsync();
        }

        private async void LoadAlbumDetailsAsync()
        {
            try
            {
                IsLoading = true;

                // 加载专辑的歌曲
                var songs = await _libraryService.GetAlbumSongsAsync(_album.Id);
                App.Logger.Info($"为专辑 '{_album.Title}' (ID: {_album.Id}) 加载到 {songs.Count} 首歌曲");
                
                Songs.Clear();
                foreach (var song in songs)
                {
                    Songs.Add(song);
                }
                
                // 如果没有数据，显示提示
                if (songs.Count == 0)
                {
                    MessageBox.Show($"专辑 '{_album.Title}' 中没有歌曲", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"加载专辑详情失败: {_album.Title}");
                MessageBox.Show($"加载专辑详情失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void PlayAlbum()
        {
            try
            {
                if (Songs.Count == 0)
                    return;

                // 设置播放列表为专辑的所有歌曲，从第一首开始播放
                _playerService.SetPlaylist(Songs.ToList(), 0);
                
                // 播放第一首歌曲
                _playerService.PlayAsync(Songs[0]);
                
                App.Logger.Info($"开始播放专辑: {_album.Title}");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"播放专辑失败: {_album.Title}");
                MessageBox.Show($"播放专辑失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    // 设置当前播放列表为专辑的歌曲，从选中的歌曲开始播放
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
                        MessageBox.Show("无法添加到播放列表，请确保有可用账户", "提示", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                // 获取用户的播放列表
                var playlists = await _libraryService.GetUserPlaylistsAsync(currentUser.Id);

                // 显示播放列表选择对话框
                var dialog = new MusicPlayerApp.Views.SelectPlaylistDialog(playlists, song);
                dialog.Owner = Application.Current.MainWindow;
                
                var dialogResult = dialog.ShowDialog();
                
                if (dialogResult.HasValue && dialogResult.Value)
                {
                    App.Logger.Info($"歌曲 \"{song.Title}\" 成功添加到播放列表");
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
                        MessageBox.Show("无法添加到我喜欢的音乐，请确保有可用账户", "提示", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                bool isFavorite = await _libraryService.IsSongFavoritedAsync(currentUser.Id, song.Id);
                
                if (isFavorite)
                {
                    // 如果已在"我喜欢的音乐"中，则移除
                    await _libraryService.RemoveFromFavoritesAsync(currentUser.Id, song.Id);
                }
                else
                {
                    // 如果不在"我喜欢的音乐"中，则添加
                    await _libraryService.AddToFavoritesAsync(currentUser.Id, song.Id);
                    
                    // 同步到"我喜欢的音乐"播放列表
                    await _libraryService.SyncFavoritesToPlaylistAsync(currentUser.Id);
                }
                
                // 发送通知消息，以便其他视图更新
                Messenger.Default.Send(new NotificationMessage("FavoritesChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"处理喜欢状态失败: {ex.Message}");
                MessageBox.Show($"添加到我喜欢的音乐失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToArtist(Artist artist)
        {
            if (artist == null)
                return;

            // 发送导航消息
            Messenger.Default.Send(artist, "NavigateToArtist");
        }

        private void NavigateBack()
        {
            // 发送消息返回到主页
            Messenger.Default.Send(new NotificationMessage("NavigateToAllMusic"));
        }
    }
} 