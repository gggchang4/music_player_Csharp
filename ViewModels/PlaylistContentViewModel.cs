using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.ViewModels
{
    public class PlaylistContentViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        private readonly Playlist _playlist;
        
        private ObservableCollection<Song> _songs;
        private Song _selectedSong;
        private bool _isLoading;
        private string _playlistTitle;
        private int _songsCount;
        
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
        
        public string PlaylistTitle
        {
            get => _playlistTitle;
            set => Set(ref _playlistTitle, value);
        }
        
        public int SongsCount
        {
            get => _songsCount;
            set => Set(ref _songsCount, value);
        }
        
        public bool HasSongs => Songs != null && Songs.Count > 0;
        
        public bool HasNoSongs => Songs == null || Songs.Count == 0;
        
        // 命令
        public ICommand PlaySongCommand { get; }
        public ICommand PlayAllCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RemoveFromPlaylistCommand { get; }
        public ICommand DeletePlaylistCommand { get; }
        
        public PlaylistContentViewModel(Playlist playlist)
        {
            _playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
            _libraryService = ServiceLocator.Instance.GetService<MediaLibraryService>();
            _playerService = ServiceLocator.Instance.GetService<MediaPlayerService>();
            
            // 初始化集合
            _songs = new ObservableCollection<Song>();
            
            // 初始化属性
            PlaylistTitle = playlist.Title;
            
            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            PlayAllCommand = new RelayCommand(PlayAll);
            RefreshCommand = new RelayCommand(async () => await LoadPlaylistContentAsync());
            RemoveFromPlaylistCommand = new RelayCommand<Song>(RemoveSongFromPlaylist);
            DeletePlaylistCommand = new RelayCommand(DeletePlaylist);
        }
        
        public async Task LoadPlaylistContentAsync()
        {
            try
            {
                IsLoading = true;
                
                var songs = await _libraryService.GetPlaylistSongsAsync(_playlist.Id);
                
                // 清空并添加歌曲
                Songs.Clear();
                foreach (var song in songs)
                {
                    Songs.Add(song);
                }
                
                // 更新统计信息
                SongsCount = Songs.Count;
                
                // 触发属性变更
                OnPropertyChanged(nameof(HasSongs));
                OnPropertyChanged(nameof(HasNoSongs));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载播放列表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, $"加载播放列表失败，播放列表ID: {_playlist.Id}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async void PlaySong(Song song)
        {
            if (song == null)
                return;
                
            try
            {
                // 播放选中的歌曲，并将当前播放列表设置为此播放列表
                if (Songs.Count > 0)
                {
                    int songIndex = Songs.IndexOf(song);
                    if (songIndex >= 0)
                    {
                        _playerService.SetPlaylist(Songs.ToList(), songIndex);
                    }
                }
                
                await _playerService.PlayAsync(song);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, $"播放歌曲失败，歌曲ID: {song.Id}");
            }
        }
        
        private void PlayAll()
        {
            if (Songs.Count == 0)
                return;
                
            try
            {
                // 设置播放列表并播放第一首歌
                _playerService.SetPlaylist(Songs.ToList());
                _playerService.PlayAsync(Songs[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放全部失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, "播放全部失败");
            }
        }
        
        private async void RemoveSongFromPlaylist(Song song)
        {
            if (song == null)
                return;
                
            try
            {
                var result = MessageBox.Show($"确定要将歌曲「{song.Title}」从播放列表中移除吗？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return;
                    
                await _libraryService.RemoveSongFromPlaylistAsync(_playlist.Id, song.Id);
                
                // 从UI中移除
                Songs.Remove(song);
                
                // 更新统计信息
                SongsCount = Songs.Count;
                
                // 触发属性变更
                OnPropertyChanged(nameof(HasSongs));
                OnPropertyChanged(nameof(HasNoSongs));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"从播放列表移除歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, $"从播放列表移除歌曲失败，播放列表ID: {_playlist.Id}, 歌曲ID: {song.Id}");
            }
        }
        
        private async void DeletePlaylist()
        {
            if (_playlist == null)
                return;
            
            // 检查是否是"我喜欢的音乐"播放列表，这个不允许删除
            if (_playlist.Title == "我喜欢的音乐")
            {
                MessageBox.Show("'我喜欢的音乐'播放列表不能被删除。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                var result = MessageBox.Show($"确定要删除播放列表「{_playlist.Title}」吗？\n此操作无法撤销，但其中的歌曲不会被删除。", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                    return;
                
                // 执行删除
                await _libraryService.DeletePlaylistAsync(_playlist.Id);
                
                MessageBox.Show($"播放列表「{_playlist.Title}」已成功删除。", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                    
                // 先导航回主页面
                GalaSoft.MvvmLight.Messaging.Messenger.Default.Send(new GalaSoft.MvvmLight.Messaging.NotificationMessage("NavigateToAllMusic"));
                
                // 发送通知以刷新播放列表（添加延迟确保UI更新后再刷新列表）
                await Task.Delay(100); // 短暂延迟确保消息按顺序处理
                GalaSoft.MvvmLight.Messaging.Messenger.Default.Send(new GalaSoft.MvvmLight.Messaging.NotificationMessage("PlaylistsChanged"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除播放列表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, $"删除播放列表失败，播放列表ID: {_playlist.Id}");
            }
        }
    }
} 