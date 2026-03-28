using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayerApp.Helpers
{
    public class NumberToSongCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return $"{count} 首歌曲";
            }
            return "首歌曲";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 