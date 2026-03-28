using System;
using System.IO;
using System.Linq;
using TagLib;

namespace MusicPlayerApp.Helpers
{
    public static class MetadataHelper
    {
        // 从音频文件中提取元数据
        public static (string title, string artist, string album, string genre, int year, TimeSpan duration, byte[] albumArt) 
            ExtractMetadata(string filePath)
        {
            try
            {
                var file = TagLib.File.Create(filePath);
                
                var title = !string.IsNullOrWhiteSpace(file.Tag.Title) 
                    ? file.Tag.Title 
                    : Path.GetFileNameWithoutExtension(filePath);
                
                var artist = file.Tag.FirstPerformer ?? "未知艺术家";
                var album = file.Tag.Album ?? "未知专辑";
                var genre = file.Tag.FirstGenre ?? "未知流派";
                var year = (int)(file.Tag.Year > 0 ? file.Tag.Year : 0);
                var duration = file.Properties.Duration;
                
                byte[] albumArt = null;
                if (file.Tag.Pictures.Length > 0)
                {
                    var picture = file.Tag.Pictures[0];
                    albumArt = picture.Data.Data;
                }
                
                return (title, artist, album, genre, year, duration, albumArt);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"元数据提取失败: {filePath}");
                return (Path.GetFileNameWithoutExtension(filePath), "未知艺术家", "未知专辑", "未知流派", 0, TimeSpan.Zero, null);
            }
        }
        
        // 清理标题（移除前导数字和分隔符）
        public static string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "未知标题";
                
            // 移除前导数字和分隔符（如 "01 - "）
            string cleaned = System.Text.RegularExpressions.Regex.Replace(
                title, 
                @"^\d+[\s.\-_]+|[\(\[\{].*?[\)\]\}]|\s*-\s*.*$", 
                "");
                
            // 去除多余空格
            cleaned = cleaned.Trim();
            
            return string.IsNullOrWhiteSpace(cleaned) ? title : cleaned;
        }
        
        // 分割艺术家和标题
        public static (string artist, string title) SplitArtistAndTitle(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return ("未知艺术家", "未知标题");
                
            // 尝试从形如 "Artist - Title" 的格式中提取
            var match = System.Text.RegularExpressions.Regex.Match(
                filename, 
                @"^(.*?)\s*-\s*");
                
            if (match.Success)
            {
                string artist = match.Groups[1].Value.Trim();
                string title = filename.Substring(match.Length).Trim();
                return (artist, title);
            }
            
            // 如果没有找到艺术家，返回整个文件名作为标题
            return ("未知艺术家", filename);
        }
        
        // 格式化时间（将秒数转换为格式化字符串）
        public static string FormatTime(int seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}