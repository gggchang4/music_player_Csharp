using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.ViewModels
{
    public class CreatePlaylistViewModel : ViewModelBase
    {
        private readonly MediaLibraryService _libraryService;
        private readonly UserService _userService;
        
        private string _playlistName;
        private string _playlistDescription;
        private string _playlistTags;
        private bool _isBusy;
        
        public string PlaylistName
        {
            get => _playlistName;
            set => Set(ref _playlistName, value);
        }
        
        public string PlaylistDescription
        {
            get => _playlistDescription;
            set => Set(ref _playlistDescription, value);
        }
        
        public string PlaylistTags
        {
            get => _playlistTags;
            set => Set(ref _playlistTags, value);
        }
        
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }
        
        public ICommand CreatePlaylistCommand { get; }
        
        public CreatePlaylistViewModel(MediaLibraryService libraryService, UserService userService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            
            // 初始化默认值
            PlaylistName = $"我的歌单 {DateTime.Now:yyyy-MM-dd}";
            PlaylistDescription = "";
            PlaylistTags = "";
            
            // 创建命令
            CreatePlaylistCommand = new RelayCommand(ExecuteCreatePlaylist, CanExecuteCreatePlaylist);
        }
        
        private bool CanExecuteCreatePlaylist()
        {
            return !string.IsNullOrWhiteSpace(PlaylistName) && !IsBusy;
        }
        
        private async void ExecuteCreatePlaylist()
        {
            if (string.IsNullOrWhiteSpace(PlaylistName))
            {
                MessageBox.Show("请输入播放列表名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                IsBusy = true;
                
                // 获取当前用户
                var currentUser = _userService.CurrentUser;
                if (currentUser == null)
                {
                    try
                    {
                        // 尝试登录默认用户
                        currentUser = await _userService.LoginAsync("DefaultUser", "password");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("无法创建播放列表，请确保您已登录", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        App.Logger.Error(ex, "创建播放列表时登录失败");
                        return;
                    }
                }
                
                // 检查是否存在同名播放列表
                string playlistName = PlaylistName.Trim();
                if (await _libraryService.PlaylistNameExistsAsync(playlistName, currentUser.Id))
                {
                    MessageBox.Show("已存在同名播放列表，请更换名称后重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 创建播放列表
                var playlist = new Playlist
                {
                    Title = playlistName,
                    Description = PlaylistDescription?.Trim(),
                    UserId = currentUser.Id,
                    AddedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };
                
                // 存储标签信息供未来扩展使用
                string tags = PlaylistTags?.Trim();
                
                var createdPlaylist = await _libraryService.CreatePlaylistAsync(playlist.Title, currentUser.Id, playlist.Description);
                
                MessageBox.Show($"播放列表 \"{playlist.Title}\" 创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 重置表单
                ResetForm();
                
                // 发送消息通知播放列表已创建
                Messenger.Default.Send(new NotificationMessage("PlaylistsChanged"));
                
                // 导航至播放列表页面
                Messenger.Default.Send(new NotificationMessage("NavigateToPlaylists"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建播放列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                App.Logger.Error(ex, "创建播放列表失败");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private void ResetForm()
        {
            PlaylistName = $"我的歌单 {DateTime.Now:yyyy-MM-dd}";
            PlaylistDescription = "";
            PlaylistTags = "";
        }
    }
} 