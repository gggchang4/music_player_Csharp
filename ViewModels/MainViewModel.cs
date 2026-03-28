using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MusicPlayerApp.Helpers;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using RelayCommand = GalaSoft.MvvmLight.Command.RelayCommand;
using GalaSoft.MvvmLight.Messaging;
using MaterialDesignThemes.Wpf;
using MusicPlayerApp.Views;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // 字段
        private readonly MediaPlayerService _playerService;
        private readonly MediaLibraryService _libraryService;
        private readonly UserService _userService;

        // 当前用户
        private User _currentUser;

        // 视图管理
        private object _currentView;

        // 导航
        private int _selectedNavigationIndex;

        // 播放列表
        private ObservableCollection<Playlist> _playlists;
        private Playlist _selectedPlaylist;

        // 搜索
        private string _searchQuery;

        // 播放状态
        private Song _currentSong;
        private bool _isPlaying;
        private bool _isShuffleEnabled;
        private RepeatMode _repeatMode = RepeatMode.None;
        private bool _isMuted;
        private int _volume;
        private TimeSpan _currentPosition;
        private TimeSpan _totalTime;
        private int _currentPositionSeconds;
        private int _totalSeconds;
        private string _playPauseIcon;

        // 歌词
        private LyricFile _currentLyric;
        private ObservableCollection<LyricLine> _lyricLines;

        // 导航到我喜欢的音乐
        private object _navigatingLock = new object();
        private DateTime _lastFavoriteNavigationTime = DateTime.MinValue;

        // 导航到播放列表
        private DateTime _lastPlaylistNavigationTime = DateTime.MinValue;

        // 搜索结果
        private ObservableCollection<Song> _searchResults;
        private ObservableCollection<Artist> _artists;
        private ObservableCollection<Album> _albums;

        // 加载状态
        private bool _isLoading;

        // 命令
        public ICommand NavigateToAllMusicCommand { get; }
        public ICommand NavigateToArtistsCommand { get; }
        public ICommand NavigateToAlbumsCommand { get; }
        public ICommand NavigateToFavoriteSongsCommand { get; }
        public ICommand NavigateToFavoriteAlbumsCommand { get; }
        public ICommand CreatePlaylistCommand { get; }
        public ICommand ImportMusicCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand NextTrackCommand { get; }
        public ICommand PreviousTrackCommand { get; }
        public ICommand ToggleShuffleCommand { get; }
        public ICommand ToggleRepeatCommand { get; }
        public ICommand ToggleMuteCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenUserProfileCommand { get; }
        public ICommand ImportSongCommand { get; }
        public ICommand ImportFolderCommand { get; }
        public ICommand OpenPlaylistsCommand { get; }
        public ICommand NavigateToArtistDetailsCommand { get; }
        public ICommand NavigateToAlbumDetailsCommand { get; }
        public ICommand AddTestDataCommand { get; }
        public ICommand FixDatabaseCommand { get; }
        public ICommand OpenEqualizerCommand { get; }

        // 属性
        public object CurrentView
        {
            get => _currentView;
            set => Set(ref _currentView, value);
        }

        public int SelectedNavigationIndex
        {
            get => _selectedNavigationIndex;
            set
            {
                if (Set(ref _selectedNavigationIndex, value))
                {
                    // 根据选中项更新当前视图
                    switch (value)
                    {
                        case 0: // 所有音乐
                            NavigateToAllMusic();
                            break;
                        case 1: // 艺术家
                            NavigateToArtists();
                            break;
                        case 2: // 专辑
                            NavigateToAlbums();
                            break;
                        case 3: // 我的收藏
                            NavigateToFavoriteSongs();
                            break;
                    }
                }
            }
        }

        public ObservableCollection<Playlist> Playlists
        {
            get => _playlists;
            set => Set(ref _playlists, value);
        }

        public Playlist SelectedPlaylist
        {
            get { return _selectedPlaylist; }
            set
            {
                if (Set(ref _selectedPlaylist, value) && value != null)
                {
                    // 当选中播放列表项时直接触发导航
                    NavigateToPlaylist(value);
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => Set(ref _searchQuery, value);
        }

        public Song CurrentSong
        {
            get => _currentSong;
            set => Set(ref _currentSong, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => Set(ref _isPlaying, value);
        }

        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (Set(ref _isShuffleEnabled, value))
                {
                    _playerService.IsShuffled = value;
                }
            }
        }

        public bool IsRepeatEnabled
        {
            get => _repeatMode != RepeatMode.None;
            set
            {
                if (value)
                {
                    if (_repeatMode == RepeatMode.None)
                    {
                        _repeatMode = RepeatMode.All; // 启用时默认为列表循环
                    }
                }
                else
                {
                    _repeatMode = RepeatMode.None;
                }
                
                _playerService.RepeatMode = _repeatMode;
                OnPropertyChanged(nameof(IsRepeatEnabled));
                OnPropertyChanged(nameof(RepeatModeIcon));
                OnPropertyChanged(nameof(RepeatModeToolTip));
            }
        }
        
        public string RepeatModeIcon
        {
            get
            {
                switch (_repeatMode)
                {
                    case RepeatMode.One:
                        return "RepeatOne";
                    case RepeatMode.All:
                        return "Repeat";
                    default:
                        return "RepeatOff";
                }
            }
        }
        
        public string RepeatModeToolTip
        {
            get
            {
                switch (_repeatMode)
                {
                    case RepeatMode.One:
                        return "单曲循环";
                    case RepeatMode.All:
                        return "列表循环";
                    default:
                        return "关闭循环";
                }
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (Set(ref _isMuted, value))
                {
                    _playerService.IsMuted = value;
                }
            }
        }

        public int Volume
        {
            get => _volume;
            set
            {
                if (Set(ref _volume, value))
                {
                    _playerService.Volume = value;

                    // 如果设置了音量，自动取消静音
                    if (value > 0 && _isMuted)
                    {
                        IsMuted = false;
                    }
                }
            }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set => Set(ref _currentPosition, value);
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set => Set(ref _totalTime, value);
        }

        public int CurrentPositionSeconds
        {
            get => _currentPositionSeconds;
            set
            {
                if (Set(ref _currentPositionSeconds, value))
                {
                    // 用户手动拖动进度条
                    _playerService.SetPosition(TimeSpan.FromSeconds(value));
                }
            }
        }

        public int TotalSeconds
        {
            get => _totalSeconds;
            set => Set(ref _totalSeconds, value);
        }

        public string CurrentPositionText => CurrentPosition.ToString(@"mm\:ss");

        public string TotalTimeText => TotalTime.ToString(@"mm\:ss");

        public string PlayPauseIcon
        {
            get => _playPauseIcon;
            set => Set(ref _playPauseIcon, value);
        }

        public LyricFile CurrentLyric
        {
            get => _currentLyric;
            set => Set(ref _currentLyric, value);
        }

        public ObservableCollection<LyricLine> LyricLines
        {
            get => _lyricLines;
            set => Set(ref _lyricLines, value);
        }

        public ObservableCollection<Song> SearchResults
        {
            get => _searchResults;
            set => Set(ref _searchResults, value);
        }

        public ObservableCollection<Artist> Artists
        {
            get => _artists;
            set => Set(ref _artists, value);
        }

        public ObservableCollection<Album> Albums
        {
            get => _albums;
            set => Set(ref _albums, value);
        }

        // 加载状态
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        // 构造函数
        public MainViewModel(
            MediaLibraryService libraryService, 
            MediaPlayerService playerService, 
            UserService userService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));

            // 初始化播放器状态属性
            _volume = playerService.Volume;
            _isMuted = playerService.IsMuted;
            _isShuffleEnabled = playerService.IsShuffled;
            _repeatMode = playerService.RepeatMode;
            _playPauseIcon = "Play";

            // 初始化集合
            _playlists = new ObservableCollection<Playlist>();
            _lyricLines = new ObservableCollection<LyricLine>();
            _currentLyric = new LyricFile();
            _searchResults = new ObservableCollection<Song>();
            _artists = new ObservableCollection<Artist>();
            _albums = new ObservableCollection<Album>();

            // 初始化命令
            NavigateToAllMusicCommand = new RelayCommand(NavigateToAllMusic);
            NavigateToArtistsCommand = new RelayCommand(NavigateToArtists);
            NavigateToAlbumsCommand = new RelayCommand(NavigateToAlbums);
            NavigateToFavoriteSongsCommand = new RelayCommand(NavigateToFavoriteSongs);
            NavigateToFavoriteAlbumsCommand = new RelayCommand(NavigateToFavoriteAlbums);
            CreatePlaylistCommand = new RelayCommand(CreatePlaylist);
            ImportMusicCommand = new RelayCommand(ImportMusic);
            SearchCommand = new RelayCommand<string>(Search);
            PlayPauseCommand = new RelayCommand(PlayPause);
            NextTrackCommand = new RelayCommand(NextTrack);
            PreviousTrackCommand = new RelayCommand(PreviousTrack);
            ToggleShuffleCommand = new RelayCommand(ToggleShuffle);
            ToggleRepeatCommand = new RelayCommand(ToggleRepeat);
            ToggleMuteCommand = new RelayCommand(ToggleMute);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenUserProfileCommand = new RelayCommand(OpenUserProfile);
            ImportSongCommand = new RelayCommand(ImportSong);
            ImportFolderCommand = new RelayCommand(ImportFolder);
            OpenPlaylistsCommand = new RelayCommand(OpenPlaylists);
            NavigateToArtistDetailsCommand = new RelayCommand<Artist>(NavigateToArtistDetails);
            NavigateToAlbumDetailsCommand = new RelayCommand<Album>(NavigateToAlbumDetails);
            AddTestDataCommand = new RelayCommand(AddTestData);
            FixDatabaseCommand = new RelayCommand(FixDatabase);
            OpenEqualizerCommand = new RelayCommand(OpenEqualizer);

            // 注册事件
            _playerService.SongChanged += PlayerService_SongChanged;
            _playerService.PlaybackStateChanged += PlayerService_PlaybackStateChanged;
            _playerService.PlaybackPositionChanged += PlayerService_PlaybackPositionChanged;

            // 默认导航到所有音乐
            SelectedNavigationIndex = 0;

            // 初始化播放器状态 - 确保图标正确显示
            PlayPauseIcon = "Play";
            Volume = 50;

            // 注册消息处理器
            RegisterMessages();
            
            // 加载用户数据
            LoadUserDataAsync();
            
            // 强制导航到所有音乐视图，确保正确显示歌曲
            App.Logger.Info("强制初始化所有音乐视图");
            NavigateToAllMusic();
        }

        // 异步加载用户数据
        private async void LoadUserDataAsync()
        {
            try
            {
                // 尝试自动登录
                _currentUser = await _userService.AutoLoginAsync();

                if (_currentUser == null)
                {
                    // 如果自动登录失败，可以显示登录界面
                    // TODO: 实现登录界面

                    // 临时使用默认用户
                    _currentUser = await _userService.LoginAsync("DefaultUser", "password");
                }

                // 加载用户设置
                if (_currentUser.Settings != null)
                {
                    Volume = _currentUser.Settings.Volume;
                    IsShuffleEnabled = _currentUser.Settings.Shuffle;
                    _repeatMode = _currentUser.Settings.RepeatMode;
                    _playerService.RepeatMode = _repeatMode;
                    OnPropertyChanged(nameof(IsRepeatEnabled));
                    OnPropertyChanged(nameof(RepeatModeIcon));
                    OnPropertyChanged(nameof(RepeatModeToolTip));
                }

                // 加载播放列表
                await LoadPlaylistsAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载用户数据失败");
                MessageBox.Show($"加载用户数据失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 加载播放列表
        private async Task LoadPlaylistsAsync()
        {
            try
            {
                App.Logger.Info("开始重新加载播放列表");
                
                // 确保"我喜欢的音乐"播放列表与收藏同步
                await _libraryService.SyncFavoritesToPlaylistAsync(_currentUser.Id);
                
                // 获取所有播放列表
                var playlists = await _libraryService.GetUserPlaylistsAsync(_currentUser.Id);

                // 使用Dispatcher确保在UI线程上执行集合更新
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 彻底清空播放列表集合
                    Playlists.Clear();
                    
                    // 重新添加所有播放列表
                    foreach (var playlist in playlists)
                    {
                        Playlists.Add(playlist);
                    }
                    
                    // 触发集合变更通知
                    OnPropertyChanged(nameof(Playlists));
                    
                    App.Logger.Info($"播放列表已刷新，当前数量: {Playlists.Count}");
                });
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载播放列表失败");
                MessageBox.Show($"加载播放列表失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 导航方法
        private void NavigateToAllMusic()
        {
            try 
            {
                App.Logger.Info("导航到\"所有音乐\"视图");
                
                // 创建AllMusicUserControl实例，它会自己创建和设置视图模型
                var allMusicView = new Views.AllMusicUserControl();
                
                // 设置为当前视图
                CurrentView = allMusicView;
                
                // 强制触发属性变更通知，确保UI更新
                OnPropertyChanged(nameof(CurrentView));
                
                App.Logger.Info("完成导航到AllMusicUserControl");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "导航到所有音乐视图时出错");
            }
        }

        private void NavigateToArtists()
        {
            // 使用新创建的艺术家视图
            CurrentView = new Views.ArtistsUserControl();
        }

        private void NavigateToAlbums()
        {
            var albumsViewModel = new AlbumsViewModel(_libraryService, _playerService);
            var albumsView = new Views.AlbumsUserControl { DataContext = albumsViewModel };
            CurrentView = albumsView;
        }

        private void NavigateToFavoriteSongs()
        {
            try
            {
                // 限制点击频率
                DateTime now = DateTime.Now;
                if ((now - _lastFavoriteNavigationTime).TotalMilliseconds < 500)
                {
                    App.Logger.Info("点击过于频繁，忽略此次请求");
                    return;
                }
                _lastFavoriteNavigationTime = now;
                
                App.Logger.Info("导航到'我喜欢的音乐'视图");
                
                // 创建新的收藏视图模型并设置数据上下文
                var favoritesViewModel = new FavoritesViewModel(_libraryService, _playerService, _userService);
                var favoritesView = new UserControls.FavoritesUserControl { DataContext = favoritesViewModel };
                CurrentView = favoritesView;
                
                // 触发FavoritesChanged消息以刷新收藏视图
                Messenger.Default.Send(new NotificationMessage("FavoritesChanged"));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "导航到'我喜欢的音乐'视图失败");
                MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToFavoriteAlbums()
        {
            try
            {
                // 限制点击频率
                DateTime now = DateTime.Now;
                if ((now - _lastFavoriteNavigationTime).TotalMilliseconds < 500)
                {
                    App.Logger.Info("点击收藏专辑过于频繁，忽略此次请求");
                    return;
                }
                _lastFavoriteNavigationTime = now;
                
                App.Logger.Info("导航到收藏专辑视图");
                
                // 创建收藏专辑视图模型并设置数据上下文
                try
                {
                    var favoriteAlbumsViewModel = new FavoriteAlbumsViewModel(_libraryService, _playerService, _userService);
                    var favoriteAlbumsView = new UserControls.FavoriteAlbumsUserControl();
                    
                    // 在设置DataContext前先设置CurrentView，避免UI线程不安全
                    CurrentView = favoriteAlbumsView;
                    
                    // 设置数据上下文
                    App.Current.Dispatcher.InvokeAsync(() => {
                        favoriteAlbumsView.DataContext = favoriteAlbumsViewModel;
                    });
                }
                catch (Exception innerEx)
                {
                    App.Logger.Error(innerEx, "创建收藏专辑视图模型失败");
                    MessageBox.Show($"创建收藏专辑视图失败: {innerEx.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "导航到收藏专辑视图失败");
                MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToPlaylist(Playlist playlist)
        {
            try
            {
                // 限制点击频率
                DateTime now = DateTime.Now;
                if ((now - _lastPlaylistNavigationTime).TotalMilliseconds < 500)
                {
                    App.Logger.Info("点击播放列表过于频繁，忽略此次请求");
                    return;
                }
                _lastPlaylistNavigationTime = now;
                
                App.Logger.Info($"导航到播放列表: {playlist?.Title ?? "null"}");
                
                if (playlist == null)
                {
                    App.Logger.Warn("无法导航：播放列表为null");
                    return;
                }
                
                // 处理"我喜欢的音乐"播放列表
                if (playlist.Title == "我喜欢的音乐")
                {
                    // 导航到我喜欢的音乐视图
                    NavigateToFavoriteSongs();
                    return;
                }
                
                // 处理其他播放列表
                CurrentView = new MusicPlayerApp.Views.PlaylistContentUserControl(playlist);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导航到播放列表失败: {playlist?.Title ?? "null"}");
                MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 创建播放列表
        private void CreatePlaylist()
        {
            try
            {
                // 导航到创建播放列表页面
                CurrentView = new MusicPlayerApp.Views.CreatePlaylistUserControl();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "导航到创建播放列表界面失败");
                MessageBox.Show($"导航失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 导入音乐
        private async void ImportMusic()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "音频文件|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.wma|所有文件|*.*",
                    Multiselect = true,
                    Title = "选择要导入的音乐文件"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePaths = openFileDialog.FileNames;
                    if (filePaths.Length > 0)
                    {
                        foreach (var filePath in filePaths)
                        {
                            try
                            {
                                await _libraryService.ImportSongAsync(filePath);
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Warn(ex, $"导入文件失败: {filePath}");
                                // 继续处理下一个文件
                            }
                        }

                        MessageBox.Show($"成功导入 {filePaths.Length} 个文件", "导入完成",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // 刷新当前视图
                        if (CurrentView is Views.AllMusicUserControl allMusicView)
                        {
                            allMusicView.Refresh();
                        }
                        else
                        {
                            // 如果当前不是AllMusicUserControl，切换到"所有音乐"视图
                            NavigateToAllMusic();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "导入音乐失败");
                MessageBox.Show($"导入音乐失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 搜索
        private void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            try
            {
                App.Logger.Info($"执行搜索: {query}");
                
                // 创建并设置搜索结果视图
                var searchResultsView = new MusicPlayerApp.Views.SearchResultsUserControl(query);
                CurrentView = searchResultsView;
                
                // 通知UI更新
                OnPropertyChanged(nameof(CurrentView));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"搜索过程中发生错误: {ex.Message}");
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 播放控制
        private async void PlayPause()
        {
            if (_playerService.State == PlaybackState.Playing)
            {
                _playerService.Pause();
            }
            else if (_playerService.State == PlaybackState.Paused)
            {
                _playerService.Resume();
            }
            else if (CurrentSong != null)
            {
                try 
                {
                    await _playerService.PlayAsync(CurrentSong);
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"播放歌曲失败: {CurrentSong.Title}");
                }
            }
        }

        private async void NextTrack()
        {
            try
            {
                if (_playerService.Playlist.Count == 0)
                {
                    // 如果播放列表为空，尝试加载所有音乐
                    var songs = await _libraryService.SearchSongsAsync("");
                    if (songs.Count > 0)
                    {
                        _playerService.SetPlaylist(songs);
                        await _playerService.PlayNextAsync();
                    }
                    else
                    {
                        MessageBox.Show("没有可播放的音乐，请先导入音乐", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // 播放下一曲
                    await _playerService.PlayNextAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放下一曲失败");
                MessageBox.Show($"播放下一曲失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void PreviousTrack()
        {
            try
            {
                if (_playerService.Playlist.Count == 0)
                {
                    // 如果播放列表为空，尝试加载所有音乐
                    var songs = await _libraryService.SearchSongsAsync("");
                    if (songs.Count > 0)
                    {
                        _playerService.SetPlaylist(songs);
                        await _playerService.PlayPreviousAsync();
                    }
                    else
                    {
                        MessageBox.Show("没有可播放的音乐，请先导入音乐", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // 播放上一曲
                    await _playerService.PlayPreviousAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放上一曲失败");
                MessageBox.Show($"播放上一曲失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        }

        private void ToggleRepeat()
        {
            // 在三种循环模式之间切换: None -> All -> One -> None
            switch (_repeatMode)
            {
                case RepeatMode.None:
                    _repeatMode = RepeatMode.All; // 列表循环
                    break;
                case RepeatMode.All:
                    _repeatMode = RepeatMode.One; // 单曲循环
                    break;
                case RepeatMode.One:
                    _repeatMode = RepeatMode.None; // 关闭循环
                    break;
            }
            
            _playerService.RepeatMode = _repeatMode;
            OnPropertyChanged(nameof(IsRepeatEnabled));
            OnPropertyChanged(nameof(RepeatModeIcon));
            OnPropertyChanged(nameof(RepeatModeToolTip));
        }

        private void ToggleMute()
        {
            _playerService.IsMuted = !_playerService.IsMuted;
            IsMuted = _playerService.IsMuted;
        }

        // 设置和用户配置
        private void OpenSettings()
        {
            // 打开设置窗口
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.ShowDialog();
        }

        private void OpenUserProfile()
        {
            // 打开用户资料窗口
            var profileWindow = new Views.UserProfileWindow();
            profileWindow.ShowDialog();
        }

        private void OpenEqualizer()
        {
            // 打开均衡器窗口
            var equalizerWindow = new Views.EqualizerWindow();
            equalizerWindow.ShowDialog();
        }

        // 事件处理
        private async void PlayerService_SongChanged(object sender, SongChangedEventArgs e)
        {
            CurrentSong = e.Song;
            IsPlaying = true;
            
            try
            {
                // 加载歌词
                var lyricService = ServiceLocator.Instance.GetService<LyricService>();
                CurrentLyric = await lyricService.LoadLyricsForSongAsync(e.Song);
                
                // 更新歌词显示
                LyricLines.Clear();
                foreach (var line in CurrentLyric.Lines)
                {
                    LyricLines.Add(line);
                }
                
                // 确保歌词控件更新
                OnPropertyChanged(nameof(LyricLines));
                
                App.Logger.Info($"已加载歌词，行数: {LyricLines.Count}");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载歌词失败");
                // 确保在发生错误时歌词显示为空
                LyricLines.Clear();
                OnPropertyChanged(nameof(LyricLines));
            }
        }

        private void PlayerService_PlaybackStateChanged(object sender, PlaybackStateChangedEventArgs e)
        {
            IsPlaying = e.State == PlaybackState.Playing;
            PlayPauseIcon = e.State == PlaybackState.Playing ? "Pause" : "Play";
        }

        private void PlayerService_PlaybackPositionChanged(object sender, PlaybackPositionChangedEventArgs e)
        {
            try 
            {
                // 更新当前位置
                CurrentPosition = e.Position;
                
                // 使用整数秒
                CurrentPositionSeconds = (int)e.Position.TotalSeconds;
                TotalTime = _playerService.TotalTime;
                TotalSeconds = (int)_playerService.TotalTime.TotalSeconds;
    
                // 同步歌词，但在后台线程进行以避免UI卡顿
                Task.Run(() => 
                {
                    try 
                    {
                        // 在后台线程计算歌词同步
                        SyncLyrics(e.Position);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, "后台同步歌词失败");
                    }
                });
    
                // 更新文本显示
                OnPropertyChanged(nameof(CurrentPositionText));
                OnPropertyChanged(nameof(TotalTimeText));
            }
            catch (Exception ex) 
            {
                App.Logger.Error(ex, "更新播放位置时出错");
            }
        }

        // 添加歌词同步方法
        private void SyncLyrics(TimeSpan position)
        {
            if (CurrentLyric == null || CurrentLyric.Lines.Count == 0)
                return;

            CurrentLyric.UpdateCurrentLine(position);
            
            // 如果LyricLines集合为空，或者与CurrentLyric.Lines不同，则更新整个集合
            if (LyricLines.Count != CurrentLyric.Lines.Count)
            {
                LyricLines.Clear();
                foreach (var line in CurrentLyric.Lines)
                {
                    LyricLines.Add(line);
                }
            }
            else
            {
                // 否则只更新IsCurrent状态
                for (int i = 0; i < LyricLines.Count; i++)
                {
                    if (LyricLines[i].IsCurrent != CurrentLyric.Lines[i].IsCurrent)
                    {
                        LyricLines[i].IsCurrent = CurrentLyric.Lines[i].IsCurrent;
                        OnPropertyChanged($"LyricLines[{i}].IsCurrent");
                    }
                }
            }
        }

        // 简单的空视图模型，用于显示调试视图时临时替换主视图
        public class EmptyViewModel
        {
            // 这是一个简单类，不再继承ViewModelBase
        }

        /// <summary>
        /// 强制导航到指定播放列表，即使它已经被选中
        /// </summary>
        public void ForceNavigateToPlaylist(Playlist playlist)
        {
            try
            {
                App.Logger.Info($"强制导航到播放列表: {playlist?.Title ?? "null"}");
                
                // 直接调用NavigateToPlaylist方法，不依赖于SelectedPlaylist属性的变更检测
                NavigateToPlaylist(playlist);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"强制导航到播放列表失败: {playlist?.Title ?? "null"}");
            }
        }

        // 注册消息处理器
        private void RegisterMessages()
        {
            // 确保先注销可能存在的旧消息处理器
            Messenger.Default.Unregister<NotificationMessage>(this);
            Messenger.Default.Unregister<Artist>(this);
            Messenger.Default.Unregister<Album>(this);
            
            // 注册消息处理器
            Messenger.Default.Register<NotificationMessage>(this, async message =>
            {
                App.Logger.Info($"收到消息: {message.Notification}");
                
                if (message.Notification == "PlaylistsChanged")
                {
                    App.Logger.Info("主窗口处理PlaylistsChanged消息");
                    
                    // 当收到播放列表已更改的消息时，立即重新加载播放列表
                    await LoadPlaylistsAsync();
                    
                    // 如果当前选中的播放列表已被删除，清除选择
                    if (_selectedPlaylist != null)
                    {
                        bool playlistExists = false;
                        foreach (var playlist in Playlists)
                        {
                            if (playlist.Id == _selectedPlaylist.Id)
                            {
                                playlistExists = true;
                                break;
                            }
                        }
                        
                        if (!playlistExists)
                        {
                            App.Logger.Info($"选中的播放列表已不存在，ID: {_selectedPlaylist.Id}");
                            _selectedPlaylist = null;
                            OnPropertyChanged(nameof(SelectedPlaylist));
                        }
                    }
                }
                else if (message.Notification == "NavigateToPlaylists")
                {
                    // 返回到播放列表视图
                    if (_selectedPlaylist != null)
                    {
                        NavigateToPlaylist(_selectedPlaylist);
                    }
                    else
                    {
                        NavigateToAllMusic(); // 如果没有选中的播放列表，返回到所有音乐
                    }
                }
                else if (message.Notification == "NavigateToAllMusic")
                {
                    // 导航到所有音乐视图
                    NavigateToAllMusic();
                }
            });
            
            // 注册艺术家导航消息处理
            Messenger.Default.Register<Artist>(this, "NavigateToArtist", artist => 
            {
                App.Logger.Info($"收到导航到艺术家消息: {artist.Name}");
                NavigateToArtistDetails(artist);
            });
            
            // 注册专辑导航消息处理
            Messenger.Default.Register<Album>(this, "NavigateToAlbum", album => 
            {
                App.Logger.Info($"收到导航到专辑消息: {album.Title}");
                NavigateToAlbumDetails(album);
            });
        }
        
        // 导航到艺术家详情页面
        private void NavigateToArtistDetails(Artist artist)
        {
            try
            {
                App.Logger.Info($"导航到艺术家详情: {artist.Name}");
                
                // 创建艺术家详情视图模型
                var artistDetailsViewModel = new MusicPlayerApp.ViewModels.ArtistDetailsViewModel(artist, _libraryService, _playerService, _userService);
                
                // 创建艺术家详情视图并设置数据上下文
                var artistDetailsView = new MusicPlayerApp.Views.ArtistDetailsUserControl
                {
                    DataContext = artistDetailsViewModel
                };
                
                // 更新当前视图
                CurrentView = artistDetailsView;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导航到艺术家详情失败: {artist.Name}");
                MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 导航到专辑详情页面
        private void NavigateToAlbumDetails(Album album)
        {
            try
            {
                App.Logger.Info($"导航到专辑详情: {album.Title}");
                
                // 创建专辑详情视图模型
                var albumDetailsViewModel = new MusicPlayerApp.ViewModels.AlbumDetailsViewModel(album, _libraryService, _playerService, _userService);
                
                // 创建专辑详情视图并设置数据上下文
                var albumDetailsView = new MusicPlayerApp.Views.AlbumDetailsUserControl
                {
                    DataContext = albumDetailsViewModel
                };
                
                // 更新当前视图
                CurrentView = albumDetailsView;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导航到专辑详情失败: {album.Title}");
                MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportSong()
        {
            // Implementation of ImportSong method
        }

        private void ImportFolder()
        {
            // Implementation of ImportFolder method
        }

        private void OpenPlaylists()
        {
            // Implementation of OpenPlaylists method
        }

        // 添加测试数据
        private async void AddTestData()
        {
            try
            {
                IsLoading = true;
                await _libraryService.AddTestDataAsync();
                
                // 刷新艺术家和专辑列表
                await LoadArtistsAndAlbumsAsync();
                
                IsLoading = false;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "添加测试数据失败");
                MessageBox.Show($"添加测试数据失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
        
        // 修复数据库
        private void FixDatabase()
        {
            try
            {
                // 获取数据库路径
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayerApp");
                string dbPath = Path.Combine(appDataPath, "musicplayer.db");
                
                if (File.Exists(dbPath))
                {
                    // 备份数据库
                    string backupPath = Path.Combine(appDataPath, $"musicplayer_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                    File.Copy(dbPath, backupPath);
                    
                    // 删除现有数据库
                    File.Delete(dbPath);
                    
                    MessageBox.Show($"数据库已重置，原数据库已备份到: {backupPath}", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // 提示用户重启应用
                    if (MessageBox.Show("请重启应用程序以重建数据库。要现在退出吗？", "提示", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "修复数据库失败");
                MessageBox.Show($"修复数据库失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 加载艺术家和专辑
        private async Task LoadArtistsAndAlbumsAsync()
        {
            try
            {
                IsLoading = true;
                
                // 加载所有艺术家
                var artists = await _libraryService.GetAllArtistsAsync();
                Artists.Clear();
                foreach (var artist in artists)
                {
                    Artists.Add(artist);
                }
                
                // 加载所有专辑
                var albums = await _libraryService.GetAllAlbumsAsync();
                Albums.Clear();
                foreach (var album in albums)
                {
                    Albums.Add(album);
                }
                
                App.Logger.Info($"已加载 {Artists.Count} 位艺术家和 {Albums.Count} 张专辑");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载艺术家和专辑失败");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}