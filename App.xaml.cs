using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MusicPlayerApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using MusicPlayerApp.Services;
using NLog;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace MusicPlayerApp
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        // 全局日志实例
        public static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 设置Entity Framework对缺失列宽容处理
            AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue9825", true);
            
            base.OnStartup(e);

            // 临时重置数据库，以解决列缺失问题
            ResetDatabase();
            
            // 检查并更新数据库结构
            try
            {
                MusicPlayerApp.Data.MusicDbContext.CheckAndUpdateSchema();
                Logger.Info("数据库模式更新检查完成");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "数据库模式更新失败");
            }

            // 准备动态资源（确保主题资源立即可用）
            ResourceDictionary resources = Current.Resources;
            resources["TopBarBackground"] = resources["BackgroundBrush"];
            resources["SideNavBackground"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1A1A1A"));
            resources["ContentBackground"] = resources["BackgroundBrush"];
            resources["PlayBarBackground"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2D2D2D"));
            resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#333333"));
            resources["HoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3A3A3A"));
            Logger.Info("初始动态资源已准备就绪");

            // 设置全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Logger.Fatal(ex, "未处理的异常");
                MessageBox.Show($"发生严重错误：{ex?.Message}\n\n{ex?.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // 处理WPF异常
            DispatcherUnhandledException += (s, args) =>
            {
                Logger.Error(args.Exception, "未处理的UI线程异常");
                MessageBox.Show($"UI线程发生未处理的异常: {args.Exception.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // 记录应用程序启动
            Logger.Info("应用程序启动");

            try
            {
                // 设置日志目录
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayerApp", "Logs");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // 配置NLog
                var config = new NLog.Config.LoggingConfiguration();
                var logfile = new NLog.Targets.FileTarget("logfile")
                {
                    FileName = Path.Combine(logDirectory, "app-${shortdate}.log"),
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}"
                };
                config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logfile);
                NLog.LogManager.Configuration = config;

                Logger.Info("应用程序启动");

                // 初始化数据库 - 只在必要时才重建
                using (var context = new MusicDbContext())
                {
                    try
                    {
                        Logger.Info("开始准备数据库...");

                        // 输出数据库路径以便调试
                        var connectionString = context.Database.GetDbConnection().ConnectionString;
                        Logger.Info($"数据库连接字符串: {connectionString}");

                        // 尝试检查数据库结构是否正常
                        bool hasStructureIssue = false;
                        try
                        {
                            // 尝试查询FavoriteSongs表，检测可能的结构问题
                            context.FavoriteSongs.Take(1).ToList();
                            Logger.Info("FavoriteSongs表结构正常");
                        }
                        catch (Exception tableEx)
                        {
                            Logger.Error(tableEx, "FavoriteSongs表结构异常，可能需要重建数据库");
                            hasStructureIssue = true;
                        }

                        // 如果检测到结构问题，尝试重建数据库
                        if (hasStructureIssue)
                        {
                            Logger.Info("检测到数据库结构问题，将尝试重建数据库");
                            try
                            {
                                // 关闭当前连接
                                Logger.Info("关闭当前数据库连接");
                                context.Dispose();
                                
                                // 强制GC回收，确保所有连接被释放
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                
                                // 尝试手动删除数据库文件
                                string appDataPath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "MusicPlayerApp");
                                string dbPath = Path.Combine(appDataPath, "musicplayer.db");
                                
                                Logger.Info($"尝试手动删除数据库文件: {dbPath}");
                                if (File.Exists(dbPath))
                                {
                                    try
                                    {
                                        File.Delete(dbPath);
                                        Logger.Info("已手动删除数据库文件");
                                    }
                                    catch (IOException ioEx)
                                    {
                                        Logger.Error(ioEx, "无法删除数据库文件，它可能正被另一个程序使用");
                                        
                                        // 提示用户手动删除文件
                                        MessageBoxResult result = MessageBox.Show(
                                            $"数据库文件被锁定，无法自动修复。\n\n您需要手动删除以下文件后重启应用：\n{dbPath}\n\n是否打开文件所在位置？",
                                            "数据库锁定",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Warning);
                                                
                                        if (result == MessageBoxResult.Yes)
                                        {
                                            // 打开文件夹
                                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dbPath}\"");
                                        }
                                        
                                        // 退出应用
                                        Current.Shutdown();
                                        return;
                                    }
                                }
                                
                                // 使用新的上下文重建数据库
                                using (var newContext = new MusicDbContext())
                                {
                                    newContext.Database.EnsureCreated();
                                    Logger.Info("已重新创建数据库");
                                    
                                    // 重新初始化基础数据
                                    DbInitializer.Initialize(newContext);
                                    Logger.Info("数据库重新初始化完成");
                                }

                                // 显示成功消息
                                MessageBox.Show("数据库结构问题已修复，您的收藏数据已重置。", 
                                    "数据库修复", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                            }
                            catch (Exception rebuildEx)
                            {
                                Logger.Error(rebuildEx, "重建数据库失败");
                                MessageBox.Show($"重建数据库失败: {rebuildEx.Message}\n\n请尝试关闭所有可能使用数据库的应用后重试，或手动删除数据库文件:\nC:\\Users\\[用户名]\\AppData\\Roaming\\MusicPlayerApp\\musicplayer.db", 
                                    "数据库错误", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                                    
                                    // 退出应用程序
                                    Current.Shutdown();
                                    return;
                            }
                        }
                        else
                        {
                            bool databaseExists = context.Database.CanConnect();
                            bool needMigration = false;

                            if (!databaseExists)
                            {
                                // 数据库不存在，创建新数据库
                                Logger.Info("数据库不存在，将创建新数据库");
                                bool created = context.Database.EnsureCreated();
                                Logger.Info($"数据库创建结果: {(created ? "创建成功" : "失败")}");
                                
                                // 初始化基础数据
                                Logger.Info("开始初始化基础数据...");
                                DbInitializer.Initialize(context);
                                Logger.Info("数据库初始化完成");
                            }
                            else
                            {
                                // 数据库已存在
                                Logger.Info("数据库已存在且结构正常");
                            }

                            // 验证用户表是否有数据
                            var userCount = context.Users.Count();
                            Logger.Info($"Users表中记录数: {userCount}");
                        }
                    }
                    catch (Exception dbEx)
                    {
                        Logger.Error(dbEx, $"数据库初始化失败: {dbEx.Message}");
                        MessageBox.Show($"数据库初始化失败: {dbEx.Message}\n\n内部错误: {dbEx.InnerException?.Message}",
                            "数据库错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        
                        // 尝试使用现有数据库
                        try
                        {
                            Logger.Info("尝试使用现有数据库...");
                            var userCount = context.Users.Count();
                            Logger.Info($"Users表中记录数: {userCount}，将继续使用现有数据库");
                        }
                        catch
                        {
                            // 如果连接到现有数据库也失败，则无法继续
                            return;
                        }
                    }
                }

                // 测试自动登录
                var userService = ServiceLocator.Instance.GetService<UserService>();
                var user = await userService.AutoLoginAsync();

                if (user != null)
                {
                    Logger.Info($"自动登录成功: {user.Username}");
                    // 应用用户主题设置
                    InitializeUserTheme(user);
                }
                else
                {
                    Logger.Info("自动登录失败，创建并使用默认用户");
                    
                    try
                    {
                        // 尝试注册默认用户
                        user = await userService.RegisterAsync("DefaultUser", "password", "default@example.com");
                        Logger.Info($"默认用户创建成功: {user.Username}");
                        // 应用默认主题
                        InitializeUserTheme(user);
                    }
                    catch (InvalidOperationException)
                    {
                        // 如果用户已存在，尝试登录
                        Logger.Info("默认用户已存在，尝试登录");
                        try
                        {
                            user = await userService.LoginAsync("DefaultUser", "password");
                            Logger.Info($"使用默认用户登录成功: {user.Username}");
                            // 应用用户主题设置
                            InitializeUserTheme(user);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "默认用户登录失败");
                        }
                    }
                }

                // 替代方案：直接加载主窗口
                Logger.Info("加载主窗口");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "应用程序启动时出错");
                MessageBox.Show($"应用程序启动时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 初始化主题
            InitializeTheme();

            Logger.Info("应用程序启动完成");
        }

        /// <summary>
        /// 初始化用户主题设置
        /// </summary>
        private void InitializeUserTheme(Models.User user)
        {
            if (user?.Settings != null)
            {
                try
                {
                    string theme = user.Settings.Theme;
                    Logger.Info($"应用用户主题设置: {theme}");

                    // 获取主题名称
                    string themeName = theme;
                    if (string.IsNullOrEmpty(themeName))
                        themeName = "Dark"; // 默认深色主题

                    // 如果是系统主题，则获取系统主题设置
                    if (themeName.Equals("System", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isDarkTheme = false;
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        {
                            if (key != null)
                            {
                                object value = key.GetValue("AppsUseLightTheme");
                                if (value != null && value is int intValue)
                                {
                                    isDarkTheme = (intValue == 0); // 0 表示深色主题，1 表示浅色主题
                                }
                            }
                        }
                        themeName = isDarkTheme ? "Dark" : "Light";
                    }

                    // 应用主题
                    var bundledTheme = Current.Resources.MergedDictionaries
                        .OfType<MaterialDesignThemes.Wpf.BundledTheme>()
                        .FirstOrDefault();
                        
                    if (bundledTheme != null)
                    {
                        bundledTheme.BaseTheme = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase) 
                            ? MaterialDesignThemes.Wpf.BaseTheme.Dark 
                            : MaterialDesignThemes.Wpf.BaseTheme.Light;
                        
                        Logger.Info($"已应用主题: {themeName}");
                    }
                    else
                    {
                        Logger.Warn("无法找到MaterialDesign主题资源，主题设置失败");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "应用用户主题设置失败");
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 记录应用程序退出
            Logger.Info("应用程序退出");
            LogManager.Shutdown();
            
            base.OnExit(e);
        }
        
        // 检查是否需要迁移数据库
        private bool CheckIfMigrationNeeded(MusicDbContext context)
        {
            try
            {
                // 检查是否缺少关键列
                bool hasTrackNumberColumn = false;
                bool hasLastPlayedDateColumn = false;
                
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();
                
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(Songs)";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var columnName = reader["name"].ToString();
                            if (columnName == "TrackNumber")
                                hasTrackNumberColumn = true;
                            else if (columnName == "LastPlayedDate")
                                hasLastPlayedDateColumn = true;
                        }
                    }
                }
                
                Logger.Info($"列检查结果: TrackNumber={hasTrackNumberColumn}, LastPlayedDate={hasLastPlayedDateColumn}");
                
                // 如果缺少任一列，则需要迁移
                return !hasTrackNumberColumn || !hasLastPlayedDateColumn;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "检查数据库迁移需求失败");
                return false; // 出错时保守处理，不强制迁移
            }
        }

        // 更新数据库结构
        private void UpdateDatabaseSchema(MusicDbContext context)
        {
            try
            {
                Logger.Info("开始检查并更新数据库结构...");

                // 检查Songs表是否包含TrackNumber列
                bool hasTrackNumberColumn = false;
                bool hasLastPlayedDateColumn = false;
                
                try
                {
                    // 尝试执行一个使用TrackNumber的查询，如果列不存在会抛出异常
                    var tempSong = context.Songs.FirstOrDefault();
                    if (tempSong != null)
                    {
                        // 只是为了编译通过，实际上这个检查通过其他方式实现
                        var _ = tempSong.TrackNumber;
                        hasTrackNumberColumn = true;
                        Logger.Info("TrackNumber列已存在");
                    }
                }
                catch (Exception ex)
                {
                    hasTrackNumberColumn = false;
                    Logger.Info($"TrackNumber列不存在或检查出错: {ex.Message}");
                }

                // 检查Songs表是否包含LastPlayedDate列
                try
                {
                    // 直接执行SQL查询来检查列是否存在
                    var sql = "PRAGMA table_info(Songs)";
                    // 注意: ExecuteSqlCommand已被弃用，使用更安全的方式检查列是否存在
                    
                    // 使用更安全的方式检查列是否存在
                    var connection = context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                        connection.Open();
                    
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var columnName = reader["name"].ToString();
                                if (columnName == "TrackNumber")
                                    hasTrackNumberColumn = true;
                                else if (columnName == "LastPlayedDate")
                                    hasLastPlayedDateColumn = true;
                            }
                        }
                    }
                    
                    Logger.Info($"列检查结果: TrackNumber={hasTrackNumberColumn}, LastPlayedDate={hasLastPlayedDateColumn}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "检查数据库列失败");
                }

                // 如果没有TrackNumber列，添加它
                if (!hasTrackNumberColumn)
                {
                    Logger.Info("开始添加TrackNumber列");
                    
                    try
                    {
                        // 使用原始SQL命令添加列
                        var connection = context.Database.GetDbConnection();
                        if (connection.State != System.Data.ConnectionState.Open)
                            connection.Open();
                        
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "ALTER TABLE Songs ADD COLUMN TrackNumber INTEGER DEFAULT 0";
                            command.ExecuteNonQuery();
                        }
                        
                        Logger.Info("成功添加TrackNumber列");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "添加TrackNumber列失败");
                        // 不要抛出异常，继续执行后续逻辑
                    }
                }

                // 如果没有LastPlayedDate列，添加它
                if (!hasLastPlayedDateColumn)
                {
                    Logger.Info("开始添加LastPlayedDate列");
                    
                    try
                    {
                        // 使用原始SQL命令添加列
                        var connection = context.Database.GetDbConnection();
                        if (connection.State != System.Data.ConnectionState.Open)
                            connection.Open();
                        
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "ALTER TABLE Songs ADD COLUMN LastPlayedDate TEXT NULL";
                            command.ExecuteNonQuery();
                        }
                        
                        Logger.Info("成功添加LastPlayedDate列");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "添加LastPlayedDate列失败");
                        // 不要抛出异常，继续执行后续逻辑
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "更新数据库结构失败");
            }
        }

        // 检查Windows系统是否设置为深色模式
        private bool IsWindowsSystemDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value != null && value is int intValue)
                        {
                            return (intValue == 0); // 0表示深色主题，1表示浅色主题
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "检查Windows系统主题设置失败");
            }
            return false; // 默认返回非深色主题
        }

        // 更新主题配置
        public void ApplyTheme(string themeName)
        {
            try
            {
                var bundledTheme = Current.Resources.MergedDictionaries
                    .OfType<MaterialDesignThemes.Wpf.BundledTheme>()
                    .FirstOrDefault();

                if (bundledTheme != null)
                {
                    switch (themeName?.ToLower())
                    {
                        case "light":
                            bundledTheme.BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Light;
                            UpdateResourcesForTheme(false); // 更新为浅色主题资源
                            break;
                        case "system":
                            var isDarkMode = SystemParameters.HighContrast || 
                                (Environment.OSVersion.Version.Major >= 10 && IsWindowsSystemDarkMode());
                            bundledTheme.BaseTheme = isDarkMode ? 
                                MaterialDesignThemes.Wpf.BaseTheme.Dark : 
                                MaterialDesignThemes.Wpf.BaseTheme.Light;
                            UpdateResourcesForTheme(isDarkMode);
                            break;
                        default: // Dark
                            bundledTheme.BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Dark;
                            UpdateResourcesForTheme(true); // 更新为深色主题资源
                            break;
                    }
                }

                Logger.Info($"主题已更改为: {themeName}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "应用主题失败");
            }
        }

        // 更新应用资源以匹配当前主题
        private void UpdateResourcesForTheme(bool isDarkTheme)
        {
            ResourceDictionary resources = Current.Resources;

            // 定义主题色
            if (isDarkTheme)
            {
                // 应用深色主题资源
                resources["TopBarBackground"] = resources["BackgroundBrush"];
                resources["SideNavBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                resources["ContentBackground"] = resources["BackgroundBrush"];
                resources["PlayBarBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D"));
                resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                resources["HoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"));
                
                // 更新DataGrid相关资源
                resources["DataGridRowBackground"] = new SolidColorBrush(Colors.Transparent);
                resources["DataGridRowForeground"] = new SolidColorBrush(Colors.White);
                resources["DataGridHeaderBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                resources["DataGridCellBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                
                // 确保文本颜色在深色主题中可见
                resources["MaterialDesignBodyLight"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
            }
            else
            {
                // 应用浅色主题资源
                resources["TopBarBackground"] = resources["LightTopBarBackground"];
                resources["SideNavBackground"] = resources["LightSideNavBackground"];
                resources["ContentBackground"] = resources["LightContentBackground"];
                resources["PlayBarBackground"] = resources["LightPlayBarBackground"];
                resources["BorderBrush"] = resources["LightBorderBrush"];
                resources["HoverBrush"] = resources["LightHoverBrush"];
                
                // 更新DataGrid相关资源
                resources["DataGridRowBackground"] = new SolidColorBrush(Colors.Transparent);
                resources["DataGridRowForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212121"));
                resources["DataGridHeaderBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
                resources["DataGridCellBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDBDBD"));
                
                // 确保文本颜色在浅色主题中可见
                resources["MaterialDesignBodyLight"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"));
                
                // 确保DataGrid文本在浅色主题中可见
                resources["DataGridTextForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212121"));
            }

            Logger.Info($"已更新应用资源为{(isDarkTheme ? "深色" : "浅色")}主题");
        }

        private void InitializeTheme()
        {
            try
            {
                // 获取当前用户设置的主题
                string userTheme = "dark"; // 默认为深色
                try
                {
                    var userService = ServiceLocator.Instance.GetService<UserService>();
                    if (userService.IsLoggedIn && userService.CurrentUser?.Settings?.Theme != null)
                    {
                        userTheme = userService.CurrentUser.Settings.Theme.ToLower();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "获取用户主题设置失败，使用默认深色主题");
                }

                // 应用主题
                ApplyTheme(userTheme);

                Logger.Info($"应用主题初始化完成: {userTheme}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "初始化主题失败");
            }
        }

        // 临时方法：重置数据库
        private void ResetDatabase()
        {
            try
            {
                // 获取数据库路径
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicPlayerApp");
                string dbPath = Path.Combine(appDataPath, "musicplayer.db");
                
                if (File.Exists(dbPath))
                {
                    // 备份数据库
                    string backupPath = Path.Combine(appDataPath, $"musicplayer_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                    File.Copy(dbPath, backupPath);
                    
                    // 删除现有数据库
                    File.Delete(dbPath);
                    
                    Logger.Info($"已重置数据库，原数据库已备份到: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "重置数据库失败");
            }
        }
    }
}