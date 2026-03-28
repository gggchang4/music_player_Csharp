using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    public class SongCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                if (count == 0)
                {
                    return "暂无歌曲";
                }
                return $"{count} 首歌曲";
            }
            return "暂无歌曲";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 