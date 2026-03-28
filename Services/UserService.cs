using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MusicPlayerApp.Data;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services
{
    public class UserService
    {
        private readonly Func<MusicDbContext> _contextFactory;
        private User _currentUser;

        public User CurrentUser => _currentUser;
        
        // 添加判断用户是否已登录的属性
        public bool IsLoggedIn => _currentUser != null;

        public UserService(Func<MusicDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // 登录
        public async Task<User> LoginAsync(string username, string password)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 注意：实际应用中应该使用密码哈希而不是明文
                    var user = await context.Users
                        .Include(u => u.Settings)
                        .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                    if (user == null)
                        throw new UnauthorizedAccessException("用户名或密码不正确");

                    _currentUser = user;
                    return user;
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "登录失败");
                throw new Exception($"登录失败: {ex.Message}", ex);
            }
        }

        // 注册
        public async Task<User> RegisterAsync(string username, string password, string email)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    // 检查用户名是否已存在
                    var existingUser = await context.Users
                        .FirstOrDefaultAsync(u => u.Username == username);

                    if (existingUser != null)
                        throw new InvalidOperationException("用户名已被使用");

                    // 创建新用户
                    var user = new User
                    {
                        Username = username,
                        Password = password, // 实际应用中应该哈希
                        Email = email,
                        CreatedDate = DateTime.Now
                    };

                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    // 创建用户设置
                    var settings = new UserSettings
                    {
                        UserId = user.Id,
                        Theme = "Dark",
                        Volume = 50,
                        Shuffle = false,
                        RepeatMode = RepeatMode.None,
                        EqualizerSettings = "{}"
                    };

                    context.UserSettings.Add(settings);
                    await context.SaveChangesAsync();

                    // 创建默认播放列表
                    var playlist = new Playlist
                    {
                        Title = "我喜欢的音乐",
                        UserId = user.Id,
                        Description = "我最喜欢的歌曲集合",
                        AddedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };

                    context.Playlists.Add(playlist);
                    await context.SaveChangesAsync();

                    user.Settings = settings;
                    _currentUser = user;

                    return user;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "注册用户失败");
                throw new Exception($"注册用户失败: {ex.Message}", ex);
            }
        }

        // 退出登录
        public void Logout()
        {
            _currentUser = null;
        }

        // 更新用户设置
        public async Task SaveUserSettingsAsync(UserSettings settings)
        {
            if (_currentUser == null)
                throw new InvalidOperationException("没有登录用户");

            try
            {
                using (var context = _contextFactory())
                {
                    var dbSettings = await context.UserSettings
                        .FirstOrDefaultAsync(us => us.UserId == _currentUser.Id);

                    if (dbSettings == null)
                    {
                        dbSettings = new UserSettings
                        {
                            UserId = _currentUser.Id
                        };
                        context.UserSettings.Add(dbSettings);
                    }

                    // 先更新基本设置
                    dbSettings.Theme = settings.Theme;
                    dbSettings.Volume = settings.Volume;
                    dbSettings.Shuffle = settings.Shuffle;
                    dbSettings.RepeatMode = settings.RepeatMode;
                    dbSettings.EqualizerSettings = settings.EqualizerSettings;
                    
                    // 尝试更新新增的设置属性
                    try
                    {
                        var sourceProps = settings.GetType().GetProperties();
                        var targetProps = dbSettings.GetType().GetProperties();
                        
                        foreach (var sourceProp in sourceProps)
                        {
                            // 跳过基本字段
                            if (sourceProp.Name == "Theme" || 
                                sourceProp.Name == "Volume" || 
                                sourceProp.Name == "Shuffle" || 
                                sourceProp.Name == "RepeatMode" || 
                                sourceProp.Name == "EqualizerSettings" ||
                                sourceProp.Name == "UserId" ||
                                sourceProp.Name == "User")
                                continue;
                            
                            // 查找目标属性
                            var targetProp = targetProps.FirstOrDefault(p => p.Name == sourceProp.Name);
                            if (targetProp != null && targetProp.CanWrite)
                            {
                                // 复制属性值
                                try
                                {
                                    object value = sourceProp.GetValue(settings);
                                    targetProp.SetValue(dbSettings, value);
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.Warn(ex, $"复制属性 {sourceProp.Name} 失败");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Warn(ex, "更新扩展设置属性失败");
                    }

                    await context.SaveChangesAsync();

                    // 更新当前用户的设置
                    _currentUser.Settings = dbSettings;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "保存用户设置失败");
                throw new Exception($"保存用户设置失败: {ex.Message}", ex);
            }
        }

        // 使用用户ID获取用户
        public async Task<User> GetUserByIdAsync(int userId)
        {
            try
            {
                using (var context = _contextFactory())
                {
                    return await context.Users
                        .Include(u => u.Settings)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"获取用户失败: ID={userId}");
                throw new Exception($"获取用户失败: {ex.Message}", ex);
            }
        }

        // 尝试使用已保存的凭据自动登录
        public async Task<User> AutoLoginAsync()
        {
            try
            {
                // 实际应用中，这里可以从本地存储或加密文件中读取保存的凭据
                // 为了简单起见，我们总是使用默认用户
                using (var context = _contextFactory())
                {
                    var defaultUser = await context.Users
                        .Include(u => u.Settings)
                        .FirstOrDefaultAsync(u => u.Username == "DefaultUser");

                    if (defaultUser != null)
                    {
                        _currentUser = defaultUser;
                        return defaultUser;
                    }
                    
                    // 如果没有默认用户，则创建一个
                    return await RegisterAsync("DefaultUser", "password", "default@example.com");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "自动登录失败");
                // 不抛出异常，而是返回null，表示自动登录失败
                return null;
            }
        }
        
        // 更新用户资料信息
        public async Task<User> UpdateUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
                
            try
            {
                using (var context = _contextFactory())
                {
                    // 获取数据库中现有的用户
                    var dbUser = await context.Users
                        .Include(u => u.Settings)
                        .FirstOrDefaultAsync(u => u.Id == user.Id);
                        
                    if (dbUser == null && _currentUser != null)
                    {
                        // 尝试通过当前用户ID查询
                        dbUser = await context.Users
                            .Include(u => u.Settings)
                            .FirstOrDefaultAsync(u => u.Id == _currentUser.Id);
                    }
                    
                    if (dbUser == null)
                    {
                        // 如果用户不存在，添加新用户
                        context.Users.Add(user);
                    }
                    else
                    {
                        // 更新用户信息
                        dbUser.Username = user.Username;
                        dbUser.Email = user.Email;
                        dbUser.Bio = user.Bio;
                        dbUser.AvatarColor = user.AvatarColor;
                        dbUser.AvatarChar = user.AvatarChar;
                        dbUser.AvatarImagePath = user.AvatarImagePath;
                        dbUser.UpdatedDate = DateTime.Now;
                        
                        // 如果有设置其他必要的字段，也应在此更新
                    }
                    
                    await context.SaveChangesAsync();
                    
                    // 更新当前用户引用
                    if (_currentUser != null && dbUser != null && _currentUser.Id == dbUser.Id)
                    {
                        _currentUser = dbUser;
                    }
                    
                    return dbUser ?? user;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "更新用户信息失败");
                throw new Exception($"更新用户信息失败: {ex.Message}", ex);
            }
        }
    }
}