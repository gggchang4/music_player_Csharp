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

namespace MusicPlayerApp.ViewModels
{
    public class PlaylistViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _mediaLibraryService;
        private readonly MediaPlayerService _mediaPlayerService;
        private readonly UserService _userService;

        private Playlist _currentPlaylist;
        public Playlist CurrentPlaylist
        {
            get => _currentPlaylist;
            set
            {
                if (Set(ref _currentPlaylist, value))
                {
                    LoadPlaylistSongs();
                }
            }
        }

        private ObservableCollection<Song> _songs;
        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set => Set(ref _songs, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _editPlaylistName;
        public string EditPlaylistName
        {
            get => _editPlaylistName;
            set => Set(ref _editPlaylistName, value);
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => Set(ref _isEditMode, value);
        }

        // 命令
        public ICommand PlaySongCommand { get; private set; }
        public ICommand RemoveSongCommand { get; private set; }
        public ICommand PlayAllCommand { get; private set; }
        public ICommand ShufflePlayCommand { get; private set; }
        public ICommand RenamePlaylistCommand { get; private set; }
        public ICommand DeletePlaylistCommand { get; private set; }
        public ICommand SavePlaylistNameCommand { get; private set; }
        public ICommand CancelEditCommand { get; private set; }

        public PlaylistViewModel(MediaLibraryService mediaLibraryService, MediaPlayerService mediaPlayerService, UserService userService)
        {
            _mediaLibraryService = mediaLibraryService;
            _mediaPlayerService = mediaPlayerService;
            _userService = userService;

            Songs = new ObservableCollection<Song>();

            // 初始化命令
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            RemoveSongCommand = new RelayCommand<Song>(RemoveSong);
            PlayAllCommand = new RelayCommand(PlayAll);
            ShufflePlayCommand = new RelayCommand(ShufflePlay);
            RenamePlaylistCommand = new RelayCommand(StartRenamePlaylist);
            DeletePlaylistCommand = new RelayCommand(DeletePlaylist);
            SavePlaylistNameCommand = new RelayCommand(SavePlaylistName);
            CancelEditCommand = new RelayCommand(CancelEdit);

            // 注册消息
            Messenger.Default.Register<Playlist>(this, "LoadPlaylist", playlist => 
            {
                CurrentPlaylist = playlist;
            });
        }

        private void LoadPlaylistSongs()
        {
            if (CurrentPlaylist == null)
                return;

            IsLoading = true;
            Task.Run(async () =>
            {
                try
                {
                    var songs = await _mediaLibraryService.GetPlaylistSongsAsync(CurrentPlaylist.Id);

                    // 在UI线程更新ObservableCollection
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Songs.Clear();
                        foreach (var song in songs)
                        {
                            Songs.Add(song);
                        }
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"加载播放列表歌曲失败: {CurrentPlaylist.Id}");
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }

        private async void PlaySong(Song song)
        {
            if (song == null)
                return;

            try
            {
                await _mediaPlayerService.PlaySong(song);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"播放歌曲失败: {song.Title}");
            }
        }

        private void RemoveSong(Song song)
        {
            if (song == null || CurrentPlaylist == null)
                return;

            Task.Run(async () =>
            {
                try
                {
                    await _mediaLibraryService.RemoveSongFromPlaylistAsync(CurrentPlaylist.Id, song.Id);
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Songs.Remove(song);
                    });

                    App.Logger.Info($"从播放列表移除歌曲: {song.Title}");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"从播放列表移除歌曲失败: {song.Title}");
                }
            });
        }

        private async void PlayAll()
        {
            if (Songs.Count == 0)
                return;

            try
            {
                await _mediaPlayerService.PlaySongList(Songs, false);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放所有歌曲失败");
            }
        }

        private async void ShufflePlay()
        {
            if (Songs.Count == 0)
                return;

            try
            {
                await _mediaPlayerService.PlaySongList(Songs, true);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "随机播放歌曲失败");
            }
        }

        private void StartRenamePlaylist()
        {
            if (CurrentPlaylist == null)
                return;

            EditPlaylistName = CurrentPlaylist.Title;
            IsEditMode = true;
        }

        private void SavePlaylistName()
        {
            if (CurrentPlaylist == null || string.IsNullOrWhiteSpace(EditPlaylistName))
                return;

            Task.Run(async () =>
            {
                try
                {
                    CurrentPlaylist.Title = EditPlaylistName;
                    await _mediaLibraryService.UpdatePlaylistAsync(CurrentPlaylist);
                    App.Logger.Info($"播放列表重命名为: {EditPlaylistName}");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"重命名播放列表失败: {EditPlaylistName}");
                }
                finally
                {
                    IsEditMode = false;
                }
            });
        }

        private void CancelEdit()
        {
            IsEditMode = false;
        }

        private void DeletePlaylist()
        {
            if (CurrentPlaylist == null)
                return;

            Task.Run(async () =>
            {
                try
                {
                    await _mediaLibraryService.DeletePlaylistAsync(CurrentPlaylist.Id);
                    App.Logger.Info($"播放列表已删除: {CurrentPlaylist.Title}");
                    
                    // 通知其他视图播放列表已更改
                    Messenger.Default.Send("PlaylistDeleted");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"删除播放列表失败: {CurrentPlaylist.Title}");
                }
            });
        }

        public void Cleanup()
        {
            Messenger.Default.Unregister(this);
            base.Cleanup();
        }
    }
} 