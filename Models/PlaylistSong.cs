// PlaylistSong.cs
namespace MusicPlayerApp.Models
{
    // 多对多关系的联接表
    public class PlaylistSong
    {
        public int PlaylistId { get; set; }
        public int SongId { get; set; }
        public int OrderNumber { get; set; }

        // 导航属性
        public Playlist Playlist { get; set; }
        public Song Song { get; set; }
    }
}