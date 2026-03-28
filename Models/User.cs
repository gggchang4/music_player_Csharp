// User.cs
using System;
using System.Collections.Generic;

namespace MusicPlayerApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; } // 实际应用中应存储密码哈希
        public string Email { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string ProfileImage { get; set; }
        
        // 用户资料属性
        public string Bio { get; set; }
        public string AvatarColor { get; set; }
        public string AvatarChar { get; set; }
        public string AvatarImagePath { get; set; }

        // 导航属性
        public UserSettings Settings { get; set; }
        public ICollection<Playlist> Playlists { get; set; }
        public ICollection<FavoriteSong> FavoriteSongs { get; set; }
        public ICollection<FavoriteAlbum> FavoriteAlbums { get; set; }

        public User()
        {
            Playlists = new List<Playlist>();
            FavoriteSongs = new List<FavoriteSong>();
            FavoriteAlbums = new List<FavoriteAlbum>();
            CreatedDate = DateTime.Now;
        }
    }
}
