using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Data
{
    public static class DbInitializer
    {
        public static void Initialize(MusicDbContext context)
        {
            // 确保数据库已创建
            context.Database.EnsureCreated();

            // 如果已经有数据，就不需要初始化
            if (context.Users.Any())
            {
                return;
            }

            // 添加默认用户
            var defaultUser = new User
            {
                Username = "DefaultUser",
                Password = "password", // 实际应用中应存储哈希
                Email = "user@example.com",
            };
            context.Users.Add(defaultUser);
            context.SaveChanges();

            // 添加默认用户设置
            var userSettings = new UserSettings
            {
                UserId = defaultUser.Id,
                Theme = "Dark",
                Volume = 50,
                Shuffle = false,
                RepeatMode = RepeatMode.None,
                EqualizerSettings = "{}"
            };
            context.UserSettings.Add(userSettings);

            // 添加一些基本流派
            var genres = new Genre[]
            {
                new Genre { Name = "Pop" },
                new Genre { Name = "Rock" },
                new Genre { Name = "Hip Hop" },
                new Genre { Name = "Electronic" },
                new Genre { Name = "Classical" },
                new Genre { Name = "Jazz" },
                new Genre { Name = "R&B" },
                new Genre { Name = "Country" }
            };
            context.Genres.AddRange(genres);

            // 添加默认播放列表
            var defaultPlaylist = new Playlist
            {
                Title = "我喜欢的音乐",
                UserId = defaultUser.Id,
                Description = "我最喜欢的歌曲集合"
            };
            context.Playlists.Add(defaultPlaylist);

            context.SaveChanges();
        }
    }
}