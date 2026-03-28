using MusicPlayerApp.Services;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using CommonServiceLocator;

namespace MusicPlayerApp.ViewModels
{
    /// <summary>
    /// ViewModel定位器，用于提供ViewModel实例
    /// </summary>
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            // 使用完全限定名解决不明确引用问题
            CommonServiceLocator.ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            // 注册服务
            SimpleIoc.Default.Register<MediaLibraryService>();
            SimpleIoc.Default.Register<MediaPlayerService>();
            SimpleIoc.Default.Register<UserService>();

            // 注册ViewModel
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<HomeViewModel>();
            SimpleIoc.Default.Register<AllMusicViewModel>();
            SimpleIoc.Default.Register<ArtistsViewModel>();
            SimpleIoc.Default.Register<AlbumsViewModel>();
            SimpleIoc.Default.Register<FavoritesViewModel>();
            SimpleIoc.Default.Register<PlaylistViewModel>();
        }

        // 主ViewModel
        public MainViewModel Main => CommonServiceLocator.ServiceLocator.Current.GetInstance<MainViewModel>();

        // 首页ViewModel
        public HomeViewModel Home => CommonServiceLocator.ServiceLocator.Current.GetInstance<HomeViewModel>();

        // 所有音乐ViewModel
        public AllMusicViewModel AllMusic => CommonServiceLocator.ServiceLocator.Current.GetInstance<AllMusicViewModel>();
        
        // 艺术家ViewModel
        public ArtistsViewModel Artists => CommonServiceLocator.ServiceLocator.Current.GetInstance<ArtistsViewModel>();
        
        // 专辑ViewModel
        public AlbumsViewModel Albums => CommonServiceLocator.ServiceLocator.Current.GetInstance<AlbumsViewModel>();
        
        // 收藏ViewModel
        public FavoritesViewModel Favorites => CommonServiceLocator.ServiceLocator.Current.GetInstance<FavoritesViewModel>();
        
        // 播放列表ViewModel
        public PlaylistViewModel Playlist => CommonServiceLocator.ServiceLocator.Current.GetInstance<PlaylistViewModel>();

        public static void Cleanup()
        {
            // 清理资源
        }
    }
} 