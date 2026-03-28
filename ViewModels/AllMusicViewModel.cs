using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System.Windows.Controls;
using System.Windows.Media;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

namespace MusicPlayerApp.ViewModels
{
    public class AllMusicViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        private readonly UserService _userService;

        private ObservableCollection<Song> _songs;
        private Song _selectedSong;
        private bool _isLoading;
        private Dictionary<int, bool> _favoriteStatus = new Dictionary<int, bool>();

        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set => Set(ref _songs, value);
        }

        public Song SelectedSong
        {
            get => _selectedSong;
            set => Set(ref _selectedSong, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        // 命令
        public ICommand PlaySongCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }

        public AllMusicViewModel(MediaLibraryService libraryService, MediaPlayerService playerService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _userService = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<UserService>();

            // 初始化集合
            _songs = new ObservableCollection<Song>();

            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            AddToPlaylistCommand = new RelayCommand<Song>(AddToPlaylist);
            RefreshCommand = new RelayCommand(RefreshSongs);
            
            // 特别记录收藏命令的初始化
            App.Logger.Info("正在初始化ToggleFavoriteCommand...");
            ToggleFavoriteCommand = new RelayCommand<Song>(ToggleFavorite);
            App.Logger.Info("ToggleFavoriteCommand初始化完成");

            // 注册消息
            Messenger.Default.Register<NotificationMessage>(this, HandleMessage);

            // 加载数据
            LoadSongsAsync();
            
            App.Logger.Info("AllMusicViewModel构造函数执行完成");
        }

        // 处理消息
        private void HandleMessage(NotificationMessage message)
        {
            if (message.Notification == "FavoritesChanged")
            {
                // 当收藏状态改变时，刷新收藏状态
                CheckFavoriteStatusAsync();
            }
        }

        // 加载所有歌曲
        private async void LoadSongsAsync()
        {
            try
            {
                IsLoading = true;

                var songs = await _libraryService.SearchSongsAsync(string.Empty);
                
                // 创建新集合
                var newSongs = new ObservableCollection<Song>();
                foreach (var song in songs)
                {
                    newSongs.Add(song);
                }
                
                // 替换集合
                Songs = newSongs;
                
                // 触发属性变更通知，确保UI更新
                OnPropertyChanged(nameof(Songs));

                if (Songs.Count == 0)
                {
                    MessageBox.Show("没有找到任何歌曲。请导入音乐文件后再试。", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // 加载完歌曲后检查收藏状态
                await CheckFavoriteStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                // 再次触发属性变更通知
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        // 检查所有歌曲的收藏状态
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

        // 刷新歌曲列表
        private void RefreshSongs()
        {
            LoadSongsAsync();
        }

        // 播放歌曲
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
                    // 设置当前播放列表为所有歌曲，从选中的歌曲开始播放
                    _playerService.SetPlaylist(Songs.ToList(), songIndex);
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

        // 将歌曲添加到播放列表
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

        // 切换喜欢状态
        private async void ToggleFavorite(Song song)
        {
            App.Logger.Info($"进入ToggleFavorite方法，处理歌曲: {song?.Title ?? "null"}");
            
            if (song == null)
            {
                App.Logger.Warn("ToggleFavorite方法收到null参数");
                return;
            }

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
                    
                    // 通知用户
                    App.Current.Dispatcher.Invoke(() => {
                        // 通知UI更新
                        OnPropertyChanged(nameof(IsSongFavorited));
                    });
                    
                    App.Logger.Info($"已从我喜欢的音乐中移除歌曲: {song.Title} (ID: {song.Id})");
                }
                else
                {
                    // 如果不在"我喜欢的音乐"中，则添加
                    await _libraryService.AddToFavoritesAsync(currentUser.Id, song.Id);
                    _favoriteStatus[song.Id] = true;
                    
                    // 通知用户
                    App.Current.Dispatcher.Invoke(() => {
                        // 通知UI更新
                        OnPropertyChanged(nameof(IsSongFavorited));
                    });
                    
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

        // 清理
        public override void Cleanup()
        {
            Messenger.Default.Unregister(this);
            base.Cleanup();
        }
    }
}