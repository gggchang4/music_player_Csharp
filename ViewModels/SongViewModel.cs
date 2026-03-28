using System;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;

namespace MusicPlayerApp.ViewModels
{
    public class SongViewModel : ViewModelBase
    {
        private readonly MediaPlayerService _mediaPlayerService;
        private readonly MediaLibraryService _mediaLibraryService;
        private readonly UserService _userService;

        private readonly Song _song;

        public SongViewModel(Song song, MediaPlayerService mediaPlayerService, MediaLibraryService mediaLibraryService, UserService userService)
        {
            _song = song ?? throw new ArgumentNullException(nameof(song));
            _mediaPlayerService = mediaPlayerService;
            _mediaLibraryService = mediaLibraryService;
            _userService = userService;

            // 初始化命令
            PlayCommand = new RelayCommand(PlaySong);
            ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);

            // 检查收藏状态
            CheckFavoriteStatus();
        }

        public int Id => _song.Id;
        public string Title => _song.Title;
        public string Artist => _song.Artist?.Name ?? "未知艺术家";
        public string Album => _song.Album?.Title ?? "未知专辑";
        public string Duration => _song.GetFormattedDuration();
        public string AlbumArt => _song.AlbumArt;
        public int TrackNumber => _song.TrackNumber;
        public string FilePath => _song.FilePath;

        // 获取原始歌曲对象
        public Song Song => _song;

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set => Set(ref _isFavorite, value);
        }

        // 命令
        public ICommand PlayCommand { get; private set; }
        public ICommand ToggleFavoriteCommand { get; private set; }

        private async void CheckFavoriteStatus()
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null || _song == null)
                return;

            try
            {
                IsFavorite = await _mediaLibraryService.IsSongFavoritedAsync(currentUser.Id, _song.Id);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "检查歌曲收藏状态失败");
            }
        }

        private async void PlaySong()
        {
            if (_song == null)
                return;

            try
            {
                await _mediaPlayerService.PlaySong(_song);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"播放歌曲失败: {_song.Title}");
            }
        }

        private async void ToggleFavorite()
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null || _song == null)
                return;

            try
            {
                if (IsFavorite)
                {
                    await _mediaLibraryService.RemoveFromFavoritesAsync(currentUser.Id, _song.Id);
                    App.Logger.Info($"歌曲已从收藏中移除: {_song.Title}");
                }
                else
                {
                    await _mediaLibraryService.AddToFavoritesAsync(currentUser.Id, _song.Id);
                    App.Logger.Info($"歌曲已添加到收藏: {_song.Title}");
                }

                // 更新状态
                IsFavorite = !IsFavorite;

                // 通知其他视图收藏已更改
                Messenger.Default.Send(new NotificationMessage("FavoritesChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "切换收藏状态失败");
            }
        }
    }
} 