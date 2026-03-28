using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    /// <summary>
    /// 将集合数量转换为可见性的转换器
    /// 当集合数量等于指定参数值时，返回Visible，否则返回Collapsed
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            // 尝试将值解析为int
            if (int.TryParse(value.ToString(), out int count))
            {
                // 尝试将参数解析为int，默认为0
                int targetCount = 0;
                if (parameter != null)
                {
                    int.TryParse(parameter.ToString(), out targetCount);
                }

                // 如果集合数量等于目标数量，则显示；否则隐藏
                return count == targetCount ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 