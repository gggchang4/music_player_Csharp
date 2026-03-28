using System;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using MaterialDesignThemes.Wpf;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace MusicPlayerApp.Views
{
    /// <summary>
    /// UserProfileWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserProfileWindow : Window
    {
        private UserService _userService;
        private string _selectedAvatarColor = "#7B1FA2"; // 默认头像颜色
        private string _selectedAvatarChar = "用"; // 默认头像字符
        private string _selectedImagePath = null; // 用户选择的图片路径
        private bool _isCustomImage = false; // 是否使用自定义图片
        private User _currentUser = null; // 当前用户
        
        public UserProfileWindow()
        {
            InitializeComponent();
            
            // 获取用户服务
            _userService = ServiceLocator.Instance.GetService<UserService>();
            
            // 在窗口加载完成后加载用户数据
            Loaded += UserProfileWindow_Loaded;
        }
        
        private void UserProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载用户数据
            LoadUserData();
        }
        
        private void LoadUserData()
        {
            try 
            {
                // 获取当前登录的用户
                _currentUser = _userService.CurrentUser;
                
                if (_currentUser != null)
                {
                    // 填充用户界面
                    var userNameTextBox = FindVisualChild<TextBox>(this, tb => tb.Margin.Top == 0 && tb.Margin.Bottom == 20);
                    var emailTextBox = FindVisualChild<TextBox>(this, tb => tb.Margin.Top == 0 && tb.Margin.Bottom == 20 && tb != userNameTextBox);
                    var bioTextBox = FindVisualChild<TextBox>(this, tb => tb.Height == 100 && tb.Margin.Bottom == 20);
                    
                    if (userNameTextBox != null)
                        userNameTextBox.Text = _currentUser.Username ?? "默认用户";
                        
                    if (emailTextBox != null)
                        emailTextBox.Text = _currentUser.Email ?? "user@example.com";
                        
                    if (bioTextBox != null)
                        bioTextBox.Text = _currentUser.Bio ?? "这里是我的个人音乐简介。我喜欢各种类型的音乐，特别是流行和摇滚。";
                        
                    // 加载头像信息
                    if (!string.IsNullOrEmpty(_currentUser.AvatarColor))
                        _selectedAvatarColor = _currentUser.AvatarColor;
                        
                    if (!string.IsNullOrEmpty(_currentUser.AvatarChar))
                        _selectedAvatarChar = _currentUser.AvatarChar;
                        
                    if (!string.IsNullOrEmpty(_currentUser.AvatarImagePath))
                    {
                        _selectedImagePath = _currentUser.AvatarImagePath;
                        _isCustomImage = true;
                    }
                    
                    // 更新头像显示
                    UpdateAvatarDisplay();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载用户数据时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前用户，如果不存在则创建
                if (_currentUser == null)
                {
                    _currentUser = new User();
                    _currentUser.CreatedDate = DateTime.Now;
                }
                
                // 从界面获取用户输入
                var userNameTextBox = FindVisualChild<TextBox>(this, tb => tb.Margin.Top == 0 && tb.Margin.Bottom == 20);
                var emailTextBox = FindVisualChild<TextBox>(this, tb => tb.Margin.Top == 0 && tb.Margin.Bottom == 20 && tb != userNameTextBox);
                var bioTextBox = FindVisualChild<TextBox>(this, tb => tb.Height == 100 && tb.Margin.Bottom == 20);
                
                // 更新用户对象
                _currentUser.Username = userNameTextBox?.Text?.Trim();
                _currentUser.Email = emailTextBox?.Text?.Trim();
                _currentUser.Bio = bioTextBox?.Text?.Trim();
                _currentUser.UpdatedDate = DateTime.Now;
                
                // 保存头像信息
                _currentUser.AvatarColor = _selectedAvatarColor;
                _currentUser.AvatarChar = _selectedAvatarChar;
                _currentUser.AvatarImagePath = _isCustomImage ? _selectedImagePath : null;
                
                // 保存到数据库或配置文件
                Task.Run(async () =>
                {
                    try
                    {
                        await _userService.UpdateUser(_currentUser);
                        
                        // 在UI线程上显示保存成功消息
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("用户资料保存成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        });
                    }
                    catch (Exception ex)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"保存用户资料时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"准备保存用户资料时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 选择自定义头像图片
        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择头像图片",
                Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedImagePath = openFileDialog.FileName;
                _isCustomImage = true;
                
                // 关闭对话框
                AvatarDialogHost.IsOpen = false;
                
                // 更新头像显示
                UpdateAvatarDisplay();
            }
        }
        
        // 处理预设头像选择按钮
        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // 获取所选颜色和文字
                _selectedAvatarColor = button.Background.ToString();
                
                if (button.Content is TextBlock textBlock)
                {
                    _selectedAvatarChar = textBlock.Text;
                }
                
                _isCustomImage = false;
                
                // 直接关闭对话框
                AvatarDialogHost.IsOpen = false;
                
                // 更新头像显示
                UpdateAvatarDisplay();
            }
        }
        
        // 打开头像选择对话框
        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            AvatarDialogHost.IsOpen = true;
        }
        
        // 关闭头像选择对话框
        private void CloseAvatarDialog(object sender, RoutedEventArgs e)
        {
            AvatarDialogHost.IsOpen = false;
        }
        
        private void UpdateAvatarDisplay()
        {
            try
            {
                // 查找Border控件用于显示头像
                Border avatarBorder = FindVisualChild<Border>(this, b => b.Width == 120 && b.Height == 120);
                
                if (avatarBorder != null)
                {
                    if (_isCustomImage && !string.IsNullOrEmpty(_selectedImagePath))
                    {
                        // 使用用户选择的图片
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(_selectedImagePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        // 创建图像控件
                        Image avatarImage = new Image
                        {
                            Source = bitmap,
                            Stretch = Stretch.UniformToFill
                        };

                        // 清除已有内容并添加图片
                        avatarBorder.Child = avatarImage;
                        avatarBorder.Background = new SolidColorBrush(Colors.Transparent);
                    }
                    else
                    {
                        // 使用文字头像
                        Brush selectedBrush = null;
                        try
                        {
                            // 尝试将颜色字符串解析为画刷
                            var converter = new BrushConverter();
                            selectedBrush = converter.ConvertFromString(_selectedAvatarColor) as Brush;
                        }
                        catch
                        {
                            // 如果解析失败，使用默认颜色
                            selectedBrush = new SolidColorBrush(Color.FromRgb(123, 31, 162)); // 紫色
                        }
                        
                        // 设置背景颜色
                        avatarBorder.Background = selectedBrush;
                        
                        // 创建文字显示
                        TextBlock avatarText = new TextBlock
                        {
                            Text = _selectedAvatarChar,
                            FontSize = 42,
                            Foreground = new SolidColorBrush(Colors.White),
                            FontWeight = FontWeights.Medium,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        // 设置头像文字
                        avatarBorder.Child = avatarText;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新头像显示时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 查找视觉树中符合条件的子元素的辅助方法
        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> condition) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && condition(typedChild))
                    return typedChild;
                
                T childOfChild = FindVisualChild<T>(child, condition);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }
    }
} 