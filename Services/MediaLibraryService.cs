using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MusicPlayerApp.Models;
using MusicPlayerApp.Data;
using System.Windows;
using System.Diagnostics;
using TagLib;

namespace MusicPlayerApp.Services
{
    // 媒体库服务类
    public class MediaLibraryService
    {
        // 支持的音频文件扩展名
        private readonly string[] _supportedExtensions = { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".wma" };

        // 正则表达式模式，用于清理和提取元数据
        private static readonly Regex _cleanTitleRegex = new Regex(@"^\d+[\s.\-_]+|[\(\[\{].*?[\)\]\}]|\s*-\s*.*$", RegexOptions.Compiled);
        private static readonly Regex _extractArtistRegex = new Regex(@"^(.*?)\s*-\s*", RegexOptions.Compiled);
        private static readonly Regex _extractFeatRegex = new Regex(@"\(feat\.?(?:\s*|\s+)([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 数据库上下文工厂
        private readonly Func<MusicDbContext> _contextFactory;

        public MediaLibraryService(Func<MusicDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // 搜索歌曲
        public async Task<List<Song>> SearchSongsAsync(string query)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    IQueryable<Song> songsQuery = context.Songs
                        .Include(s => s.Artist)
                        .Include(s => s.Album)
                        .Include(s => s.Genre);

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        query = query.ToLower();
                        songsQuery = songsQuery.Where(s =>
                            s.Title.ToLower().Contains(query) ||
                            s.Artist.Name.ToLower().Contains(query) ||
                            s.Album.Title.ToLower().Contains(query));
                    }

                    return await songsQuery.ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "搜索歌曲失败");
                throw new Exception($"搜索歌曲失败: {ex.Message}", ex);
            }
        }

        // 获取所有艺术家
        public async Task<List<Artist>> GetAllArtistsAsync()
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.Artists.OrderBy(a => a.Name).ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取艺术家失败");
                throw new Exception($"获取艺术家失败: {ex.Message}", ex);
            }
        }

        // 获取所有专辑
        public async Task<List<Album>> GetAllAlbumsAsync()
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.Albums
                        .Include(a => a.Artist)
                        .OrderBy(a => a.Title)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取专辑失败");
                throw new Exception($"获取专辑失败: {ex.Message}", ex);
            }
        }

        // 获取所有播放列表
        public async Task<List<Playlist>> GetUserPlaylistsAsync(int userId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.Playlists
                        .Where(p => p.UserId == userId)
                        .Include(p => p.PlaylistSongs) // 确保包含PlaylistSongs导航属性
                        .OrderBy(p => p.Title)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取播放列表失败");
                throw new Exception($"获取播放列表失败: {ex.Message}", ex);
            }
        }

        // 导入单个音乐文件
        public async Task<Song> ImportSongAsync(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            var extension = Path.GetExtension(filePath).ToLower();
            if (!_supportedExtensions.Contains(extension))
                throw new ArgumentException($"不支持的文件类型: {extension}");

            try
            {
                App.Logger.Info($"开始导入音乐文件: {filePath}");
                
                using (var context = _contextFactory())
                {
                    // 检查是否已导入
                    var existingSong = await context.Songs
                        .FirstOrDefaultAsync(s => s.FilePath == filePath);

                    if (existingSong != null)
                    {
                        App.Logger.Info($"文件已存在于数据库中: {filePath}");
                        return existingSong;
                    }

                    // 读取音频文件元数据
                    var tfile = TagLib.File.Create(filePath);
                    App.Logger.Info($"读取音频文件元数据成功: {filePath}");

                    // 准备歌曲数据
                    var song = new Song
                    {
                        Title = !string.IsNullOrWhiteSpace(tfile.Tag.Title)
                            ? tfile.Tag.Title
                            : CleanTitle(Path.GetFileNameWithoutExtension(filePath)),
                        FilePath = filePath,
                        Duration = (int)tfile.Properties.Duration.TotalSeconds,
                        TrackNumber = (int)(tfile.Tag.Track > 0 ? tfile.Tag.Track : 0),
                        AddedDate = DateTime.Now
                    };

                    // 查找或创建艺术家
                    string artistName = tfile.Tag.FirstPerformer ?? "未知艺术家";
                    var artist = await context.Artists
                        .FirstOrDefaultAsync(a => a.Name == artistName);

                    if (artist == null)
                    {
                        artist = new Artist
                        {
                            Name = artistName,
                            Title = artistName,
                            AddedDate = DateTime.Now
                        };
                        context.Artists.Add(artist);
                        await context.SaveChangesAsync();
                        App.Logger.Info($"创建新艺术家: {artistName}");
                    }

                    song.ArtistId = artist.Id;
                    song.Artist = artist;

                    // 查找或创建专辑
                    string albumTitle = tfile.Tag.Album ?? "未知专辑";
                    var album = await context.Albums
                        .FirstOrDefaultAsync(a => a.Title == albumTitle && a.ArtistId == artist.Id);

                    if (album == null)
                    {
                        album = new Album
                        {
                            Title = albumTitle,
                            ArtistId = artist.Id,
                            Year = (int)(tfile.Tag.Year > 0 ? tfile.Tag.Year : 0),
                            AddedDate = DateTime.Now
                        };

                        // 提取专辑封面
                        if (tfile.Tag.Pictures.Length > 0)
                        {
                            var picture = tfile.Tag.Pictures[0];
                            string coversDir = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "MusicPlayerApp", "Covers");

                            Directory.CreateDirectory(coversDir);

                            string coverPath = Path.Combine(coversDir, $"{Guid.NewGuid()}.jpg");
                            System.IO.File.WriteAllBytes(coverPath, picture.Data.Data);
                            album.CoverImage = coverPath;
                            App.Logger.Info($"保存专辑封面: {coverPath}");
                        }

                        context.Albums.Add(album);
                        await context.SaveChangesAsync();
                        App.Logger.Info($"创建新专辑: {albumTitle}");
                    }

                    song.AlbumId = album.Id;
                    song.Album = album;

                    // 查找或创建流派
                    string genreName = tfile.Tag.FirstGenre ?? "未知流派";
                    var genre = await context.Genres
                        .FirstOrDefaultAsync(g => g.Name == genreName);

                    if (genre == null)
                    {
                        genre = new Genre { Name = genreName };
                        context.Genres.Add(genre);
                        await context.SaveChangesAsync();
                        App.Logger.Info($"创建新流派: {genreName}");
                    }

                    song.GenreId = genre.Id;
                    song.Genre = genre;

                    // 提取歌曲封面
                    if (tfile.Tag.Pictures.Length > 0 && string.IsNullOrEmpty(song.AlbumArt))
                    {
                        var picture = tfile.Tag.Pictures[0];
                        string coversDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "MusicPlayerApp", "Covers");

                        Directory.CreateDirectory(coversDir);

                        string coverPath = Path.Combine(coversDir, $"{Guid.NewGuid()}_song.jpg");
                        System.IO.File.WriteAllBytes(coverPath, picture.Data.Data);
                        song.AlbumArt = coverPath;
                        App.Logger.Info($"保存歌曲封面: {coverPath}");
                    }
                    else if (!string.IsNullOrEmpty(album.CoverImage))
                    {
                        // 使用专辑封面
                        song.AlbumArt = album.CoverImage;
                    }

                    // 保存歌曲
                    context.Songs.Add(song);
                    await context.SaveChangesAsync();
                    
                    App.Logger.Info($"成功导入歌曲: {song.Title} - 艺术家: {artist.Name}, 专辑: {album.Title}");
                    
                    // 验证歌曲是否已添加到数据库
                    var addedSong = await context.Songs.FirstOrDefaultAsync(s => s.FilePath == filePath);
                    if (addedSong != null)
                    {
                        App.Logger.Info($"验证歌曲已存在于数据库: ID={addedSong.Id}");
                    }
                    else
                    {
                        App.Logger.Warn($"验证失败：无法在数据库中找到刚添加的歌曲: {filePath}");
                    }
                    
                    return song;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导入歌曲失败: {filePath}");
                throw new Exception($"导入歌曲失败: {ex.Message}", ex);
            }
        }

        // 导入文件夹中的所有音乐文件
        public async Task<List<Song>> ImportFolderAsync(string folderPath, IProgress<(int current, int total, string fileName)> progress = null)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"文件夹不存在: {folderPath}");

            try
            {
                var importedSongs = new List<Song>();

                // 获取所有支持的音频文件
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                int total = files.Count;
                int current = 0;

                foreach (var file in files)
                {
                    try
                    {
                        current++;
                        progress?.Report((current, total, Path.GetFileName(file)));

                        var song = await ImportSongAsync(file);
                        if (song != null)
                        {
                            importedSongs.Add(song);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Warn(ex, $"导入文件失败: {file}");
                        // 继续处理下一个文件
                    }
                }

                return importedSongs;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"导入文件夹失败: {folderPath}");
                throw new Exception($"导入文件夹失败: {ex.Message}", ex);
            }
        }

        // 检查播放列表名称是否已存在
        public async Task<bool> PlaylistNameExistsAsync(string name, int userId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.Playlists
                        .AnyAsync(p => p.Title == name && p.UserId == userId);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"检查播放列表名称是否存在时出错: {name}");
                throw new Exception($"检查播放列表名称失败: {ex.Message}", ex);
            }
        }

        // 创建播放列表
        public async Task<Playlist> CreatePlaylistAsync(string name, int userId, string description = "")
        {
            try
            {
                // 检查是否已存在同名播放列表
                if (await PlaylistNameExistsAsync(name, userId))
                {
                    throw new Exception("已存在同名播放列表，请使用其他名称。");
                }
                
                using (var context = _contextFactory())
                {
                    var playlist = new Playlist
                    {
                        Title = name,
                        UserId = userId,
                        Description = description,
                        AddedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        PlaylistSongs = new List<PlaylistSong>() // 确保PlaylistSongs集合被初始化
                    };

                    context.Playlists.Add(playlist);
                    await context.SaveChangesAsync();

                    // 重新查询以确保所有关联数据都被加载
                    var createdPlaylist = await context.Playlists
                        .Include(p => p.PlaylistSongs)
                        .FirstOrDefaultAsync(p => p.Id == playlist.Id);

                    return createdPlaylist;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"创建播放列表失败: {name}");
                throw new Exception($"创建播放列表失败: {ex.Message}", ex);
            }
        }

        // 添加歌曲到播放列表
        public async Task AddSongToPlaylistAsync(int playlistId, int songId)
        {
            try
            {
                App.Logger.Info($"添加歌曲到播放列表: 播放列表ID={playlistId}, 歌曲ID={songId}");
                
                // 获取播放列表信息，特别处理"我喜欢的音乐"播放列表
                Playlist playlist = null;
                using (var checkContext = _contextFactory())
                {
                    playlist = await checkContext.Playlists
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == playlistId);
                }
                
                // 如果是我喜欢的音乐播放列表，则调用AddToFavoritesAsync方法
                if (playlist != null && playlist.Title == "我喜欢的音乐")
                {
                    App.Logger.Info($"特殊处理：添加歌曲到'我喜欢的音乐'播放列表: 播放列表ID={playlistId}, 歌曲ID={songId}");
                    await AddToFavoritesAsync(playlist.UserId, songId);
                    return;
                }
                
                // 使用重试机制处理并发问题
                int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using (var context = _contextFactory())
                        {
                            // 检查是否已存在
                            var existing = await context.PlaylistSongs
                                .AsNoTracking() // 避免实体跟踪冲突
                                .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

                            if (existing != null)
                            {
                                App.Logger.Info($"歌曲已在播放列表中，跳过添加: 播放列表ID={playlistId}, 歌曲ID={songId}");
                                return;
                            }

                            // 获取当前最大顺序号
                            int maxOrder = 0;
                            var lastItem = await context.PlaylistSongs
                                .AsNoTracking() // 避免实体跟踪冲突
                                .Where(ps => ps.PlaylistId == playlistId)
                                .OrderByDescending(ps => ps.OrderNumber)
                                .FirstOrDefaultAsync();

                            if (lastItem != null)
                            {
                                maxOrder = lastItem.OrderNumber;
                            }

                            // 添加到播放列表
                            var playlistSong = new PlaylistSong
                            {
                                PlaylistId = playlistId,
                                SongId = songId,
                                OrderNumber = maxOrder + 1
                            };

                            context.PlaylistSongs.Add(playlistSong);

                            // 更新修改时间
                            var playlistToUpdate = await context.Playlists.FindAsync(playlistId);
                            if (playlistToUpdate != null)
                            {
                                playlistToUpdate.ModifiedDate = DateTime.Now;
                            }

                            await context.SaveChangesAsync();
                            App.Logger.Info($"成功添加歌曲到播放列表: 播放列表ID={playlistId}, 歌曲ID={songId}");
                            return; // 成功退出方法
                        }
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        if (attempt == maxRetries)
                        {
                            App.Logger.Error(ex, $"添加歌曲到播放列表失败(并发错误)，已重试{attempt}次: 播放列表ID={playlistId}, 歌曲ID={songId}");
                            throw;
                        }
                        
                        App.Logger.Warn($"添加歌曲到播放列表时发生并发冲突，尝试重试({attempt}/{maxRetries}): 播放列表ID={playlistId}, 歌曲ID={songId}");
                        await Task.Delay(100 * attempt); // 递增延迟重试
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"添加歌曲到播放列表失败: 播放列表ID={playlistId}, 歌曲ID={songId}");
                throw new Exception($"添加歌曲到播放列表失败: {ex.Message}", ex);
            }
        }

        // 从播放列表移除歌曲
        public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    var songToRemove = await context.PlaylistSongs
                        .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

                    if (songToRemove == null)
                        return;

                    context.PlaylistSongs.Remove(songToRemove);

                    // 更新修改时间
                    var playlistToUpdate = await context.Playlists.FindAsync(playlistId);
                    if (playlistToUpdate != null)
                    {
                        playlistToUpdate.ModifiedDate = DateTime.Now;
                    }

                    await context.SaveChangesAsync();

                    // 重新排序
                    await ReorderPlaylistSongsAsync(playlistId);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"从播放列表移除歌曲失败: 播放列表ID={playlistId}, 歌曲ID={songId}");
                throw new Exception($"从播放列表移除歌曲失败: {ex.Message}", ex);
            }
        }

        // 重新排序播放列表歌曲
        public async Task ReorderPlaylistSongsAsync(int playlistId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    var playlistSongs = await context.PlaylistSongs
                        .Where(ps => ps.PlaylistId == playlistId)
                        .OrderBy(ps => ps.OrderNumber)
                        .ToListAsync();

                    int order = 1;
                    foreach (var ps in playlistSongs)
                    {
                        ps.OrderNumber = order++;
                    }

                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"重新排序播放列表歌曲失败: 播放列表ID={playlistId}");
                throw new Exception($"重新排序播放列表歌曲失败: {ex.Message}", ex);
            }
        }

        // 获取播放列表歌曲
        public async Task<List<Song>> GetPlaylistSongsAsync(int playlistId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    var songs = await context.PlaylistSongs
                        .Where(ps => ps.PlaylistId == playlistId)
                        .OrderBy(ps => ps.OrderNumber)
                        .Select(ps => ps.Song)
                        .Include(s => s.Artist)
                        .Include(s => s.Album)
                        .Include(s => s.Genre)
                        .ToListAsync();

                    return songs;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取播放列表歌曲失败: 播放列表ID={playlistId}");
                throw new Exception($"获取播放列表歌曲失败: {ex.Message}", ex);
            }
        }

        // 使用正则表达式清理标题
        private string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "未知标题";

            // 移除前导数字和分隔符（如 "01 - "）
            string cleaned = _cleanTitleRegex.Replace(title, "");

            // 去除多余空格
            cleaned = cleaned.Trim();

            return string.IsNullOrWhiteSpace(cleaned) ? title : cleaned;
        }

        // 从文件名分割艺术家和标题
        public (string artist, string title) SplitArtistAndTitle(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return ("未知艺术家", "未知标题");

            // 尝试从形如 "Artist - Title" 的格式中提取
            var match = _extractArtistRegex.Match(filename);
            if (match.Success)
            {
                string artist = match.Groups[1].Value.Trim();
                string title = filename.Substring(match.Length).Trim();
                return (artist, title);
            }

            // 如果没有找到艺术家，返回整个文件名作为标题
            return ("未知艺术家", filename);
        }

        // 提取特色艺术家
        public string ExtractFeaturedArtist(string title)
        {
            var match = _extractFeatRegex.Match(title);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // 获取专辑的所有歌曲
        public async Task<List<Song>> GetAlbumSongsAsync(int albumId)
        {
            try
            {
                App.Logger.Info($"开始获取专辑(ID: {albumId})的歌曲");
                
                using (var context = _contextFactory())
                {
                    try
                    {
                        // 检查专辑是否存在
                        var album = await context.Albums.FindAsync(albumId);
                        if (album != null)
                        {
                            App.Logger.Info($"找到专辑: '{album.Title}' (ID: {albumId})");
                        }
                        else
                        {
                            App.Logger.Warn($"未找到专辑ID: {albumId}");
                        }
                        
                        // 尝试使用TrackNumber排序
                        var songs = await context.Songs
                            .Include(s => s.Artist)
                            .Include(s => s.Album)
                            .Include(s => s.Genre)
                            .Where(s => s.AlbumId == albumId)
                            .OrderBy(s => s.TrackNumber)
                            .ThenBy(s => s.Title)
                            .ToListAsync();
                            
                        App.Logger.Info($"获取到专辑(ID: {albumId})的歌曲数量: {songs.Count}");
                        return songs;
                    }
                    catch (Exception ex)
                    {
                        // 如果使用TrackNumber失败，回退到只使用Title排序
                        App.Logger.Warn($"无法使用TrackNumber排序，回退到使用Title排序: {ex.Message}");
                        var songs = await context.Songs
                            .Include(s => s.Artist)
                            .Include(s => s.Album)
                            .Include(s => s.Genre)
                            .Where(s => s.AlbumId == albumId)
                            .OrderBy(s => s.Title)
                            .ToListAsync();
                            
                        App.Logger.Info($"获取到专辑(ID: {albumId})的歌曲数量: {songs.Count}");
                        return songs;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取专辑歌曲失败: AlbumId={albumId}");
                throw new Exception($"获取专辑歌曲失败: {ex.Message}", ex);
            }
        }

        // 获取用户收藏的歌曲
        public async Task<List<Song>> GetFavoriteSongsAsync(int userId, bool trySync = true)
        {
            try
            {
                App.Logger.Info($"开始获取用户ID={userId}的收藏歌曲");
                
                using (var context = _contextFactory())
                {
                    // 首先检查FavoriteSongs表是否有数据
                    var favCount = await context.FavoriteSongs.CountAsync(fs => fs.UserId == userId);
                    App.Logger.Info($"用户ID={userId}在FavoriteSongs表中有{favCount}条记录");
                    
                    // 确保正确包含Artist和Album导航属性
                    var songs = await context.FavoriteSongs
                        .Where(fs => fs.UserId == userId)
                        .Include(fs => fs.Song)
                        .ThenInclude(s => s.Artist)
                        .Include(fs => fs.Song)
                        .ThenInclude(s => s.Album)
                        .Include(fs => fs.Song.Genre)
                        .Select(fs => new Song
                        {
                            Id = fs.Song.Id,
                            Title = fs.Song.Title,
                            FilePath = fs.Song.FilePath,
                            Duration = fs.Song.Duration,
                            TrackNumber = fs.Song.TrackNumber,
                            Rating = fs.Song.Rating,
                            PlayCount = fs.Song.PlayCount,
                            LastPlayedDate = fs.Song.LastPlayedDate,
                            ArtistId = fs.Song.ArtistId,
                            Artist = fs.Song.Artist,
                            AlbumId = fs.Song.AlbumId,
                            Album = fs.Song.Album,
                            GenreId = fs.Song.GenreId,
                            Genre = fs.Song.Genre,
                            AlbumArt = fs.Song.AlbumArt
                        })
                        .ToListAsync();

                    App.Logger.Info($"成功获取用户ID={userId}的收藏歌曲，共{songs.Count}首");
                    
                    // 如果没有收藏歌曲，尝试同步"我喜欢的音乐"播放列表中的歌曲
                    // 添加trySync参数防止无限递归
                    if (songs.Count == 0 && trySync)
                    {
                        App.Logger.Info("没有收藏歌曲，尝试同步播放列表中的歌曲");
                        await SyncFavoritesToPlaylistAsync(userId);
                        
                        // 再次尝试获取，但不再尝试同步以避免无限递归
                        songs = await GetFavoriteSongsAsync(userId, false);
                        App.Logger.Info($"同步后再次获取，共{songs.Count}首");
                    }

                    return songs;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取收藏歌曲失败");
                throw new Exception($"获取收藏歌曲失败: {ex.Message}", ex);
            }
        }

        // 添加到收藏夹
        public async Task AddToFavoritesAsync(int userId, int songId)
        {
            try
            {
                App.Logger.Info($"尝试添加歌曲到收藏: 用户ID={userId}, 歌曲ID={songId}");
                
                using (var context = _contextFactory())
                {
                    // 检查是否已存在
                    var existing = await context.FavoriteSongs
                        .Where(fs => fs.UserId == userId)
                        .Where(fs => fs.SongId == songId)
                        .FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        App.Logger.Info($"歌曲已在收藏中，跳过添加: 用户ID={userId}, 歌曲ID={songId}");
                        return;
                    }

                    // 添加到收藏夹
                    var favoriteSong = new FavoriteSong
                    {
                        UserId = userId,
                        SongId = songId,
                        AddedDate = DateTime.Now
                    };

                    App.Logger.Info($"正在添加歌曲到收藏: 用户ID={userId}, 歌曲ID={songId}");
                    context.FavoriteSongs.Add(favoriteSong);
                    await context.SaveChangesAsync();
                    App.Logger.Info($"成功添加歌曲到收藏: 用户ID={userId}, 歌曲ID={songId}");

                    // 同步到"我喜欢的音乐"播放列表
                    try
                    {
                        var favoritesPlaylist = await GetOrCreateFavoritesPlaylistAsync(userId);
                        await AddSongToPlaylistAsync(favoritesPlaylist.Id, songId);
                        App.Logger.Info($"歌曲已添加到'我喜欢的音乐'播放列表: 用户ID={userId}, 歌曲ID={songId}");
                    }
                    catch (Exception innerEx)
                    {
                        // 记录错误但不中断主流程
                        App.Logger.Error(innerEx, $"添加歌曲到'我喜欢的音乐'播放列表失败，但已添加到收藏: 歌曲ID={songId}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"添加到收藏夹失败: 用户ID={userId}, 歌曲ID={songId}");
                throw new Exception($"添加到收藏夹失败: {ex.Message}", ex);
            }
        }

        // 从收藏夹移除
        public async Task RemoveFromFavoritesAsync(int userId, int songId)
        {
            try
            {
                App.Logger.Info($"尝试从收藏中移除歌曲: 用户ID={userId}, 歌曲ID={songId}");
                
                using (var context = _contextFactory())
                {
                    // 直接使用SQL参数查询，避免使用实体属性名称
                    var favoriteSong = await context.FavoriteSongs
                        .Where(fs => fs.UserId == userId)
                        .Where(fs => fs.SongId == songId)
                        .FirstOrDefaultAsync();

                    if (favoriteSong == null)
                    {
                        App.Logger.Info($"未找到要移除的收藏歌曲: 用户ID={userId}, 歌曲ID={songId}");
                        return;
                    }

                    App.Logger.Info($"找到要移除的收藏歌曲记录ID={favoriteSong.Id}，正在删除");
                    context.FavoriteSongs.Remove(favoriteSong);
                    await context.SaveChangesAsync();
                    App.Logger.Info($"已成功从收藏中移除歌曲: 用户ID={userId}, 歌曲ID={songId}");

                                // 从"我喜欢的音乐"播放列表中移除
                    try 
                    {
                        // 为避免并发问题，添加短暂延迟
                        await Task.Delay(200);
                        
                        var favoritesPlaylist = await GetOrCreateFavoritesPlaylistAsync(userId);
                        await RemoveSongFromPlaylistAsync(favoritesPlaylist.Id, songId);
                    }
                    catch (Exception innerEx)
                    {
                        // 记录错误但不中断主流程
                        App.Logger.Error(innerEx, $"从'我喜欢的音乐'播放列表移除歌曲失败，但歌曲已从收藏中移除: 歌曲ID={songId}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"从收藏夹移除失败: 用户ID={userId}, 歌曲ID={songId}");
                throw new Exception($"从收藏夹移除失败: {ex.Message}", ex);
            }
        }

        // 检查歌曲是否已收藏
        public async Task<bool> IsSongFavoritedAsync(int userId, int songId)
        {
            try
            {
                App.Logger.Debug($"检查歌曲收藏状态: 用户ID={userId}, 歌曲ID={songId}");
                
                using (var context = _contextFactory())
                {
                    // 使用两个单独的Where子句而不是组合条件
                    bool isFavorited = await context.FavoriteSongs
                        .Where(fs => fs.UserId == userId)
                        .Where(fs => fs.SongId == songId)
                        .AnyAsync();
                    
                    App.Logger.Debug($"歌曲收藏状态: 用户ID={userId}, 歌曲ID={songId}, 是否收藏={isFavorited}");
                    return isFavorited;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"检查歌曲收藏状态失败: 用户ID={userId}, 歌曲ID={songId}");
                // 返回false而不是抛出异常，避免UI错误
                return false;
            }
        }

        // 获取最近播放的歌曲
        public async Task<List<Song>> GetRecentlyPlayedSongsAsync(int userId, int limit = 10)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 根据LastPlayedDate获取最近播放的歌曲，暂时忽略userId
                    // 实际应用中应该有一个PlayHistory表来记录每个用户的播放历史
                    var songs = await context.Songs
                        .Where(s => s.LastPlayedDate != null)
                        .OrderByDescending(s => s.LastPlayedDate)
                        .Take(limit)
                        .Include(s => s.Artist)
                        .Include(s => s.Album)
                        .Include(s => s.Genre)
                        .ToListAsync();

                    return songs;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取最近播放歌曲失败");
                throw new Exception($"获取最近播放歌曲失败: {ex.Message}", ex);
            }
        }

        // 获取播放次数最多的歌曲
        public async Task<List<Song>> GetMostPlayedSongsAsync(int userId, int limit = 10)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 根据PlayCount获取最常播放的歌曲，暂时忽略userId
                    // 实际应用中应该有一个PlayHistory表来记录每个用户的播放计数
                    var songs = await context.Songs
                        .Where(s => s.PlayCount > 0)
                        .OrderByDescending(s => s.PlayCount)
                        .Take(limit)
                        .Include(s => s.Artist)
                        .Include(s => s.Album)
                        .Include(s => s.Genre)
                        .ToListAsync();

                    return songs;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取最常播放歌曲失败");
                throw new Exception($"获取最常播放歌曲失败: {ex.Message}", ex);
            }
        }

        // 更新播放列表
        public async Task UpdatePlaylistAsync(Playlist playlist)
        {
            if (playlist == null)
                throw new ArgumentNullException(nameof(playlist), "播放列表不能为空");

            try
            {
                using (var context = _contextFactory())
                {
                    context.Entry(playlist).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    App.Logger.Info($"已更新播放列表: {playlist.Title}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"更新播放列表失败: {playlist.Title}");
                throw new Exception($"更新播放列表失败: {ex.Message}", ex);
            }
        }

        // 删除播放列表
        public async Task DeletePlaylistAsync(int playlistId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 先删除关联的播放列表歌曲
                    var playlistSongs = await context.PlaylistSongs
                        .Where(ps => ps.PlaylistId == playlistId)
                        .ToListAsync();

                    context.PlaylistSongs.RemoveRange(playlistSongs);

                    // 再删除播放列表
                    var playlistToDelete = await context.Playlists.FindAsync(playlistId);
                    if (playlistToDelete != null)
                    {
                        context.Playlists.Remove(playlistToDelete);
                        await context.SaveChangesAsync();
                        App.Logger.Info($"已删除播放列表ID: {playlistId}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"删除播放列表失败: {playlistId}");
                throw new Exception($"删除播放列表失败: {ex.Message}", ex);
            }
        }

        // 添加专辑到收藏
        public async Task AddToFavoriteAlbumsAsync(int userId, int albumId)
        {
            try
            {
                App.Logger.Info($"开始将专辑添加到收藏: 用户ID={userId}, 专辑ID={albumId}");
                
                using (var context = _contextFactory())
                {
                    // 检查是否已收藏
                    var existing = await context.FavoriteAlbums
                        .FirstOrDefaultAsync(fa => fa.UserId == userId && fa.AlbumId == albumId);

                    if (existing != null)
                    {
                        App.Logger.Info($"专辑已在收藏中，跳过添加: 用户ID={userId}, 专辑ID={albumId}");
                        return;
                    }

                    // 添加到收藏
                    var favoriteAlbum = new FavoriteAlbum
                    {
                        UserId = userId,
                        AlbumId = albumId,
                        AddedDate = DateTime.Now
                    };

                    App.Logger.Info($"正在将专辑添加到FavoriteAlbums表: 用户ID={userId}, 专辑ID={albumId}");
                    context.FavoriteAlbums.Add(favoriteAlbum);
                    await context.SaveChangesAsync();
                    App.Logger.Info($"专辑已成功添加到收藏: 用户ID={userId}, 专辑ID={albumId}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"添加专辑到收藏失败: 用户ID={userId}, 专辑ID={albumId}");
                throw new Exception($"添加专辑到收藏失败: {ex.Message}", ex);
            }
        }

        // 从收藏中移除专辑
        public async Task RemoveFromFavoriteAlbumsAsync(int userId, int albumId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    var favorite = await context.FavoriteAlbums
                        .FirstOrDefaultAsync(fa => fa.UserId == userId && fa.AlbumId == albumId);

                    if (favorite == null)
                        return;

                    context.FavoriteAlbums.Remove(favorite);
                    await context.SaveChangesAsync();
                    App.Logger.Info($"专辑已从收藏中移除: 用户ID={userId}, 专辑ID={albumId}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"从收藏中移除专辑失败: 用户ID={userId}, 专辑ID={albumId}");
                throw new Exception($"从收藏中移除专辑失败: {ex.Message}", ex);
            }
        }

        // 检查专辑是否已收藏
        public async Task<bool> IsAlbumFavoritedAsync(int userId, int albumId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.FavoriteAlbums
                        .AnyAsync(fa => fa.UserId == userId && fa.AlbumId == albumId);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"检查专辑收藏状态失败: 用户ID={userId}, 专辑ID={albumId}");
                throw new Exception($"检查专辑收藏状态失败: {ex.Message}", ex);
            }
        }

        // 获取用户收藏的专辑
        public async Task<List<Album>> GetFavoriteAlbumsAsync(int userId)
        {
            try
            {
                App.Logger.Info($"开始获取用户ID={userId}的收藏专辑");
                
                using (var context = _contextFactory())
                {
                    // 使用标准LINQ查询
                    var favoriteAlbumIds = await context.FavoriteAlbums
                        .Where(fa => fa.UserId == userId)
                        .Select(fa => fa.AlbumId)
                        .ToListAsync();
                        
                    App.Logger.Info($"获取到收藏专辑ID列表，数量: {favoriteAlbumIds.Count}");
                    
                    if (favoriteAlbumIds.Count == 0)
                    {
                        App.Logger.Info("没有找到收藏的专辑ID，返回空列表");
                        return new List<Album>();
                    }
                    
                    var albums = await context.Albums
                        .Where(a => favoriteAlbumIds.Contains(a.Id))
                        .Include(a => a.Artist)
                        .ToListAsync();
                        
                    App.Logger.Info($"获取到收藏专辑详情，数量: {albums.Count}");
                    return albums;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取收藏专辑失败: 用户ID={userId}");
                throw new Exception($"获取收藏专辑失败: {ex.Message}", ex);
            }
        }

        // 获取或创建"我喜欢的音乐"播放列表
        public async Task<Playlist> GetOrCreateFavoritesPlaylistAsync(int userId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 查找用户的"我喜欢的音乐"播放列表
                    var favoritesPlaylist = await context.Playlists
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.Title == "我喜欢的音乐");

                    // 如果不存在则创建
                    if (favoritesPlaylist == null)
                    {
                        favoritesPlaylist = new Playlist
                        {
                            Title = "我喜欢的音乐",
                            UserId = userId,
                            Description = "我收藏的歌曲",
                            AddedDate = DateTime.Now,
                            ModifiedDate = DateTime.Now
                        };

                        context.Playlists.Add(favoritesPlaylist);
                        await context.SaveChangesAsync();
                    }

                    return favoritesPlaylist;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "获取或创建收藏播放列表失败");
                throw new Exception($"获取或创建收藏播放列表失败: {ex.Message}", ex);
            }
        }

        // 将收藏的歌曲同步到"我喜欢的音乐"播放列表
        public async Task SyncFavoritesToPlaylistAsync(int userId)
        {
            try
            {
                App.Logger.Info($"开始同步用户ID={userId}的收藏歌曲到\"我喜欢的音乐\"播放列表");
                
                // 最大重试次数
                int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        // 获取收藏播放列表
                        var favoritesPlaylist = await GetOrCreateFavoritesPlaylistAsync(userId);
                        
                        // 获取所有收藏的歌曲和当前播放列表歌曲 - 在同一个上下文中完成以保持一致性
                        List<Song> favoriteSongs;
                        List<int> currentSongIds;
                        
                        using (var context = _contextFactory())
                        {
                            // 优化查询方式，使用AsNoTracking减少内存使用和避免跟踪冲突
                            favoriteSongs = await context.FavoriteSongs
                                .AsNoTracking()
                                .Where(fs => fs.UserId == userId)
                                .Join(
                                    context.Songs,
                                    fs => fs.SongId,
                                    s => s.Id,
                                    (fs, s) => new Song
                                    {
                                        Id = s.Id,
                                        Title = s.Title,
                                        ArtistId = s.ArtistId,
                                        AlbumId = s.AlbumId
                                    }
                                )
                                .ToListAsync();
                            
                            App.Logger.Info($"从FavoriteSongs表获取到{favoriteSongs.Count}首歌曲");
                            
                            // 获取当前播放列表中的所有歌曲ID
                            currentSongIds = await context.PlaylistSongs
                                .AsNoTracking()
                                .Where(ps => ps.PlaylistId == favoritesPlaylist.Id)
                                .Select(ps => ps.SongId)
                                .ToListAsync();
                            
                            App.Logger.Info($"播放列表中已有{currentSongIds.Count}首歌曲");
                            
                            // 收藏中有但播放列表中没有的歌曲需要添加
                            var favoriteSongIds = favoriteSongs.Select(s => s.Id).ToList();
                            var songsToAdd = favoriteSongIds.Where(id => !currentSongIds.Contains(id)).ToList();
                            
                            // 播放列表中有但收藏中没有的歌曲需要移除
                            var songsToRemove = currentSongIds.Where(id => !favoriteSongIds.Contains(id)).ToList();
                            
                            App.Logger.Info($"需要添加{songsToAdd.Count}首歌曲, 需要移除{songsToRemove.Count}首歌曲");
                            
                            // 对于同步操作，尝试使用批量操作来提高效率和避免并发问题
                            if (songsToAdd.Count > 0)
                            {
                                // 批量添加歌曲到播放列表
                                int maxOrder = 0;
                                if (currentSongIds.Count > 0)
                                {
                                    maxOrder = await context.PlaylistSongs
                                        .Where(ps => ps.PlaylistId == favoritesPlaylist.Id)
                                        .MaxAsync(ps => (int?)ps.OrderNumber) ?? 0;
                                }
                                
                                var playlistSongs = new List<PlaylistSong>();
                                foreach (var songId in songsToAdd)
                                {
                                    playlistSongs.Add(new PlaylistSong
                                    {
                                        PlaylistId = favoritesPlaylist.Id,
                                        SongId = songId,
                                        OrderNumber = ++maxOrder
                                    });
                                }
                                
                                context.PlaylistSongs.AddRange(playlistSongs);
                                
                                // 更新播放列表修改时间
                                var playlistToUpdate = await context.Playlists.FindAsync(favoritesPlaylist.Id);
                                if (playlistToUpdate != null)
                                {
                                    playlistToUpdate.ModifiedDate = DateTime.Now;
                                }
                                
                                await context.SaveChangesAsync();
                                App.Logger.Info($"批量添加了{playlistSongs.Count}首歌曲到播放列表");
                            }
                            
                            // 清空上下文避免跟踪冲突
                            context.ChangeTracker.Entries().ToList().ForEach(entry => entry.State = EntityState.Detached);
                            
                            if (songsToRemove.Count > 0)
                            {
                                // 批量删除歌曲
                                var playlistSongsToRemove = await context.PlaylistSongs
                                    .Where(ps => ps.PlaylistId == favoritesPlaylist.Id && songsToRemove.Contains(ps.SongId))
                                    .ToListAsync();
                                
                                context.PlaylistSongs.RemoveRange(playlistSongsToRemove);
                                
                                // 更新播放列表修改时间
                                var playlistToUpdate = await context.Playlists.FindAsync(favoritesPlaylist.Id);
                                if (playlistToUpdate != null)
                                {
                                    playlistToUpdate.ModifiedDate = DateTime.Now;
                                }
                                
                                await context.SaveChangesAsync();
                                App.Logger.Info($"批量移除了{playlistSongsToRemove.Count}首歌曲从播放列表");
                            }
                        }
                        
                        if (!await IsFavoritesPlaylistInSyncAsync(userId))
                        {
                            // 如果批量操作后仍不同步，退回到逐个处理
                            App.Logger.Warn("批量同步不完整，正在进行增量同步");
                            await PerformIncrementalSyncAsync(userId);
                        }
                        
                        App.Logger.Info($"成功同步收藏歌曲到\"我喜欢的音乐\"播放列表");
                        return; // 成功完成同步
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        if (attempt == maxRetries)
                        {
                            App.Logger.Error(ex, $"同步收藏歌曲失败(并发错误)，已重试{attempt}次");
                            throw;
                        }
                        
                        App.Logger.Warn($"同步收藏歌曲时发生并发冲突，尝试重试({attempt}/{maxRetries})");
                        await Task.Delay(500 * attempt); // 递增延迟重试
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "同步收藏歌曲到播放列表失败");
                throw new Exception($"同步收藏歌曲到播放列表失败: {ex.Message}", ex);
            }
        }
        
        // 检查"我喜欢的音乐"播放列表是否与收藏同步
        private async Task<bool> IsFavoritesPlaylistInSyncAsync(int userId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 获取收藏播放列表
                    var favoritesPlaylist = await context.Playlists
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.Title == "我喜欢的音乐");
                        
                    if (favoritesPlaylist == null)
                        return false;
                    
                    // 收藏歌曲IDs
                    var favoriteSongIds = await context.FavoriteSongs
                        .AsNoTracking()
                        .Where(fs => fs.UserId == userId)
                        .Select(fs => fs.SongId)
                        .ToListAsync();
                    
                    // 播放列表歌曲IDs
                    var playlistSongIds = await context.PlaylistSongs
                        .AsNoTracking()
                        .Where(ps => ps.PlaylistId == favoritesPlaylist.Id)
                        .Select(ps => ps.SongId)
                        .ToListAsync();
                    
                    // 检查两个列表是否匹配
                    return favoriteSongIds.Count == playlistSongIds.Count &&
                           !favoriteSongIds.Except(playlistSongIds).Any() &&
                           !playlistSongIds.Except(favoriteSongIds).Any();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "检查收藏同步状态失败");
                return false;
            }
        }
        
        // 执行增量同步操作
        private async Task PerformIncrementalSyncAsync(int userId)
        {
            try
            {
                // 获取收藏播放列表
                var favoritesPlaylist = await GetOrCreateFavoritesPlaylistAsync(userId);
                
                List<int> favoriteSongIds;
                List<int> playlistSongIds;
                
                using (var context = _contextFactory())
                {
                    // 收藏歌曲IDs
                    favoriteSongIds = await context.FavoriteSongs
                        .AsNoTracking()
                        .Where(fs => fs.UserId == userId)
                        .Select(fs => fs.SongId)
                        .ToListAsync();
                    
                    // 播放列表歌曲IDs
                    playlistSongIds = await context.PlaylistSongs
                        .AsNoTracking()
                        .Where(ps => ps.PlaylistId == favoritesPlaylist.Id)
                        .Select(ps => ps.SongId)
                        .ToListAsync();
                }
                
                // 需要添加的歌曲
                var songsToAdd = favoriteSongIds.Except(playlistSongIds).ToList();
                // 需要移除的歌曲
                var songsToRemove = playlistSongIds.Except(favoriteSongIds).ToList();
                
                // 逐个添加
                foreach (var songId in songsToAdd)
                {
                    await AddSongToPlaylistAsync(favoritesPlaylist.Id, songId);
                    await Task.Delay(50);
                }
                
                // 逐个移除
                foreach (var songId in songsToRemove)
                {
                    await RemoveSongFromPlaylistAsync(favoritesPlaylist.Id, songId);
                    await Task.Delay(50);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "增量同步失败");
                throw;
            }
        }

        // 搜索专辑
        public async Task<List<Album>> SearchAlbumsAsync(string query)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    IQueryable<Album> albumsQuery = context.Albums
                        .Include(a => a.Artist);

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        query = query.ToLower();
                        albumsQuery = albumsQuery.Where(a =>
                            a.Title.ToLower().Contains(query) ||
                            a.Artist.Name.ToLower().Contains(query));
                    }

                    return await albumsQuery.ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "搜索专辑失败");
                throw new Exception($"搜索专辑失败: {ex.Message}", ex);
            }
        }

        // 搜索艺术家
        public async Task<List<Artist>> SearchArtistsAsync(string query)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    IQueryable<Artist> artistsQuery = context.Artists;

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        query = query.ToLower();
                        artistsQuery = artistsQuery.Where(a => a.Name.ToLower().Contains(query));
                    }

                    return await artistsQuery.ToListAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "搜索艺术家失败");
                throw new Exception($"搜索艺术家失败: {ex.Message}", ex);
            }
        }
        
        // 获取艺术家的专辑
        public async Task<List<Album>> GetArtistAlbumsAsync(int artistId)
        {
            try
            {
                App.Logger.Info($"开始获取艺术家(ID: {artistId})的专辑");
                
                using (var context = _contextFactory())
                {
                    var albums = await context.Albums
                        .Where(a => a.ArtistId == artistId)
                        .Include(a => a.Artist)
                        .OrderBy(a => a.Title)
                        .ToListAsync();
                    
                    App.Logger.Info($"获取到艺术家(ID: {artistId})的专辑数量: {albums.Count}");
                    return albums;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取艺术家专辑失败: ArtistId={artistId}");
                throw new Exception($"获取艺术家专辑失败: {ex.Message}", ex);
            }
        }
        
        // 获取艺术家的歌曲
        public async Task<List<Song>> GetArtistSongsAsync(int artistId)
        {
            try
            {
                App.Logger.Info($"开始获取艺术家(ID: {artistId})的歌曲");
                
                using (var context = _contextFactory())
                {
                    var songs = await context.Songs
                        .Where(s => s.ArtistId == artistId)
                        .Include(s => s.Artist)
                        .Include(s => s.Album)
                        .OrderBy(s => s.Title)
                        .ToListAsync();
                    
                    App.Logger.Info($"获取到艺术家(ID: {artistId})的歌曲数量: {songs.Count}");
                    return songs;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取艺术家歌曲失败: ArtistId={artistId}");
                throw new Exception($"获取艺术家歌曲失败: {ex.Message}", ex);
            }
        }

        // 临时方法：添加测试数据
        public async Task AddTestDataAsync()
        {
            try
            {
                App.Logger.Info("开始添加测试数据");
                
                using (var context = _contextFactory())
                {
                    // 检查是否已存在测试数据
                    var artistExists = await context.Artists.AnyAsync(a => a.Name == "测试艺术家");
                    var albumExists = await context.Albums.AnyAsync(a => a.Title == "测试专辑");
                    
                    if (artistExists && albumExists)
                    {
                        App.Logger.Info("测试数据已存在，跳过添加");
                        return;
                    }
                    
                    // 添加测试艺术家
                    var artist = new Artist
                    {
                        Name = "测试艺术家",
                        AddedDate = DateTime.Now,
                        Title = "测试艺术家"
                    };
                    
                    context.Artists.Add(artist);
                    await context.SaveChangesAsync();
                    
                    App.Logger.Info($"已添加测试艺术家，ID: {artist.Id}");
                    
                    // 添加测试专辑
                    var album = new Album
                    {
                        Title = "测试专辑",
                        ArtistId = artist.Id,
                        Artist = artist,
                        AddedDate = DateTime.Now,
                        Year = 2023
                    };
                    
                    context.Albums.Add(album);
                    await context.SaveChangesAsync();
                    
                    App.Logger.Info($"已添加测试专辑，ID: {album.Id}");
                    
                    // 添加测试歌曲
                    for (int i = 1; i <= 5; i++)
                    {
                        var song = new Song
                        {
                            Title = $"测试歌曲 {i}",
                            ArtistId = artist.Id,
                            Artist = artist,
                            AlbumId = album.Id,
                            Album = album,
                            Duration = 180 + i * 10,
                            TrackNumber = i,
                            FilePath = $"D:\\Music\\test_song_{i}.mp3",
                            AddedDate = DateTime.Now
                        };
                        
                        context.Songs.Add(song);
                    }
                    
                    await context.SaveChangesAsync();
                    App.Logger.Info("已添加5首测试歌曲");
                    
                    MessageBox.Show("测试数据已成功添加！", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "添加测试数据失败");
                MessageBox.Show($"添加测试数据失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}