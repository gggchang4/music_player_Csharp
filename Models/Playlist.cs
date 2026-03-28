// Playlist.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicPlayerApp.Models
{
    public class Playlist : MediaItem
    {
        public int UserId { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Description { get; set; }
        public string CoverImage { get; set; }

        // 导航属性
        public User User { get; set; }
        public ICollection<PlaylistSong> PlaylistSongs { get; set; }

        public Playlist()
        {
            PlaylistSongs = new List<PlaylistSong>();
            AddedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }

        // 辅助方法，从关联表获取歌曲集合
        public IEnumerable<Song> GetSongs()
        {
            return PlaylistSongs?.Select(ps => ps.Song) ?? Enumerable.Empty<Song>();
        }

        public void AddSong(Song song)
        {
            if (song == null) return;

            // 检查是否已存在
            if (PlaylistSongs.Any(ps => ps.SongId == song.Id))
                return;

            int maxOrder = 0;
            if (PlaylistSongs.Any())
            {
                maxOrder = PlaylistSongs.Max(ps => ps.OrderNumber);
            }

            PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = this.Id,
                SongId = song.Id,
                Song = song,
                OrderNumber = maxOrder + 1
            });

            ModifiedDate = DateTime.Now;
        }

        public void RemoveSong(Song song)
        {
            if (song == null) return;

            var playlistSong = PlaylistSongs.FirstOrDefault(ps => ps.SongId == song.Id);
            if (playlistSong != null)
            {
                PlaylistSongs.Remove(playlistSong);
                ModifiedDate = DateTime.Now;

                // 重新排序
                int order = 1;
                foreach (var ps in PlaylistSongs.OrderBy(ps => ps.OrderNumber))
                {
                    ps.OrderNumber = order++;
                }
            }
        }
    }
}
