using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    /// <summary>
    /// 将秒数转换为时间字符串格式的转换器
    /// </summary>
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "00:00";
                
            if (value is int seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return time.TotalHours >= 1
                    ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                    : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
            }
            
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 通常不需要反向转换
            throw new NotImplementedException();
        }
    }
} 