using System;

namespace MusicPlayerApp.Models
{
    // 媒体项目的抽象基类，展示面向对象编程
    public abstract class MediaItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime AddedDate { get; set; }

        // 虚方法，允许子类重写
        public virtual string GetDisplayName()
        {
            return Title;
        }

        public override string ToString()
        {
            return GetDisplayName();
        }
    }
}