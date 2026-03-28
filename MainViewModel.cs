using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System.Windows.Threading;

namespace MusicPlayerApp.ViewModels
{
    // 简单的空视图模型，用于显示调试视图时临时替换主视图
    public class EmptyViewModel : ViewModelBase
    {
        // 这是一个具体类，继承自抽象的ViewModelBase
    }

    public class MainViewModel : ViewModelBase
    {
        // ... existing code ...
    }
} 