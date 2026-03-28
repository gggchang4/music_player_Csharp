using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicPlayerApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (boolValue)
                {
                    // 如果提供了参数颜色，则使用该颜色
                    if (parameter is string colorStr)
                    {
                        try
                        {
                            // 尝试将颜色字符串转换为颜色
                            return (SolidColorBrush)(new BrushConverter().ConvertFrom(colorStr));
                        }
                        catch (Exception)
                        {
                            // 如果转换失败，使用默认高亮颜色
                            return new SolidColorBrush(Color.FromRgb(156, 39, 176)); // 默认紫色 #9C27B0
                        }
                    }
                    // 如果没有提供参数，使用默认高亮颜色
                    return new SolidColorBrush(Color.FromRgb(156, 39, 176)); // 默认紫色 #9C27B0
                }
                else
                {
                    // 不高亮的情况，返回透明
                    return new SolidColorBrush(Colors.Transparent);
                }
            }

            // 默认返回透明
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 不支持反向转换
            throw new NotImplementedException();
        }
    }
} 