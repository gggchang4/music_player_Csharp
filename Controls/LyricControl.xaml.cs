using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Controls
{
    /// <summary>
    /// LyricControl.xaml 的交互逻辑
    /// </summary>
    public partial class LyricControl : UserControl
    {
        public LyricControl()
        {
            InitializeComponent();
            
            this.DataContextChanged += LyricControl_DataContextChanged;
            this.Loaded += LyricControl_Loaded;
        }
        
        // 歌词是否可用
        public bool HasLyrics
        {
            get { return (bool)GetValue(HasLyricsProperty); }
            set { SetValue(HasLyricsProperty, value); }
        }
        
        public static readonly DependencyProperty HasLyricsProperty =
            DependencyProperty.Register("HasLyrics", typeof(bool), typeof(LyricControl), 
                new PropertyMetadata(false));
        
        private void LyricControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.MainViewModel oldViewModel)
            {
                // 取消订阅旧视图模型的事件
                if (oldViewModel.LyricLines is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= LyricLines_CollectionChanged;
                }
            }
            
            if (e.NewValue is ViewModels.MainViewModel newViewModel)
            {
                // 订阅新视图模型的事件
                if (newViewModel.LyricLines is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += LyricLines_CollectionChanged;
                }
                
                // 更新歌词是否可用
                UpdateHasLyrics();
            }
        }
        
        private void LyricControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 更新歌词是否可用
            UpdateHasLyrics();
        }
        
        private void LyricLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 更新歌词是否可用
            UpdateHasLyrics();
            
            // 滚动到当前歌词
            ScrollToCurrentLyric();
        }
        
        private void UpdateHasLyrics()
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                // 简化逻辑，只要有歌词行就显示
                HasLyrics = viewModel.LyricLines != null && viewModel.LyricLines.Count > 0;
            }
            else
            {
                HasLyrics = false;
            }
        }
        
        private void ScrollToCurrentLyric()
        {
            if (DataContext is ViewModels.MainViewModel viewModel && 
                viewModel.LyricLines != null && 
                viewModel.LyricLines.Count > 0)
            {
                // 查找当前歌词的索引
                int currentIndex = -1;
                for (int i = 0; i < viewModel.LyricLines.Count; i++)
                {
                    if (viewModel.LyricLines[i].IsCurrent)
                    {
                        currentIndex = i;
                        break;
                    }
                }
                
                if (currentIndex >= 0)
                {
                    // 等待UI更新完成后再滚动
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 获取ItemsControl中的相应项，并滚动到可见
                            if (LyricItemsControl.ItemContainerGenerator.ContainerFromIndex(currentIndex) is FrameworkElement container)
                            {
                                container.BringIntoView();
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error(ex, "滚动到当前歌词时出错");
                        }
                    }));
                }
            }
        }
    }
} 