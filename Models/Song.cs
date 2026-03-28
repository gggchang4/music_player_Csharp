using System;
using System.Collections.Generic;
using TagLib;

namespace MusicPlayerApp.Models
{
    // 定义可播放接口
    public interface IPlayable
    {
        void Play();
        void Pause();
        void Stop();
    }

    // 继承自MediaItem，实现IPlayable接口
    public class Song : MediaItem, IPlayable
    {
        public int ArtistId { get; set; }
        public int AlbumId { get; set; }
        public int GenreId { get; set; }
        public string FilePath { get; set; }
        public int Duration { get; set; }
        public string AlbumArt { get; set; }
        public int PlayCount { get; set; }
        public int Rating { get; set; }
        public int TrackNumber { get; set; }
        public DateTime? LastPlayedDate { get; set; }

        // 导航属性 - 稍后在数据库上下文中配置
        public Artist Artist { get; set; }
        public Album Album { get; set; }
        public Genre Genre { get; set; }
        public ICollection<PlaylistSong> PlaylistSongs { get; set; }
        public ICollection<FavoriteSong> FavoriteSongs { get; set; }

        public Song()
        {
            PlaylistSongs = new List<PlaylistSong>();
            FavoriteSongs = new List<FavoriteSong>();
            AddedDate = DateTime.Now;
        }

        public override string GetDisplayName()
        {
            return $"{Title} - {Artist?.Name ?? "Unknown Artist"}";
        }

        // IPlayable接口实现
        public void Play()
        {
            PlayCount++;
        }

        public void Pause()
        {
        }

        public void Stop()
        {
        }

        // 格式化歌曲时长
        public string GetFormattedDuration()
        {
            TimeSpan time = TimeSpan.FromSeconds(Duration);
            return time.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}