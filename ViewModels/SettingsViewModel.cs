using System;
using System.Collections.Generic;
using System.Windows.Input;
using MusicPlayerApp.Services;
using MusicPlayerApp.Models;
using MusicPlayerApp.Commands;
using System.IO;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Diagnostics;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using System.ComponentModel;
using Newtonsoft.Json;

namespace MusicPlayerApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private readonly MediaPlayerService _playerService;

        #region 属性

        // 一般设置
        private int _defaultVolume;
        public int DefaultVolume
        {
            get => _defaultVolume;
            set
            {
                _defaultVolume = value;
                OnPropertyChanged();
                _playerService.SetVolume(value);
            }
        }

        private bool _autoPlayOnStartup;
        public bool AutoPlayOnStartup
        {
            get => _autoPlayOnStartup;
            set
            {
                _autoPlayOnStartup = value;
                OnPropertyChanged();
            }
        }

        private bool _rememberPlaybackPosition;
        public bool RememberPlaybackPosition
        {
            get => _rememberPlaybackPosition;
            set
            {
                _rememberPlaybackPosition = value;
                OnPropertyChanged();
            }
        }

        // 播放设置
        private bool _crossFade;
        public bool CrossFade
        {
            get => _crossFade;
            set
            {
                _crossFade = value;
                OnPropertyChanged();
                _playerService.EnableCrossFade = value;
            }
        }

        private int _crossFadeDuration;
        public int CrossFadeDuration
        {
            get => _crossFadeDuration;
            set
            {
                _crossFadeDuration = value;
                OnPropertyChanged();
                _playerService.CrossFadeDuration = value;
            }
        }

        private int _selectedAudioFormatIndex;
        public int SelectedAudioFormatIndex
        {
            get => _selectedAudioFormatIndex;
            set
            {
                _selectedAudioFormatIndex = value;
                OnPropertyChanged();
            }
        }

        // 显示设置
        private int _selectedThemeIndex;
        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                _selectedThemeIndex = value;
                OnPropertyChanged();
                ApplyTheme(value);
            }
        }

        private bool _showAnimations;
        public bool ShowAnimations
        {
            get => _showAnimations;
            set
            {
                _showAnimations = value;
                OnPropertyChanged();
            }
        }

        private bool _alwaysShowLyrics;
        public bool AlwaysShowLyrics
        {
            get => _alwaysShowLyrics;
            set
            {
                _alwaysShowLyrics = value;
                OnPropertyChanged();
            }
        }

        // 数据设置
        private string _musicLibraryPath;
        public string MusicLibraryPath
        {
            get => _musicLibraryPath;
            set
            {
                _musicLibraryPath = value;
                OnPropertyChanged();
            }
        }

        private int _cacheSize;
        public int CacheSize
        {
            get => _cacheSize;
            set
            {
                _cacheSize = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region 命令

        public ICommand BrowseFolderCommand { get; private set; }
        public ICommand ClearCacheCommand { get; private set; }
        public ICommand SaveSettingsCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }
        public ICommand ApplyCommand { get; private set; }
        public ICommand OpenMusicFolderCommand { get; private set; }
        public ICommand BrowseMusicFolderCommand { get; private set; }

        #endregion

        public SettingsViewModel(UserService userService, MediaPlayerService playerService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));

            // 初始化命令
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            ClearCacheCommand = new RelayCommand(ClearCache);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(() => { /* 取消操作在View中处理 */ });
            SaveCommand = new RelayCommand(SaveAndClose);
            ResetCommand = new RelayCommand(ResetSettings);
            ApplyCommand = new RelayCommand(ApplySettings);
            OpenMusicFolderCommand = new RelayCommand(OpenMusicFolder);
            BrowseMusicFolderCommand = new RelayCommand(BrowseMusicFolder);

            // 加载当前设置
            LoadSettings();
        }

        private void LoadSettings()
        {
            // 默认设置
            DefaultVolume = 50;
            AutoPlayOnStartup = false;
            RememberPlaybackPosition = true;
            CrossFade = true;
            CrossFadeDuration = 2;
            SelectedAudioFormatIndex = 0;
            SelectedThemeIndex = 0;
            ShowAnimations = true;
            AlwaysShowLyrics = false;
            MusicLibraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "MusicPlayer");
            CacheSize = 500;

            if (_userService.CurrentUser?.Settings == null)
            {
                return;
            }

            try
            {
                var settings = _userService.CurrentUser.Settings;

                // 加载设置 - 使用安全访问方式
                DefaultVolume = settings.Volume;
                
                // 尝试使用反射获取新字段，以适应旧数据库结构
                try { AutoPlayOnStartup = settings.AutoPlayOnStartup; } 
                catch { App.Logger.Warn("无法读取AutoPlayOnStartup设置，使用默认值"); }
                
                try { RememberPlaybackPosition = settings.RememberPlaybackPosition; } 
                catch { App.Logger.Warn("无法读取RememberPlaybackPosition设置，使用默认值"); }
                
                try { CrossFade = settings.CrossFade; } 
                catch { App.Logger.Warn("无法读取CrossFade设置，使用默认值"); }
                
                try { CrossFadeDuration = settings.CrossFadeDuration; } 
                catch { App.Logger.Warn("无法读取CrossFadeDuration设置，使用默认值"); }
                
                try { SelectedAudioFormatIndex = settings.AudioFormatIndex; } 
                catch { App.Logger.Warn("无法读取AudioFormatIndex设置，使用默认值"); }

                // 主题设置
                switch (settings.Theme?.ToLower())
                {
                    case "light":
                        SelectedThemeIndex = 1;
                        break;
                    case "system":
                        SelectedThemeIndex = 2;
                        break;
                    default:
                        SelectedThemeIndex = 0; // 默认深色
                        break;
                }

                // 其他设置 - 使用安全访问方式
                try { ShowAnimations = settings.ShowAnimations; } 
                catch { App.Logger.Warn("无法读取ShowAnimations设置，使用默认值"); }
                
                try { AlwaysShowLyrics = settings.AlwaysShowLyrics; } 
                catch { App.Logger.Warn("无法读取AlwaysShowLyrics设置，使用默认值"); }
                
                try
                {
                    if (!string.IsNullOrEmpty(settings.MusicLibraryPath))
                        MusicLibraryPath = settings.MusicLibraryPath;
                }
                catch { App.Logger.Warn("无法读取MusicLibraryPath设置，使用默认值"); }
                
                try { CacheSize = settings.CacheSize; } 
                catch { App.Logger.Warn("无法读取CacheSize设置，使用默认值"); }
                
                // 立即应用一些设置
                _playerService.SetVolume(DefaultVolume);
                _playerService.EnableCrossFade = CrossFade;
                _playerService.CrossFadeDuration = CrossFadeDuration;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "加载设置失败，使用默认值");
                // 使用默认设置，无需操作
            }
        }

        private void SaveSettings()
        {
            if (_userService.CurrentUser?.Settings == null)
            {
                App.Logger.Warn("无法保存设置：用户未登录或设置为空");
                return;
            }

            try
            {
                var settings = _userService.CurrentUser.Settings;
                
                // 使用dynamic类型和反射处理不存在的属性
                dynamic dynamicSettings = settings;

                // 保存基本设置
                dynamicSettings.Volume = DefaultVolume;
                
                // 使用反射安全地设置属性
                try { dynamicSettings.AutoPlayOnStartup = AutoPlayOnStartup; } 
                catch { App.Logger.Warn("无法保存AutoPlayOnStartup设置"); }
                
                try { dynamicSettings.RememberPlaybackPosition = RememberPlaybackPosition; } 
                catch { App.Logger.Warn("无法保存RememberPlaybackPosition设置"); }
                
                try { dynamicSettings.CrossFade = CrossFade; } 
                catch { App.Logger.Warn("无法保存CrossFade设置"); }
                
                try { dynamicSettings.CrossFadeDuration = CrossFadeDuration; } 
                catch { App.Logger.Warn("无法保存CrossFadeDuration设置"); }
                
                try { dynamicSettings.AudioFormatIndex = SelectedAudioFormatIndex; } 
                catch { App.Logger.Warn("无法保存AudioFormatIndex设置"); }
                
                try { dynamicSettings.ShowAnimations = ShowAnimations; } 
                catch { App.Logger.Warn("无法保存ShowAnimations设置"); }
                
                try { dynamicSettings.AlwaysShowLyrics = AlwaysShowLyrics; } 
                catch { App.Logger.Warn("无法保存AlwaysShowLyrics设置"); }
                
                try { dynamicSettings.MusicLibraryPath = MusicLibraryPath; } 
                catch { App.Logger.Warn("无法保存MusicLibraryPath设置"); }
                
                try { dynamicSettings.CacheSize = CacheSize; } 
                catch { App.Logger.Warn("无法保存CacheSize设置"); }

                // 保存主题设置
                switch (SelectedThemeIndex)
                {
                    case 1:
                        settings.Theme = "Light";
                        break;
                    case 2:
                        settings.Theme = "System";
                        break;
                    default:
                        settings.Theme = "Dark";
                        break;
                }

                // 更新用户设置
                try
                {
                    _userService.SaveUserSettingsAsync(settings).Wait();
                    App.Logger.Info("用户设置已保存");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "保存用户设置失败");
                    MessageBox.Show("保存设置时出错: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "保存设置失败");
                MessageBox.Show("保存设置时出错: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void BrowseFolder()
        {
            try
            {
                // 在此处实现浏览文件夹功能
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择音乐库文件夹",
                    ShowNewFolderButton = true
                };

                if (Directory.Exists(MusicLibraryPath))
                {
                    dialog.SelectedPath = MusicLibraryPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MusicLibraryPath = dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "浏览文件夹失败");
                MessageBox.Show("选择文件夹失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearCache()
        {
            try
            {
                // 清除缓存的实现
                string cachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MusicPlayerApp", "Cache");
                
                if (Directory.Exists(cachePath))
                {
                    // 提示用户确认删除
                    var result = MessageBox.Show("确定要清除所有缓存吗？这将删除临时文件，但不会影响您的音乐库。",
                        "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // 删除目录下的所有文件，但保留目录结构
                        foreach (string file in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Warn(ex, $"无法删除缓存文件: {file}");
                            }
                        }
                        
                        MessageBox.Show("缓存已清除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        App.Logger.Info("缓存已清除");
                    }
                }
                else
                {
                    MessageBox.Show("没有找到缓存目录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "清除缓存失败");
                MessageBox.Show("清除缓存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyTheme(int themeIndex)
        {
            try
            {
                var app = App.Current as App;
                string themeName;
                
                // 根据主题索引应用不同的主题
                switch (themeIndex)
                {
                    case 1:
                        themeName = "light";
                        break;
                    case 2:
                        themeName = "system";
                        break;
                    default:
                        themeName = "dark";
                        break;
                }
                
                // 使用App中的主题应用方法
                app?.ApplyTheme(themeName);
                
                App.Logger.Info($"已应用主题: {themeName}");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "应用主题失败");
            }
        }

        private bool IsSystemUsingDarkTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value != null && value is int intValue)
                        {
                            return intValue == 0; // 0 表示深色主题，1 表示浅色主题
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取系统主题设置失败");
            }

            return false; // 默认返回非深色主题
        }

        private void ApplySettings()
        {
            try
            {
                var app = App.Current as App;
                
                // 应用主题
                string themeName = "dark";
                switch (SelectedThemeIndex)
                {
                    case 1:
                        themeName = "light";
                        break;
                    case 2:
                        themeName = "system";
                        break;
                    default:
                        themeName = "dark";
                        break;
                }
                
                // 使用App类中的主题应用方法
                app?.ApplyTheme(themeName);
                
                // 应用音频设置
                _playerService.SetVolume(DefaultVolume);
                _playerService.EnableCrossFade = CrossFade;
                _playerService.CrossFadeDuration = CrossFadeDuration;
                
                // 保存设置
                SaveSettings();
                
                MessageBox.Show("设置已应用", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "应用设置失败");
                MessageBox.Show("应用设置失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SaveAndClose()
        {
            try
            {
                SaveSettings();
                // 关闭窗口的逻辑在View中处理
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "保存并关闭设置失败");
                MessageBox.Show("保存设置失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ResetSettings()
        {
            try
            {
                // 重置为默认设置
                DefaultVolume = 50;
                AutoPlayOnStartup = false;
                RememberPlaybackPosition = true;
                CrossFade = true;
                CrossFadeDuration = 2;
                SelectedAudioFormatIndex = 0;
                SelectedThemeIndex = 0; // 深色主题
                ShowAnimations = true;
                AlwaysShowLyrics = false;
                MusicLibraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "MusicPlayer");
                CacheSize = 500;
                
                App.Logger.Info("设置已重置为默认值");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "重置设置失败");
                MessageBox.Show("重置设置失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OpenMusicFolder()
        {
            try
            {
                if (Directory.Exists(MusicLibraryPath))
                {
                    // 打开资源管理器并定位到音乐文件夹
                    Process.Start("explorer.exe", MusicLibraryPath);
                }
                else
                {
                    MessageBox.Show("音乐库文件夹不存在，请先选择或创建一个有效的文件夹", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "打开音乐库文件夹失败");
                MessageBox.Show("打开文件夹失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void BrowseMusicFolder()
        {
            try
            {
                // 使用系统文件夹选择对话框
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择音乐库文件夹",
                    ShowNewFolderButton = true
                };

                if (Directory.Exists(MusicLibraryPath))
                {
                    dialog.SelectedPath = MusicLibraryPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MusicLibraryPath = dialog.SelectedPath;
                    App.Logger.Info($"已选择新的音乐库路径: {MusicLibraryPath}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "浏览文件夹失败");
                MessageBox.Show("选择文件夹失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
} 