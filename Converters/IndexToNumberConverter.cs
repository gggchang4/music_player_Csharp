using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayerApp.Converters
{
    /// <summary>
    /// 将列表索引转换为从指定数字开始的序号（默认从1开始）
    /// </summary>
    public class IndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                int startNumber = 1; // 默认从1开始
                
                // 如果提供了参数，则使用参数作为起始数字
                if (parameter != null && int.TryParse(parameter.ToString(), out int customStart))
                {
                    startNumber = customStart;
                }
                
                // 返回索引+起始数字
                return (index + startNumber).ToString();
            }
            
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 