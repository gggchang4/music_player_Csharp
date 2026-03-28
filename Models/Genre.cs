// Genre.cs
using System.Collections.Generic;

namespace MusicPlayerApp.Models
{
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // 导航属性
        public ICollection<Song> Songs { get; set; }

        public Genre()
        {
            Songs = new List<Song>();
        }
    }
}