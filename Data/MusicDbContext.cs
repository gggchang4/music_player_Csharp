using Microsoft.EntityFrameworkCore;
using MusicPlayerApp.Models;
using System;
using System.IO;
using System.Windows;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace MusicPlayerApp.Data
{
    // 继承自DbContext，用于数据库交互
    public class MusicDbContext : DbContext
    {
        // 定义DbSet属性，对应数据库中的表
        public DbSet<Song> Songs { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<PlaylistSong> PlaylistSongs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<FavoriteSong> FavoriteSongs { get; set; }
        public DbSet<FavoriteAlbum> FavoriteAlbums { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            try
            {
                // 创建应用数据目录
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayerApp");

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                string dbPath = Path.Combine(appDataPath, "musicplayer.db");

                // 使用SQLite作为数据库
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库配置错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置PlaylistSong多对多关系
            modelBuilder.Entity<PlaylistSong>()
                .HasKey(ps => new { ps.PlaylistId, ps.SongId });

            modelBuilder.Entity<PlaylistSong>()
                .HasOne(ps => ps.Playlist)
                .WithMany(p => p.PlaylistSongs)
                .HasForeignKey(ps => ps.PlaylistId);

            modelBuilder.Entity<PlaylistSong>()
                .HasOne(ps => ps.Song)
                .WithMany(s => s.PlaylistSongs)
                .HasForeignKey(ps => ps.SongId);

            // 配置UserSettings一对一关系
            modelBuilder.Entity<UserSettings>()
                .HasKey(us => us.UserId);

            modelBuilder.Entity<UserSettings>()
                .HasOne(us => us.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(us => us.UserId);

            // 其他配置
            modelBuilder.Entity<Song>()
                .HasOne(s => s.Artist)
                .WithMany(a => a.Songs)
                .HasForeignKey(s => s.ArtistId);

            modelBuilder.Entity<Song>()
                .HasOne(s => s.Album)
                .WithMany(a => a.Songs)
                .HasForeignKey(s => s.AlbumId);

            modelBuilder.Entity<Song>()
                .HasOne(s => s.Genre)
                .WithMany(g => g.Songs)
                .HasForeignKey(s => s.GenreId);

            modelBuilder.Entity<Album>()
                .HasOne(a => a.Artist)
                .WithMany(a => a.Albums)
                .HasForeignKey(a => a.ArtistId);

            modelBuilder.Entity<Playlist>()
                .HasOne(p => p.User)
                .WithMany(u => u.Playlists)
                .HasForeignKey(p => p.UserId);

            // 配置FavoriteSongs表
            modelBuilder.Entity<FavoriteSong>()
                .HasKey(fs => fs.Id);
            
            modelBuilder.Entity<FavoriteSong>()
                .Property(fs => fs.Id)
                .ValueGeneratedOnAdd();
            
            modelBuilder.Entity<FavoriteSong>()
                .HasOne(fs => fs.User)
                .WithMany()
                .HasForeignKey(fs => fs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<FavoriteSong>()
                .HasOne(fs => fs.Song)
                .WithMany()
                .HasForeignKey(fs => fs.SongId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // 配置FavoriteAlbums表
            modelBuilder.Entity<FavoriteAlbum>()
                .HasKey(fa => fa.Id);
            
            modelBuilder.Entity<FavoriteAlbum>()
                .Property(fa => fa.Id)
                .ValueGeneratedOnAdd();
            
            modelBuilder.Entity<FavoriteAlbum>()
                .HasOne(fa => fa.User)
                .WithMany()
                .HasForeignKey(fa => fa.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<FavoriteAlbum>()
                .HasOne(fa => fa.Album)
                .WithMany()
                .HasForeignKey(fa => fa.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        /// <summary>
        /// 检查并更新数据库结构，添加缺失的字段
        /// </summary>
        public static void CheckAndUpdateSchema()
        {
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayerApp");
                string dbPath = Path.Combine(appDataPath, "musicplayer.db");
                
                if (!File.Exists(dbPath))
                    return; // 数据库不存在，会在后续步骤中创建
                
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    
                    // 检查UserSettings表结构
                    var userSettingsInfo = GetTableInfo(connection, "UserSettings");
                    
                    // 检查并添加缺失的字段
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "AutoPlayOnStartup", "INTEGER", "0");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "RememberPlaybackPosition", "INTEGER", "1");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "CrossFade", "INTEGER", "1");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "CrossFadeDuration", "INTEGER", "2");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "AudioFormatIndex", "INTEGER", "0");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "ShowAnimations", "INTEGER", "1");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "AlwaysShowLyrics", "INTEGER", "0");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "MusicLibraryPath", "TEXT", "''");
                    AddMissingColumnIfNeeded(connection, "UserSettings", userSettingsInfo, "CacheSize", "INTEGER", "500");
                    
                    // 检查Users表结构
                    var usersInfo = GetTableInfo(connection, "Users");
                    
                    // 检查并添加用户表缺失的字段
                    AddMissingColumnIfNeeded(connection, "Users", usersInfo, "Bio", "TEXT", "NULL");
                    AddMissingColumnIfNeeded(connection, "Users", usersInfo, "AvatarColor", "TEXT", "'#7B1FA2'");
                    AddMissingColumnIfNeeded(connection, "Users", usersInfo, "AvatarChar", "TEXT", "'用'");
                    AddMissingColumnIfNeeded(connection, "Users", usersInfo, "AvatarImagePath", "TEXT", "NULL");
                    AddMissingColumnIfNeeded(connection, "Users", usersInfo, "UpdatedDate", "DATETIME", "NULL");
                }
                
                App.Logger.Info("数据库结构检查和更新完成");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "检查和更新数据库结构失败");
            }
        }
        
        /// <summary>
        /// 获取表的列信息
        /// </summary>
        private static string[] GetTableInfo(SqliteConnection connection, string tableName)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info([{tableName}]);";
                
                using (var reader = cmd.ExecuteReader())
                {
                    var columns = new System.Collections.Generic.List<string>();
                    while (reader.Read())
                    {
                        columns.Add(reader["name"].ToString());
                    }
                    
                    return columns.ToArray();
                }
            }
        }
        
        /// <summary>
        /// 如果需要，添加缺失的列
        /// </summary>
        private static void AddMissingColumnIfNeeded(SqliteConnection connection, string tableName, string[] existingColumns, string columnName, string dataType, string defaultValue)
        {
            if (!existingColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            {
                using (var cmd = connection.CreateCommand())
                {
                    // 在SQLite中添加列语法
                    cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {dataType} DEFAULT {defaultValue};";
                    cmd.ExecuteNonQuery();
                    App.Logger.Info($"已添加列: {tableName}.{columnName} ({dataType})");
                }
            }
        }
    }
}