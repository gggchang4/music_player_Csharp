using System;

namespace MusicPlayerApp.Models
{
    public class FavoriteSong
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SongId { get; set; }
        public DateTime AddedDate { get; set; }

        // 导航属性
        public User User { get; set; }
        public Song Song { get; set; }

        public FavoriteSong()
        {
            AddedDate = DateTime.Now;
        }
    }
} 