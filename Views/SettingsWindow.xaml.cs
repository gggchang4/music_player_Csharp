using System;
using System.Windows;
using MusicPlayerApp.ViewModels;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.Views
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        
        public SettingsWindow()
        {
            InitializeComponent();
            
            // 创建并设置ViewModel
            _viewModel = new SettingsViewModel(
                ServiceLocator.Instance.GetService<UserService>(),
                ServiceLocator.Instance.GetService<MediaPlayerService>());
            
            this.DataContext = _viewModel;
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 保存设置逻辑已经在ViewModel的SaveCommand中实现
            
            this.DialogResult = true;
            this.Close();
        }
    }
} 