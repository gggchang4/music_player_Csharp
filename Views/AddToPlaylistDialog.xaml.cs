using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using GalaSoft.MvvmLight.Command;

namespace MusicPlayerApp.Views
{
    public partial class AddToPlaylistDialog : Window
    {
        private readonly Song _song;
        private readonly MediaLibraryService _libraryService;
        private ObservableCollection<PlaylistViewModel> _playlists;
        
        public ICommand ConfirmCommand { get; private set; }
        public ICommand CreatePlaylistCommand { get; private set; }
        
        public AddToPlaylistDialog(List<Playlist> playlists, Song song)
        {
            InitializeComponent();
            
            _song = song ?? throw new ArgumentNullException(nameof(song));
            _libraryService = MusicPlayerApp.Services.ServiceLocator.Instance.GetService<MediaLibraryService>();
            
            // 创建视图模型列表
            _playlists = new ObservableCollection<PlaylistViewModel>();
            foreach (var playlist in playlists)
            {
                _playlists.Add(new PlaylistViewModel
                {
                    Id = playlist.Id,
                    Title = playlist.Title,
                    Description = playlist.Description,
                    SongsCount = playlist.PlaylistSongs?.Count ?? 0
                });
            }
            
            PlaylistsListView.ItemsSource = _playlists;
            
            // 使用Lambda表达式修复方法组转换问题
            ConfirmCommand = new RelayCommand(() => ExecuteConfirm(), () => CanExecuteConfirm());
            CreatePlaylistCommand = new RelayCommand(() => ExecuteCreatePlaylist());
            
            // 设置数据上下文为当前窗口
            DataContext = this;
        }
        
        private bool CanExecuteConfirm()
        {
            return PlaylistsListView.SelectedItem != null;
        }
        
        private void ExecuteConfirm()
        {
            ConfirmAddToPlaylist();
        }
        
        private void ExecuteCreatePlaylist()
        {
            CreateNewPlaylist();
        }
        
        private async void ConfirmAddToPlaylist()
        {
            var selectedPlaylist = PlaylistsListView.SelectedItem as PlaylistViewModel;
            if (selectedPlaylist == null) return;
            
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await _libraryService.AddSongToPlaylistAsync(selectedPlaylist.Id, _song.Id);
                DialogResult = true;
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
            try
            {
                // 创建简单的输入对话框
                var inputDialog = new Window
                {
                    Title = "创建播放列表",
                    Width = 350,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = SystemColors.ControlBrush
                };

                var grid = new Grid { Margin = new Thickness(15) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = "请输入播放列表名称:",
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var textBox = new TextBox
                {
                    Text = $"我的歌单 {DateTime.Now:yyyy-MM-dd}",
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var okButton = new Button
                {
                    Content = "确定",
                    IsDefault = true,
                    Width = 80,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    IsCancel = true,
                    Width = 80
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                Grid.SetRow(label, 0);
                Grid.SetRow(textBox, 1);
                Grid.SetRow(buttonPanel, 2);

                grid.Children.Add(label);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                bool? result = inputDialog.ShowDialog();
                if (result != true || string.IsNullOrWhiteSpace(textBox.Text))
                    return;

                string playlistName = textBox.Text.Trim();
                
                Mouse.OverrideCursor = Cursors.Wait;
                
                // 创建播放列表并添加歌曲
                var playlist = await _libraryService.CreatePlaylistAsync(playlistName, 1); // 假设用户ID为1
                await _libraryService.AddSongToPlaylistAsync(playlist.Id, _song.Id);
                
                // 添加新创建的播放列表到列表并选中
                var newPlaylistVM = new PlaylistViewModel
                {
                    Id = playlist.Id,
                    Title = playlist.Title,
                    Description = playlist.Description,
                    SongsCount = 1 // 刚添加了一首歌曲
                };
                
                _playlists.Add(newPlaylistVM);
                PlaylistsListView.SelectedItem = newPlaylistVM;
                
                DialogResult = true;
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
        
        // 播放列表视图模型，用于显示在列表中
        public class PlaylistViewModel
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public int SongsCount { get; set; }
        }
    }
} 