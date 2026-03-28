// UserSettings.cs
namespace MusicPlayerApp.Models
{
    public enum RepeatMode
    {
        None,
        All,
        One
    }

    public class UserSettings
    {
        public int UserId { get; set; }
        public string Theme { get; set; } = "Dark"; // 默认深色主题
        public int Volume { get; set; } = 50; // 默认音量50%
        public bool Shuffle { get; set; } = false;
        public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
        public string EqualizerSettings { get; set; } = "{}"; // 默认为平坦的均衡器
        
        // 新增设置项
        public bool AutoPlayOnStartup { get; set; } = false;
        public bool RememberPlaybackPosition { get; set; } = true;
        public bool CrossFade { get; set; } = true;
        public int CrossFadeDuration { get; set; } = 2;
        public int AudioFormatIndex { get; set; } = 0;
        public bool ShowAnimations { get; set; } = true;
        public bool AlwaysShowLyrics { get; set; } = false;
        public string MusicLibraryPath { get; set; } = "";
        public int CacheSize { get; set; } = 500;

        // 导航属性
        public User User { get; set; }
    }
}

