using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services
{
    /// <summary>
    /// 歌词服务 - 负责歌词的加载、解析和管理
    /// </summary>
    public class LyricService
    {
        private static readonly Lazy<LyricService> _instance = new Lazy<LyricService>(() => new LyricService());
        public static LyricService Instance => _instance.Value;

        // 缓存已加载的歌词
        private Dictionary<int, LyricFile> _lyricCache = new Dictionary<int, LyricFile>();

        private LyricService() { }

        /// <summary>
        /// 为歌曲加载歌词
        /// </summary>
        public async Task<LyricFile> LoadLyricsForSongAsync(Song song)
        {
            if (song == null)
                return new LyricFile();

            // 检查缓存
            if (_lyricCache.ContainsKey(song.Id))
                return _lyricCache[song.Id];

            // 首先尝试查找本地LRC文件
            var lyricFile = await TryLoadLocalLrcFileAsync(song);
            
            // 如果没有找到本地文件，生成默认歌词
            if (lyricFile.Lines.Count == 0)
            {
                lyricFile = GenerateDefaultLyric(song);
            }

            // 缓存歌词
            _lyricCache[song.Id] = lyricFile;
            return lyricFile;
        }

        /// <summary>
        /// 尝试加载本地LRC文件
        /// </summary>
        private async Task<LyricFile> TryLoadLocalLrcFileAsync(Song song)
        {
            // 检查可能的歌词文件位置
            var potentialLrcPaths = new List<string>
            {
                // 同名LRC文件 (如 song.mp3 -> song.lrc)
                Path.ChangeExtension(song.FilePath, "lrc"),
                
                // 同目录下的歌曲名.lrc
                Path.Combine(Path.GetDirectoryName(song.FilePath), $"{Path.GetFileNameWithoutExtension(song.FilePath)}.lrc"),
                
                // 歌曲名 - 艺术家.lrc
                Path.Combine(Path.GetDirectoryName(song.FilePath), $"{Path.GetFileNameWithoutExtension(song.FilePath)} - {song.Artist?.Name}.lrc")
            };

            foreach (var lrcPath in potentialLrcPaths)
            {
                if (File.Exists(lrcPath))
                {
                    try
                    {
                        string content = File.ReadAllText(lrcPath);
                        return ParseLrcContent(content);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, $"加载歌词文件失败: {lrcPath}");
                    }
                }
            }

            return new LyricFile();
        }

        /// <summary>
        /// 解析LRC文件内容
        /// </summary>
        private LyricFile ParseLrcContent(string lrcContent)
        {
            var lyricFile = new LyricFile();
            
            if (string.IsNullOrWhiteSpace(lrcContent))
                return lyricFile;

            string[] lines = lrcContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                // 跳过空行和没有时间标签的行
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("["))
                    continue;
                
                // 解析形如 [00:12.34]歌词内容 的行
                var match = Regex.Match(line, @"\[(\d{2}):(\d{2})\.(\d{2})\](.*?)$");
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value);
                    int seconds = int.Parse(match.Groups[2].Value);
                    int milliseconds = int.Parse(match.Groups[3].Value) * 10; // 转换为毫秒
                    string content = match.Groups[4].Value.Trim();
                    
                    var timeSpan = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                    lyricFile.Lines.Add(new LyricLine { Time = timeSpan, Content = content });
                }
            }
            
            // 按时间排序
            lyricFile.Lines = lyricFile.Lines.OrderBy(l => l.Time).ToList();
            return lyricFile;
        }

        /// <summary>
        /// 为没有歌词的歌曲生成默认歌词
        /// </summary>
        private LyricFile GenerateDefaultLyric(Song song)
        {
            var lyricFile = new LyricFile();
            
            // 添加歌曲基本信息作为默认歌词
            lyricFile.Lines.Add(new LyricLine 
            { 
                Time = TimeSpan.Zero,
                Content = $"歌曲: {song.Title}" 
            });
            
            if (song.Artist != null)
            {
                lyricFile.Lines.Add(new LyricLine 
                { 
                    Time = TimeSpan.FromSeconds(2),
                    Content = $"艺术家: {song.Artist.Name}" 
                });
            }
            
            if (song.Album != null)
            {
                lyricFile.Lines.Add(new LyricLine 
                { 
                    Time = TimeSpan.FromSeconds(4),
                    Content = $"专辑: {song.Album.Title}" 
                });
            }
            
            // 添加空行，提供间隔
            lyricFile.Lines.Add(new LyricLine 
            { 
                Time = TimeSpan.FromSeconds(6),
                Content = "" 
            });
            
            lyricFile.Lines.Add(new LyricLine 
            { 
                Time = TimeSpan.FromSeconds(8),
                Content = "享受音乐..." 
            });
            
            return lyricFile;
        }

        /// <summary>
        /// 尝试在线搜索歌词 (预留接口)
        /// </summary>
        public async Task<LyricFile> SearchLyricsOnlineAsync(Song song)
        {
            // 这里实现在线歌词搜索的代码
            // 预留接口，未来可以接入各种歌词API
            
            // 目前返回空歌词文件
            await Task.Delay(500); // 模拟网络请求
            return new LyricFile();
        }

        /// <summary>
        /// 清除歌词缓存
        /// </summary>
        public void ClearCache()
        {
            _lyricCache.Clear();
        }
    }
} 