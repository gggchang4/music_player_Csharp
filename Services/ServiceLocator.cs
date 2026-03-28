using System;
using System.Collections.Generic;
using MusicPlayerApp.Data;
using MusicPlayerApp.ViewModels;

namespace MusicPlayerApp.Services
{
    /// <summary>
    /// 服务定位器 - 用于管理应用程序的服务依赖
    /// </summary>
    public class ServiceLocator
    {
        private static readonly Lazy<ServiceLocator> _instance = 
            new Lazy<ServiceLocator>(() => new ServiceLocator());
        
        public static ServiceLocator Instance => _instance.Value;
        
        private readonly Dictionary<Type, object> _services;
        
        private ServiceLocator()
        {
            _services = new Dictionary<Type, object>();
            RegisterServices();
        }
        
        /// <summary>
        /// 注册所有服务
        /// </summary>
        private void RegisterServices()
        {
            // 注册数据库上下文工厂
            Register<Func<MusicDbContext>>(() => new MusicDbContext());
            
            // 媒体播放器服务
            Register<MediaPlayerService>(MediaPlayerService.Instance);
            
            // 媒体库服务
            Register<MediaLibraryService>(
                new MediaLibraryService(GetService<Func<MusicDbContext>>()));
            
            // 用户服务
            Register<UserService>(
                new UserService(GetService<Func<MusicDbContext>>()));
            
            // 歌词服务
            Register<LyricService>(LyricService.Instance);

            // 主视图模型
            _services.Add(typeof(MainViewModel), new Func<MainViewModel>(() => 
                new MainViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>(), GetService<UserService>())));

            // 使用Func<>包装ViewModel工厂方法
            _services.Add(typeof(HomeViewModel), new Func<HomeViewModel>(() => 
                new HomeViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>(), GetService<UserService>())));
                
            _services.Add(typeof(ArtistsViewModel), new Func<ArtistsViewModel>(() => 
                new ArtistsViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>())));
                
            _services.Add(typeof(AlbumsViewModel), new Func<AlbumsViewModel>(() => 
                new AlbumsViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>())));
                
            _services.Add(typeof(FavoritesViewModel), new Func<FavoritesViewModel>(() => 
                new FavoritesViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>(), GetService<UserService>())));
                
            _services.Add(typeof(PlaylistViewModel), new Func<PlaylistViewModel>(() => 
                new PlaylistViewModel(GetService<MediaLibraryService>(), GetService<MediaPlayerService>(), GetService<UserService>())));
                
            _services.Add(typeof(CreatePlaylistViewModel), new Func<CreatePlaylistViewModel>(() => 
                new CreatePlaylistViewModel(GetService<MediaLibraryService>(), GetService<UserService>())));
        }
        
        /// <summary>
        /// 注册服务
        /// </summary>
        public void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
            }
        }
        
        /// <summary>
        /// 获取服务
        /// </summary>
        public T GetService<T>() where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                var service = _services[type];
                
                // 如果是工厂方法，则执行它
                if (service is Func<T> factory)
                {
                    return factory();
                }
                
                return (T)service;
            }
            
            throw new InvalidOperationException($"服务 {type.Name} 未注册");
        }
    }
} 