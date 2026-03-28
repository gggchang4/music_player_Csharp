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

namespace MusicPlayerApp.ViewModels
{
    public class ArtistsViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly MediaPlayerService _playerService;
        
        private ObservableCollection<Artist> _artists;
        private Artist _selectedArtist;
        private ObservableCollection<Song> _artistSongs;
        private bool _isLoading;
        private bool _isArtistDetailVisible;
        
        public ObservableCollection<Artist> Artists
        {
            get => _artists;
            set => Set(ref _artists, value);
        }
        
        public Artist SelectedArtist
        {
            get => _selectedArtist;
            set
            {
                if (Set(ref _selectedArtist, value) && value != null)
                {
                    LoadArtistSongs(value.Id);
                    IsArtistDetailVisible = true;
                }
            }
        }
        
        public ObservableCollection<Song> ArtistSongs
        {
            get => _artistSongs;
            set => Set(ref _artistSongs, value);
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
        
        public bool IsArtistDetailVisible
        {
            get => _isArtistDetailVisible;
            set => Set(ref _isArtistDetailVisible, value);
        }
        
        // 命令
        public ICommand RefreshCommand { get; }
        public ICommand PlaySongCommand { get; }
        public ICommand PlayAllSongsCommand { get; }
        public ICommand BackToArtistsCommand { get; }
        public ICommand SelectArtistCommand { get; }
        
        public ArtistsViewModel(MediaLibraryService libraryService, MediaPlayerService playerService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            
            // 初始化集合
            _artists = new ObservableCollection<Artist>();
            _artistSongs = new ObservableCollection<Song>();
            
            // 初始化命令
            RefreshCommand = new RelayCommand(() => Task.Run(LoadArtistsAsync));
            PlaySongCommand = new RelayCommand<Song>(PlaySong);
            PlayAllSongsCommand = new RelayCommand(PlayAllSongs);
            BackToArtistsCommand = new RelayCommand(BackToArtists);
            SelectArtistCommand = new RelayCommand<Artist>(artist => SelectedArtist = artist);
            
            // 加载数据
            Task.Run(LoadArtistsAsync);
        }
        
        // 加载艺术家列表
        private async Task LoadArtistsAsync()
        {
            try
            {
                IsLoading = true;
                
                var artists = await _libraryService.GetAllArtistsAsync();
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    Artists.Clear();
                    foreach (var artist in artists)
                    {
                        Artists.Add(artist);
                    }
                });
                
                if (Artists.Count == 0)
                {
                    MessageBox.Show("没有找到任何艺术家。请导入音乐文件后再试。", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载艺术家失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // 加载艺术家的所有歌曲
        private async void LoadArtistSongs(int artistId)
        {
            try
            {
                IsLoading = true;
                
                // 搜索艺术家的歌曲，这里假设SearchSongsAsync可以根据artistId过滤
                // 如果没有这样的方法，可能需要扩展MediaLibraryService
                var songs = await _libraryService.SearchSongsAsync(SelectedArtist.Name);
                
                // 只保留属于当前艺术家的歌曲
                songs = songs.FindAll(s => s.ArtistId == artistId);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    ArtistSongs.Clear();
                    foreach (var song in songs)
                    {
                        ArtistSongs.Add(song);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载艺术家歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // 播放单首歌曲
        private void PlaySong(Song song)
        {
            if (song == null)
                return;
            
            try
            {
                // 将当前艺术家的所有歌曲设为播放列表
                int songIndex = ArtistSongs.IndexOf(song);
                if (songIndex >= 0)
                {
                    _playerService.SetPlaylist(new List<Song>(ArtistSongs), songIndex);
                }
                
                // 播放选中的歌曲
                _playerService.PlayAsync(song);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 播放艺术家的所有歌曲
        private void PlayAllSongs()
        {
            if (ArtistSongs.Count == 0)
                return;
            
            try
            {
                // 设置播放列表为当前艺术家的所有歌曲
                _playerService.SetPlaylist(new List<Song>(ArtistSongs), 0);
                
                // 开始播放
                _playerService.PlayAsync(ArtistSongs[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放歌曲失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 返回艺术家列表
        private void BackToArtists()
        {
            IsArtistDetailVisible = false;
            SelectedArtist = null;
        }
        
        // 刷新数据
        public void Refresh()
        {
            Task.Run(LoadArtistsAsync);
        }
    }
} 