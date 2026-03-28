using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System.ComponentModel;

namespace MusicPlayerApp.Views
{
    public partial class SelectPlaylistDialog : Window
    {
        private readonly Song _song;
        private readonly MediaLibraryService _libraryService;
        private readonly UserService _userService;
        private ObservableCollection<PlaylistViewModel> _playlists;
        
        public ICommand ConfirmCommand { get; private set; }
        public ICommand CreatePlaylistCommand { get; private set; }
        
        public SelectPlaylistDialog(List<Playlist> playlists, Song song)
        {
            InitializeComponent();
            
            _song = song ?? throw new ArgumentNullException(nameof(song));
            _libraryService = ServiceLocator.Instance.GetService<MediaLibraryService>();
            _userService = ServiceLocator.Instance.GetService<UserService>();
            
            // 创建视图模型列表
            _playlists = new ObservableCollection<PlaylistViewModel>();
            foreach (var playlist in playlists)
            {
                // 确保获取正确的歌曲数量
                int songsCount = 0;
                if (playlist.Title == "我喜欢的音乐")
                {
                    // 对于"我喜欢的音乐"播放列表，尝试从FavoriteSongs表获取数据
                    try
                    {
                        var favorites = _libraryService.GetFavoriteSongsAsync(_userService.CurrentUser.Id, false).Result;
                        songsCount = favorites?.Count ?? 0;
                    }
                    catch
                    {
                        // 如果出错，则使用播放列表本身的歌曲数量
                        songsCount = playlist.PlaylistSongs?.Count ?? 0;
                    }
                }
                else
                {
                    // 对于普通播放列表，使用播放列表自身的歌曲数量
                    songsCount = playlist.PlaylistSongs?.Count ?? 0;
                }

                _playlists.Add(new PlaylistViewModel
                {
                    Id = playlist.Id,
                    Title = playlist.Title,
                    Description = playlist.Description ?? "无描述",
                    SongsCount = songsCount
                });
            }
            
            PlaylistsListView.ItemsSource = _playlists;
            
            // 初始化命令 - 改用可以强制触发CanExecute更新的RelayCommand实现方式
            ConfirmCommand = new GalaSoft.MvvmLight.CommandWpf.RelayCommand(
                () => AddToSelectedPlaylist(), 
                () => PlaylistsListView.SelectedItem != null);
            CreatePlaylistCommand = new RelayCommand(ExecuteCreatePlaylist);
            
            // 确保初始状态下"添加"按钮是禁用的
            CommandManager.InvalidateRequerySuggested();
            
            // 设置数据上下文
            DataContext = this;
            
            // 设置默认播放列表名称
            NewPlaylistNameTextBox.Text = $"我的歌单 {DateTime.Now:yyyy-MM-dd}";
            
            // 设置添加按钮初始状态为禁用
            Loaded += (s, e) => {
                AddButton.IsEnabled = false;
            };
        }
        
        private void PlaylistsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 手动触发所有命令可执行状态的更新
            CommandManager.InvalidateRequerySuggested();
            
            // 调试信息
            Console.WriteLine($"播放列表选择已变更。当前选中：{(PlaylistsListView.SelectedItem != null ? "有项目" : "无项目")}");
            
            // 更新添加按钮状态
            AddButton.IsEnabled = PlaylistsListView.SelectedItem != null;
        }
        
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用添加到播放列表的方法
            AddToSelectedPlaylist();
        }
        
        private void ExecuteCreatePlaylist()
        {
            CreateNewPlaylist();
        }
        
        private async void AddToSelectedPlaylist()
        {
            var selectedPlaylist = PlaylistsListView.SelectedItem as PlaylistViewModel;
            if (selectedPlaylist == null) return;
            
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await _libraryService.AddSongToPlaylistAsync(selectedPlaylist.Id, _song.Id);
                
                // 更新UI中的歌曲计数
                selectedPlaylist.SongsCount += 1;
                
                // 发送消息通知主视图模型更新播放列表
                Messenger.Default.Send(new NotificationMessage("PlaylistsChanged"));
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加歌曲到播放列表失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        
        private async void CreateNewPlaylist()
        {
            string playlistName = NewPlaylistNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                MessageBox.Show("请输入播放列表名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                NewPlaylistNameTextBox.Focus();
                return;
            }
            
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    // 尝试自动登录默认用户
                    currentUser = await _userService.LoginAsync("DefaultUser", "password");
                }
                
                // 创建播放列表
                var playlist = await _libraryService.CreatePlaylistAsync(playlistName, currentUser.Id);
                
                // 添加歌曲到播放列表
                await _libraryService.AddSongToPlaylistAsync(playlist.Id, _song.Id);
                
                // 确保播放列表的PlaylistSongs集合已加载，并包含我们刚添加的歌曲
                // 这样SongsCount就可以正确显示为1
                int songsCount = playlist.PlaylistSongs?.Count ?? 0;
                if (songsCount == 0) songsCount = 1; // 我们刚添加了一首歌曲
                
                // 添加到UI列表
                var newPlaylistVM = new PlaylistViewModel
                {
                    Id = playlist.Id,
                    Title = playlist.Title,
                    Description = playlist.Description ?? "无描述",
                    SongsCount = songsCount // 确保显示正确的歌曲数量
                };
                
                _playlists.Add(newPlaylistVM);
                PlaylistsListView.SelectedItem = newPlaylistVM;
                
                // 清空输入框
                NewPlaylistNameTextBox.Text = string.Empty;
                
                // 发送消息通知主视图模型更新播放列表
                Messenger.Default.Send(new NotificationMessage("PlaylistsChanged"));
                
                MessageBox.Show($"已成功创建播放列表 \"{playlistName}\" 并添加歌曲", "成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建播放列表失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        
        // 播放列表视图模型
        public class PlaylistViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            
            private int _songsCount;
            public int SongsCount 
            { 
                get => _songsCount;
                set
                {
                    if (_songsCount != value)
                    {
                        _songsCount = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SongsCount)));
                    }
                }
            }
            
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
} 