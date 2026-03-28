// Album.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicPlayerApp.Models
{
    public class Album : MediaItem
    {
        public int ArtistId { get; set; }
        public int Year { get; set; }
        public string CoverImage { get; set; }

        // 导航属性
        public Artist Artist { get; set; }
        public ICollection<Song> Songs { get; set; }
        public ICollection<FavoriteAlbum> FavoriteAlbums { get; set; }

        public Album()
        {
            Songs = new List<Song>();
            FavoriteAlbums = new List<FavoriteAlbum>();
            AddedDate = DateTime.Now;
        }

        public override string GetDisplayName()
        {
            return $"{Title} ({Year}) - {Artist?.Name ?? "Unknown Artist"}";
        }
    }
}