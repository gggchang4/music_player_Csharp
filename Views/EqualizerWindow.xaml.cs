using System;
using System.Windows;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Views
{
    /// <summary>
    /// EqualizerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EqualizerWindow : Window
    {
        public EqualizerWindow()
        {
            InitializeComponent();
            
            // 设置DataContext为新的ViewModel
            this.DataContext = new EqualizerViewModel();
        }
    }
} 