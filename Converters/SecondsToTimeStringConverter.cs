using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    public class SecondsToTimeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return time.TotalHours >= 1
                    ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                    : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
            }
            
            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 