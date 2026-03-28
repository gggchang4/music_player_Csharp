using System;
using System.Collections.Generic;

namespace MusicPlayerApp.Models
{
    /// <summary>
    /// 歌词行 - 表示单行歌词及其时间点
    /// </summary>
    public class LyricLine
    {
        /// <summary>
        /// 时间点
        /// </summary>
        public TimeSpan Time { get; set; }
        
        /// <summary>
        /// 歌词内容
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// 是否为当前行（用于UI高亮显示）
        /// </summary>
        public bool IsCurrent { get; set; }
    }
    
    /// <summary>
    /// 歌词文件 - 包含完整的歌词行集合
    /// </summary>
    public class LyricFile
    {
        /// <summary>
        /// 歌词行集合
        /// </summary>
        public List<LyricLine> Lines { get; set; } = new List<LyricLine>();
        
        /// <summary>
        /// 获取当前应该显示的歌词行
        /// </summary>
        public LyricLine GetCurrentLine(TimeSpan currentTime)
        {
            if (Lines.Count == 0)
                return null;
                
            // 找到时间小于或等于当前时间的最后一行
            LyricLine currentLine = Lines[0];
            
            foreach (var line in Lines)
            {
                if (line.Time <= currentTime)
                {
                    currentLine = line;
                }
                else
                {
                    break;
                }
            }
            
            return currentLine;
        }
        
        /// <summary>
        /// 更新歌词行的当前状态
        /// </summary>
        public void UpdateCurrentLine(TimeSpan currentTime)
        {
            var currentLine = GetCurrentLine(currentTime);
            
            foreach (var line in Lines)
            {
                line.IsCurrent = (line == currentLine);
            }
        }
    }
} 