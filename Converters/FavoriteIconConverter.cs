using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Converters
{
    public class FavoriteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int songId)
            {
                // 从应用程序资源中查找DataContext
                var app = Application.Current;
                if (app.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    // 如果当前视图是AllMusicViewModel
                    if (mainViewModel.CurrentView is UIElement element && 
                        element.DataContext is AllMusicViewModel allMusicViewModel)
                    {
                        // 检查是否已收藏
                        bool isFavorited = allMusicViewModel.IsSongFavorited(songId);
                        return isFavorited ? PackIconKind.Heart : PackIconKind.HeartOutline;
                    }
                }
            }
            
            // 默认返回未收藏图标
            return PackIconKind.HeartOutline;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 