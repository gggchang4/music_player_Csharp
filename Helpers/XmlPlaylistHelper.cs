using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Helpers
{
    // 处理M3U和XML格式的播放列表
    public static class XmlPlaylistHelper
    {
        // 导入M3U格式播放列表
        public static List<string> ImportM3UPlaylist(string filePath)
        {
            try
            {
                var fileList = new List<string>();
                var lines = File.ReadAllLines(filePath);
                
                foreach (var line in lines)
                {
                    // 跳过注释和空行
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                        
                    // 如果是相对路径，则转换为绝对路径
                    string path = line;
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.Combine(Path.GetDirectoryName(filePath), path);
                    }
                    
                    // 添加到文件列表
                    if (File.Exists(path))
                    {
                        fileList.Add(path);
                    }
                }
                
                return fileList;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导入M3U播放列表失败: {filePath}");
                return new List<string>();
            }
        }
        
        // 导出M3U格式播放列表
        public static bool ExportM3UPlaylist(string filePath, IEnumerable<Song> songs)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    // 写入M3U头
                    writer.WriteLine("#EXTM3U");
                    
                    foreach (var song in songs)
                    {
                        // 写入歌曲信息行
                        writer.WriteLine($"#EXTINF:{song.Duration},{song.Artist.Name} - {song.Title}");
                        // 写入文件路径
                        writer.WriteLine(song.FilePath);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导出M3U播放列表失败: {filePath}");
                return false;
            }
        }
        
        // 导入XML格式播放列表
        public static List<string> ImportXMLPlaylist(string filePath)
        {
            try
            {
                var fileList = new List<string>();
                var doc = XDocument.Load(filePath);
                var playlist = doc.Root;
                
                if (playlist.Name != "playlist")
                {
                    throw new FormatException("无效的XML播放列表格式");
                }
                
                var tracks = playlist.Elements("track");
                foreach (var track in tracks)
                {
                    var location = track.Element("location");
                    if (location != null)
                    {
                        string path = location.Value;
                        
                        // 处理URI格式
                        if (path.StartsWith("file:///"))
                        {
                            path = Uri.UnescapeDataString(path.Substring(8));
                        }
                        
                        // 如果是相对路径，则转换为绝对路径
                        if (!Path.IsPathRooted(path))
                        {
                            path = Path.Combine(Path.GetDirectoryName(filePath), path);
                        }
                        
                        // 添加到文件列表
                        if (File.Exists(path))
                        {
                            fileList.Add(path);
                        }
                    }
                }
                
                return fileList;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导入XML播放列表失败: {filePath}");
                return new List<string>();
            }
        }
        
        // 导出XML格式播放列表
        public static bool ExportXMLPlaylist(string filePath, string title, IEnumerable<Song> songs)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("playlist",
                        new XAttribute("version", "1"),
                        new XAttribute("title", title),
                        new XElement("info",
                            new XElement("creator", "MusicPlayerApp"),
                            new XElement("createDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
                        ),
                        songs.Select(song => 
                            new XElement("track",
                                new XElement("location", song.FilePath),
                                new XElement("title", song.Title),
                                new XElement("artist", song.Artist.Name),
                                new XElement("album", song.Album.Title),
                                new XElement("duration", song.Duration)
                            )
                        )
                    )
                );
                
                doc.Save(filePath);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导出XML播放列表失败: {filePath}");
                return false;
            }
        }
    }
}