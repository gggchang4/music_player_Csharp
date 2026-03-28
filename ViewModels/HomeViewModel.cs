using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using RelayCommand = GalaSoft.MvvmLight.Command.RelayCommand;

namespace MusicPlayerApp.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _mediaLibraryService;
        private readonly MediaPlayerService _mediaPlayerService;
        private readonly UserService _userService;

        private ObservableCollection<Song> _recentlyPlayedSongs;
        public ObservableCollection<Song> RecentlyPlayedSongs
        {
            get => _recentlyPlayedSongs;
            set => Set(ref _recentlyPlayedSongs, value);
        }

        private ObservableCollection<Song> _mostPlayedSongs;
        public ObservableCollection<Song> MostPlayedSongs
        {
            get => _mostPlayedSongs;
            set => Set(ref _mostPlayedSongs, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public ICommand PlaySongCommand { get; private set; }

        public HomeViewModel(MediaLibraryService mediaLibraryService, MediaPlayerService mediaPlayerService, UserService userService)
        {
            _mediaLibraryService = mediaLibraryService;
            _mediaPlayerService = mediaPlayerService;
            _userService = userService;

            RecentlyPlayedSongs = new ObservableCollection<Song>();
            MostPlayedSongs = new ObservableCollection<Song>();

            PlaySongCommand = new RelayCommand<Song>(PlaySong);

            // 加载数据
            Task.Run(async () => await LoadDataAsync());
        }

        private void PlaySong(Song song)
        {
            if (song != null)
            {
                _mediaPlayerService.PlaySong(song);
            }
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                var currentUser = _userService.CurrentUser;
                if (currentUser != null)
                {
                    // 获取最近播放的歌曲
                    var recentSongs = await _mediaLibraryService.GetRecentlyPlayedSongsAsync(currentUser.Id, 10);
                    
                    // 获取播放最多的歌曲
                    var topSongs = await _mediaLibraryService.GetMostPlayedSongsAsync(currentUser.Id, 10);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        RecentlyPlayedSongs.Clear();
                        foreach (var song in recentSongs)
                        {
                            RecentlyPlayedSongs.Add(song);
                        }

                        MostPlayedSongs.Clear();
                        foreach (var song in topSongs)
                        {
                            MostPlayedSongs.Add(song);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载首页数据失败");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
} 