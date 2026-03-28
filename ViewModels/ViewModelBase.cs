using GalaSoft.MvvmLight;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicPlayerApp.ViewModels
{
    // MVVM视图模型的基类 - 继承MVVM Light框架的ViewModelBase
    public abstract class ViewModelBase : GalaSoft.MvvmLight.ViewModelBase
    {
        // 空实现，所有功能从MVVM Light的ViewModelBase继承

        // 添加OnPropertyChanged方法
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            RaisePropertyChanged(propertyName);
        }
    }
}