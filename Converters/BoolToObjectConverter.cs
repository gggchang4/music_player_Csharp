using System;
using System.Globalization;
using System.Windows.Data;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Converters
{
    /// <summary>
    /// 将布尔值转换为指定对象的转换器
    /// </summary>
    public class BoolToObjectConverter : IValueConverter
    {
        /// <summary>
        /// 当值为true时返回的对象
        /// </summary>
        public object TrueValue { get; set; }

        /// <summary>
        /// 当值为false时返回的对象
        /// </summary>
        public object FalseValue { get; set; }

        /// <summary>
        /// 将布尔值转换为指定对象
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                // 如果参数是整数ID，表示要调用视图模型上的IsSongFavorited方法
                if (parameter is int songId)
                {
                    // 如果value是方法，表示是IsSongFavorited
                    if (value is Func<int, bool> isFavoritedFunc)
                    {
                        bool result = isFavoritedFunc(songId);
                        return result ? TrueValue : FalseValue;
                    }
                }
                
                // 尝试获取视图模型实例并调用IsSongFavorited方法
                if (value is SearchResultsViewModel viewModel && parameter is int id)
                {
                    bool isFavorited = viewModel.IsSongFavorited(id);
                    return isFavorited ? TrueValue : FalseValue;
                }
            }

            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }

            return FalseValue;
        }

        /// <summary>
        /// 将对象转换回布尔值
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.Equals(TrueValue);
        }
    }
} 