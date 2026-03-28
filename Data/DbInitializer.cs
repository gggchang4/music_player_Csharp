using System;
using System.Collections.Generic;
using System.Linq;
using MusicPlayerApp.Data;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Data
{
    public static class DbInitializer
    {
        public static void Initialize(MusicDbContext context)
        {
            // 创建默认用户（如果不存在）
            if (!context.Users.Any())
            {
                var defaultUser = new User
                {
                    Username = "DefaultUser",
                    Password = "password", // 实际应用中应该哈希
                    Email = "default@example.com",
                    CreatedDate = DateTime.Now
                };

                context.Users.Add(defaultUser);
                context.SaveChanges();

                // 创建用户设置
                var settings = new UserSettings
                {
                    UserId = defaultUser.Id,
                    Theme = "Dark",
                    Volume = 50,
                    Shuffle = false,
                    RepeatMode = RepeatMode.None,
                    EqualizerSettings = "{}"
                };

                context.UserSettings.Add(settings);
                context.SaveChanges();

                // 创建默认播放列表
                var playlist = new Playlist
                {
                    Title = "我喜欢的音乐",
                    UserId = defaultUser.Id,
                    Description = "我最喜欢的歌曲集合",
                    AddedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };

                context.Playlists.Add(playlist);
                context.SaveChanges();
            }

            // 创建默认流派（如果不存在）
            if (!context.Genres.Any())
            {
                var genres = new[]
                {
                    new Genre { Name = "流行" },
                    new Genre { Name = "摇滚" },
                    new Genre { Name = "电子" },
                    new Genre { Name = "古典" },
                    new Genre { Name = "爵士" },
                    new Genre { Name = "民谣" },
                    new Genre { Name = "嘻哈" },
                    new Genre { Name = "未知流派" }
                };

                context.Genres.AddRange(genres);
                context.SaveChanges();
            }

            // 创建默认艺术家（如果不存在）
            if (!context.Artists.Any())
            {
                var artists = new[]
                {
                    new Artist
                    {
                        Name = "示例艺术家",
                        Title = "示例艺术家",
                        AddedDate = DateTime.Now
                    },
                    new Artist
                    {
                        Name = "未知艺术家",
                        Title = "未知艺术家",
                        AddedDate = DateTime.Now
                    }
                };

                context.Artists.AddRange(artists);
                context.SaveChanges();
            }

            // 创建默认专辑（如果不存在）
            if (!context.Albums.Any())
            {
                var defaultArtist = context.Artists.FirstOrDefault(a => a.Name == "示例艺术家");
                if (defaultArtist != null)
                {
                    var albums = new[]
                    {
                        new Album
                        {
                            Title = "示例专辑",
                            ArtistId = defaultArtist.Id,
                            Year = 2024,
                            AddedDate = DateTime.Now
                        },
                        new Album
                        {
                            Title = "未知专辑",
                            ArtistId = defaultArtist.Id,
                            Year = 0,
                            AddedDate = DateTime.Now
                        }
                    };

                    context.Albums.AddRange(albums);
                    context.SaveChanges();
                }
            }
        }
    }
}