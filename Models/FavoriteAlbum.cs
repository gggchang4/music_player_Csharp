using System;

namespace MusicPlayerApp.Models
{
    public class FavoriteAlbum
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AlbumId { get; set; }
        public DateTime AddedDate { get; set; }

        // 导航属性
        public User User { get; set; }
        public Album Album { get; set; }

        public FavoriteAlbum()
        {
            AddedDate = DateTime.Now;
        }
    }
} 