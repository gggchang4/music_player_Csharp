using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = false;
                
                // 检查是否需要反转逻辑
                if (parameter != null && parameter.ToString().ToLower() == "false")
                {
                    invert = true;
                }
                
                bool result = invert ? !boolValue : boolValue;
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                
                // 检查是否需要反转逻辑
                if (parameter != null && parameter.ToString().ToLower() == "false")
                {
                    return !result;
                }
                
                return result;
            }
            
            return false;
        }
    }
} 