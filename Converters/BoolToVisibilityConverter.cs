using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    /// <summary>
    /// 将布尔值转换为可见性的转换器
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 是否反转转换结果
        /// </summary>
        public bool Inverse { get; set; }

        /// <summary>
        /// 将布尔值转换为可见性
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = false;

            if (value is bool)
            {
                boolValue = (bool)value;
            }
            else if (value is int)
            {
                if (parameter != null && parameter.ToString() == "Zero")
                {
                    // Zero参数: 数值为0时显示
                    boolValue = (int)value == 0;
                }
                else
                {
                    // 默认行为: 数值大于0时显示
                    boolValue = (int)value > 0;
                }
            }
            else if (value is System.Collections.ICollection collection)
            {
                if (parameter != null && parameter.ToString() == "Zero")
                {
                    // Zero参数: 集合为空时显示
                    boolValue = collection.Count == 0;
                }
                else
                {
                    // 默认行为: 集合不为空时显示
                    boolValue = collection.Count > 0;
                }
            }
            else if (value != null)
            {
                boolValue = true;
            }

            if (parameter != null && parameter.ToString() == "Inverse")
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 将可见性转换回布尔值
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                var result = visibility == Visibility.Visible;
                return Inverse ? !result : result;
            }
            
            return false;
        }
    }
} 