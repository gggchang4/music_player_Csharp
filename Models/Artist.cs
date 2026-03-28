// Artist.cs
using System.Collections.Generic;

namespace MusicPlayerApp.Models
{
    public class Artist : MediaItem
    {
        public string Name { get; set; }
        public string Biography { get; set; }
        public string Image { get; set; }
        public string Description { get; set; }

        // 导航属性
        public ICollection<Album> Albums { get; set; }
        public ICollection<Song> Songs { get; set; }

        public Artist()
        {
            Albums = new List<Album>();
            Songs = new List<Song>();
        }

        public override string GetDisplayName()
        {
            return Name;
        }
    }
}