using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using MusicPlayerApp.Models;
using MusicPlayerApp.Audio;
using System.IO;
using System.Diagnostics;
// 添加自定义别名，避免冲突
using AppPlaybackState = MusicPlayerApp.Models.PlaybackState;
// 添加类型别名解决命名冲突
using AudioFadeInOutSampleProvider = MusicPlayerApp.Audio.FadeInOutSampleProvider;

namespace MusicPlayerApp.Services
{
    // 定义事件参数
    public class SongChangedEventArgs : EventArgs
    {
        public Song Song { get; set; }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public AppPlaybackState State { get; set; }
    }

    public class PlaybackPositionChangedEventArgs : EventArgs
    {
        public TimeSpan Position { get; set; }
    }

    // 实现媒体播放器服务，采用单例模式
    public class MediaPlayerService
    {
        private static readonly Lazy<MediaPlayerService> _instance =
            new Lazy<MediaPlayerService>(() => new MediaPlayerService());

        public static MediaPlayerService Instance => _instance.Value;

        // 音频播放器
        private IWavePlayer _waveOutDevice;
        private AudioFileReader _audioFileReader;
        private AudioFadeInOutSampleProvider _fadeInOutProvider;
        private SemaphoreSlim _playerSemaphore = new SemaphoreSlim(1, 1);

        // 预加载系统
        private AudioFileReader _nextAudioFileReader;
        private string _nextFilePath;
        private Task _preloadTask;
        private bool _isPreloading;

        // 淡入淡出设置
        private bool _enableCrossFade;
        private int _crossFadeDuration;

        // 状态
        private Song _currentSong;
        private AppPlaybackState _state;
        private int _volume;
        private bool _isMuted;
        private bool _isShuffled;
        private RepeatMode _repeatMode;

        // 多线程相关
        private CancellationTokenSource _cancellationTokenSource;
        private Task _positionUpdateTask;

        // 事件
        public event EventHandler<SongChangedEventArgs> SongChanged;
        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;

        // 属性
        public Song CurrentSong => _currentSong;
        public AppPlaybackState State => _state;
        public int Volume
        {
            get => _isMuted ? 0 : _volume;
            set
            {
                _volume = Math.Max(0, Math.Min(100, value));
                if (_waveOutDevice != null)
                {
                    // NAudio音量范围是0.0到1.0
                    _waveOutDevice.Volume = _volume / 100f;
                }
            }
        }
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_waveOutDevice != null)
                {
                    _waveOutDevice.Volume = _isMuted ? 0 : _volume / 100f;
                }
            }
        }
        public bool IsShuffled { get => _isShuffled; set => _isShuffled = value; }
        public RepeatMode RepeatMode { get => _repeatMode; set => _repeatMode = value; }

        // 淡入淡出属性
        public bool EnableCrossFade
        {
            get => _enableCrossFade;
            set => _enableCrossFade = value;
        }

        public int CrossFadeDuration
        {
            get => _crossFadeDuration;
            set => _crossFadeDuration = Math.Max(1, Math.Min(5, value));
        }

        // 播放位置相关属性
        public TimeSpan CurrentPosition => _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

        // 添加播放列表字段
        private List<Song> _playlist = new List<Song>();
        private int _currentIndex = -1;

        // 在属性部分添加
        public List<Song> Playlist => _playlist;
        public int CurrentIndex => _currentIndex;

        // 私有构造函数，实现单例模式
        private MediaPlayerService()
        {
            _state = AppPlaybackState.Stopped;
            _volume = 50;
            _repeatMode = RepeatMode.None;
            _enableCrossFade = true;
            _crossFadeDuration = 2;
            _isPreloading = false;
            
            // 初始化播放器
            InitializePlayer();
            
            // 启动单独的线程来预热和优化音频引擎
            Task.Run(() => OptimizeAudioEngine());
        }

        private void InitializePlayer()
        {
            try
            {
                // 切换回WaveOutEvent，因为DirectSoundOut不支持设置音量
                _waveOutDevice = new WaveOutEvent()
                {
                    DesiredLatency = 60,      // 进一步降低延迟
                    NumberOfBuffers = 3       // 增加到3个缓冲区提高稳定性
                };
                _waveOutDevice.PlaybackStopped += OnPlaybackStopped;
                
                // 预先初始化一个播放器，减少首次播放的延迟
                _ = Task.Run(() => {
                    try {
                        // 创建一个静音的极短音频并播放，预热音频系统
                        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                        // 增加预热数据大小
                        byte[] silenceData = new byte[waveFormat.AverageBytesPerSecond / 2]; // 500ms的静音
                        var silenceStream = new MemoryStream(silenceData);
                        var waveProvider = new RawSourceWaveStream(silenceStream, waveFormat);
                        
                        // 使用与主播放器相同的设置进行预热
                        var tempPlayer = new WaveOutEvent()
                        {
                            DesiredLatency = 60,
                            NumberOfBuffers = 3
                        };
                        tempPlayer.Init(waveProvider);
                        
                        // 设置线程优先级以确保流畅预热
                        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                        
                        tempPlayer.Play();
                        Thread.Sleep(50); // 延长预热时间
                        tempPlayer.Stop();
                        tempPlayer.Dispose();
                        
                        App.Logger.Info("音频系统预热完成");
                    }
                    catch (Exception ex) {
                        App.Logger.Error(ex, "音频系统预热失败");
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "初始化音频播放器失败");
            }
        }

        // 预加载下一首歌曲
        private void PreloadNextSong(Song nextSong)
        {
            if (nextSong == null || _isPreloading || !File.Exists(nextSong.FilePath)) 
                return;
                
            _isPreloading = true;
            _nextFilePath = nextSong.FilePath;
            
            _preloadTask = Task.Run(() => {
                try
                {
                    if (_nextAudioFileReader != null)
                    {
                        _nextAudioFileReader.Dispose();
                        _nextAudioFileReader = null;
                    }
                    
                    // 创建但不初始化音频读取器
                    _nextAudioFileReader = new AudioFileReader(_nextFilePath);
            
                    // 预热：预读取文件的前面部分
                    float[] preBuffer = new float[_nextAudioFileReader.WaveFormat.SampleRate * _nextAudioFileReader.WaveFormat.Channels / 2]; // 0.5秒数据
                    _nextAudioFileReader.Read(preBuffer, 0, preBuffer.Length);
                    // 重置位置
                    _nextAudioFileReader.Position = 0;
                    
                    App.Logger.Info($"预加载完成: {Path.GetFileName(_nextFilePath)}");
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"预加载失败: {Path.GetFileName(_nextFilePath)}");
                    if (_nextAudioFileReader != null)
                    {
                        _nextAudioFileReader.Dispose();
                        _nextAudioFileReader = null;
                    }
                }
                finally
                {
                    _isPreloading = false;
                }
            });
        }

        // 优化的音频播放引擎初始化
        private void OptimizeAudioEngine()
        {
            try
            {
                // 设置音频处理线程优先级
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                
                // 预热音频引擎，减少首次播放延迟
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                byte[] silenceData = new byte[waveFormat.AverageBytesPerSecond];
                var silenceStream = new MemoryStream(silenceData);
                var waveProvider = new RawSourceWaveStream(silenceStream, waveFormat);
                
                using (var tempPlayer = new WaveOutEvent())
                {
                    tempPlayer.Init(waveProvider);
                    tempPlayer.Play();
                    Thread.Sleep(20);
                    tempPlayer.Stop();
                }
                
                // 重置线程优先级
                Thread.CurrentThread.Priority = ThreadPriority.Normal;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "音频引擎优化失败");
                // 恢复线程优先级
                Thread.CurrentThread.Priority = ThreadPriority.Normal;
            }
        }

        // 播放指定歌曲
        public async Task PlayAsync(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(nameof(song), "无法播放空曲目");

            try
            {
                // 使用信号量防止多个播放请求同时执行
                await _playerSemaphore.WaitAsync();
                
                try
                {
                    // 如果当前歌曲与请求播放的歌曲相同，且为暂停状态，则恢复播放
                    if (_currentSong != null && _currentSong.Id == song.Id && _state == AppPlaybackState.Paused)
                    {
                        Resume();
                        return;
                    }
                    
                    // 完全停止当前播放
                    await StopCoreAsync();

                    _currentSong = song;
                    
                    try 
                    {
                        // 设置较高的线程优先级以减少初始化时的卡顿
                        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                        
                        // 简单直接地从文件读取
                        _audioFileReader = new AudioFileReader(song.FilePath);
                        
                        // 优化：预先缓冲一些数据到内存
                        // 读取前1秒的数据到缓冲区
                        float[] preBuffer = new float[_audioFileReader.WaveFormat.SampleRate * _audioFileReader.WaveFormat.Channels];
                        _audioFileReader.Read(preBuffer, 0, preBuffer.Length);
                        // 重置回起始位置
                        _audioFileReader.Position = 0;
                        
                        // 确保音频播放器已初始化
                        if (_waveOutDevice == null)
                        {
                            InitializePlayer();
                            if (_waveOutDevice == null)
                            {
                                throw new InvalidOperationException("无法初始化音频播放器");
                            }
                        }
                        
                        // 直接初始化播放器
                        _waveOutDevice.Init(_audioFileReader);
                        _waveOutDevice.Volume = _isMuted ? 0 : _volume / 100f;
                        
                        // 重置线程优先级
                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        
                        // 先设置状态，然后开始播放
                        _state = AppPlaybackState.Playing;
                        _waveOutDevice.Play();
                        
                        // 启动位置更新任务
                        StartPositionUpdateTask();
                        
                        // 触发事件
                        RaiseEvent(() => SongChanged?.Invoke(this, new SongChangedEventArgs { Song = song }));
                        RaiseEvent(() => PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { State = _state }));
                        
                        // 更新播放计数
                        song.PlayCount++;
                    }
                    catch (Exception ex)
                    {
                        // 恢复线程优先级
                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        
                        App.Logger.Error(ex, $"初始化音频播放失败: {ex.Message}");
                        await StopCoreAsync();
                        throw;
                    }
                }
                finally
                {
                    _playerSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"播放歌曲失败: {song.Title}");
                throw new Exception($"播放歌曲失败: {ex.Message}", ex);
            }
        }

        // 暂停播放
        public void Pause()
        {
            if (_state == AppPlaybackState.Playing && _waveOutDevice != null)
            {
                _waveOutDevice.Pause();
                _state = AppPlaybackState.Paused;

                RaiseEvent(() => PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { State = _state }));
            }
        }

        // 恢复播放
        public void Resume()
        {
            if (_state == AppPlaybackState.Paused && _waveOutDevice != null)
            {
                _waveOutDevice.Play();
                _state = AppPlaybackState.Playing;

                RaiseEvent(() => PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { State = _state }));
            }
        }

        // 核心停止方法 - 内部使用
        private async Task StopCoreAsync()
        {
            try
            {
                // 取消位置更新任务
                _cancellationTokenSource?.Cancel();
                if (_positionUpdateTask != null)
                {
                    try
                    {
                        await Task.WhenAny(_positionUpdateTask, Task.Delay(100));
                    }
                    catch { /* 忽略任何异常 */ }
                    
                    _positionUpdateTask = null;
                }

                // 先设置状态为停止
                _state = AppPlaybackState.Stopped;

                if (_waveOutDevice != null)
                {
                    try
                    {
                        // 移除事件处理器，防止停止时触发额外事件
                        _waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
                        
                        // 停止播放器
                        if (_waveOutDevice.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
                        {
                            _waveOutDevice.Stop();
                        }
                        
                        // 释放资源
                        _waveOutDevice.Dispose();
                        
                        // 重新创建播放设备
                        _waveOutDevice = new WaveOutEvent()
                        {
                            DesiredLatency = 80,    // 低延迟但足够稳定
                            NumberOfBuffers = 2     // 较少的缓冲区数量
                        };
                        _waveOutDevice.PlaybackStopped += OnPlaybackStopped;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, "重置音频播放设备失败");
                    }
                }

                // 释放音频文件读取器
                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }
                
                // 清理淡入淡出提供器
                _fadeInOutProvider = null;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "停止播放失败");
            }
        }

        // 公开的停止方法 - 外部调用
        public async Task StopAsync()
        {
            await _playerSemaphore.WaitAsync();
            try
            {
                await StopCoreAsync();
                RaiseEvent(() => PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { State = _state }));
            }
            finally
            {
                _playerSemaphore.Release();
            }
        }

        // 跳转到指定位置
        public void SetPosition(TimeSpan position)
        {
            if (_audioFileReader != null)
            {
                try
                {
                    // 防止位置超出范围
                    position = TimeSpan.FromMilliseconds(
                        Math.Min(position.TotalMilliseconds, _audioFileReader.TotalTime.TotalMilliseconds - 100));
                    position = TimeSpan.FromMilliseconds(
                        Math.Max(position.TotalMilliseconds, 0));
                    
                    // 暂时暂停播放
                    bool wasPlaying = _state == AppPlaybackState.Playing;
                    if (wasPlaying)
                    {
                        _waveOutDevice?.Pause();
                    }
                    
                    try
                    {
                        // 直接设置当前时间，而不是通过计算字节位置
                        _audioFileReader.CurrentTime = position;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error(ex, "设置音频位置失败");
                    }
                    
                    // 如果之前在播放，恢复播放
                    if (wasPlaying)
                    {
                        _waveOutDevice?.Play();
                    }
                    
                    // 更新UI
                    RaiseEvent(() => PlaybackPositionChanged?.Invoke(this, new PlaybackPositionChangedEventArgs { Position = position }));
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"设置播放位置失败: {ex.Message}");
                    // 发生异常时尝试恢复播放
                    if (_state == AppPlaybackState.Playing)
                    {
                        _waveOutDevice?.Play();
                    }
                }
            }
        }

        // 释放资源
        public async Task DisposeAsync()
        {
            await StopAsync();

            _waveOutDevice?.Dispose();
            _waveOutDevice = null;
            
            if (_nextAudioFileReader != null)
            {
                _nextAudioFileReader.Dispose();
                _nextAudioFileReader = null;
            }
        }

        // 启动位置更新任务
        private void StartPositionUpdateTask()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _positionUpdateTask = Task.Run(async () =>
            {
                try
                {
                    // 使用简单的固定间隔更新机制
                    while (!token.IsCancellationRequested && _audioFileReader != null)
                    {
                        try 
                        {
                            // 仅当播放中才更新位置
                            if (_state == AppPlaybackState.Playing)
                            {
                                var position = _audioFileReader.CurrentTime;
                                
                                // 更新UI
                                RaiseEvent(() => PlaybackPositionChanged?.Invoke(this, 
                                    new PlaybackPositionChangedEventArgs { Position = position }));
                            }
                        }
                        catch (Exception ex) 
                        { 
                            App.Logger.Error(ex, "读取播放位置时出错");
                        }

                        // 使用固定的间隔，不变更更新频率
                        await Task.Delay(1000, token);
                    }
                }
                catch (TaskCanceledException) { /* 任务取消，正常情况 */ }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, "位置更新任务异常");
                }
            });
        }

        // 播放停止事件处理
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // 检查并处理异常
            if (e.Exception != null)
            {
                App.Logger.Error(e.Exception, "播放停止异常");
            }

            // 避免重复处理
            if (_state == AppPlaybackState.Stopped) 
            {
                return; // 已经停止，不需要处理
            }

            try
            {
                // 判断是否是自然播放结束
                bool playbackFinished = false;
                
                if (_audioFileReader != null)
                {
                    // 计算当前位置与总时长的差距
                    double remainingTime = _audioFileReader.TotalTime.TotalSeconds - _audioFileReader.CurrentTime.TotalSeconds;
                    playbackFinished = remainingTime < 0.5; // 如果剩余时间小于0.5秒，认为播放结束
                    
                    App.Logger.Info($"播放停止事件触发: 剩余时间={remainingTime}秒, 判断为{(playbackFinished ? "播放结束" : "手动停止")}");
                }
                
                // 只在自然播放结束时处理后续曲目
                if (playbackFinished)
                {
                    // 使用UI线程处理后续播放操作
                    Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            // 先设置状态为停止，避免多次触发
                            _state = AppPlaybackState.Stopped;
                            
                            // 根据重复模式决定下一步操作
                            switch (_repeatMode)
                            {
                                case RepeatMode.One:
                                    // 单曲循环 - 重新播放当前歌曲
                                    if (_currentSong != null)
                                    {
                                        await Task.Delay(200); // 短暂停顿
                                        await PlayAsync(_currentSong);
                                    }
                                    break;
                                    
                                case RepeatMode.All:
                                    // 列表循环 - 播放下一首，如果是最后一首则回到第一首
                                    await Task.Delay(200); // 短暂停顿
                                    await PlayNextAsync();
                                    break;
                                    
                                default:
                                    // 不循环 - 尝试播放下一曲，没有则停止
                                    await Task.Delay(200); // 短暂停顿
                                    if (!await PlayNextAsync())
                                    {
                                        // 已经是最后一首，确保完全停止
                                        await StopAsync();
                                    }
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error(ex, "处理播放停止事件时出错");
                            await StopAsync(); // 发生错误时确保停止播放
                        }
                    }));
                }
                else
                {
                    // 非自然结束（可能是手动停止或跳转），不进行后续处理
                    App.Logger.Info("播放被手动停止或中断，不进行自动播放下一曲");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "处理播放停止事件时出错");
            }
        }

        // 辅助方法：安全地触发事件
        private void RaiseEvent(Action eventRaiser)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(eventRaiser);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "触发事件失败");
            }
        }

        // 在MediaPlayerService类中添加设置播放列表方法
        public void SetPlaylist(List<Song> songs, int startIndex = 0)
        {
            if (songs == null || songs.Count == 0)
                return;

            _playlist = new List<Song>(songs);
            _currentIndex = Math.Min(Math.Max(0, startIndex), songs.Count - 1);
            
            // 预加载第一首
            if (_currentIndex < songs.Count)
            {
                PreloadNextSong(songs[_currentIndex]);
            }
        }

        // 添加播放下一曲方法
        public async Task<bool> PlayNextAsync()
        {
            if (_playlist == null || _playlist.Count == 0)
                return false;

            // 防止正在播放时调用，确保先停止
            if (_state == AppPlaybackState.Playing || _state == AppPlaybackState.Paused)
            {
                await StopCoreAsync();
            }

            // 处理随机播放
            if (_isShuffled)
            {
                int nextIndex;
                if (_playlist.Count <= 1)
                {
                    nextIndex = _currentIndex;
                }
                else
                {
                    // 确保不会连续播放同一首歌
                    var random = new Random();
                    do
                    {
                        nextIndex = random.Next(0, _playlist.Count);
                    } while (nextIndex == _currentIndex && _playlist.Count > 1);
                }
                _currentIndex = nextIndex;
            }
            else
            {
                // 常规播放，移动到下一曲
                _currentIndex++;
                
                // 如果到达列表末尾
                if (_currentIndex >= _playlist.Count)
                {
                    // 如果启用了列表循环，回到开头
                    if (_repeatMode == RepeatMode.All)
                    {
                        _currentIndex = 0;
                    }
                    else
                    {
                        // 否则停在最后一首
                        _currentIndex = _playlist.Count - 1;
                        return false;
                    }
                }
            }

            try
            {
                App.Logger.Info($"播放下一曲: 当前索引={_currentIndex}, 歌曲={(_playlist[_currentIndex]?.Title ?? "未知")}");
                
                // 播放当前索引的歌曲
                await PlayAsync(_playlist[_currentIndex]);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放下一曲失败");
                return false;
            }
        }

        // 添加播放上一曲方法
        public async Task<bool> PlayPreviousAsync()
        {
            if (_playlist == null || _playlist.Count == 0)
                return false;

            // 防止正在播放时调用，确保先停止
            if (_state == AppPlaybackState.Playing || _state == AppPlaybackState.Paused)
            {
                await StopCoreAsync();
            }

            // 减少索引
            _currentIndex--;
            
            // 如果已经到达列表开头
            if (_currentIndex < 0)
            {
                // 如果启用了列表循环，跳到末尾
                if (_repeatMode == RepeatMode.All)
                {
                    _currentIndex = _playlist.Count - 1;
                }
                else
                {
                    // 否则停在第一首
                    _currentIndex = 0;
                    return false;
                }
            }

            try
            {
                App.Logger.Info($"播放上一曲: 当前索引={_currentIndex}, 歌曲={(_playlist[_currentIndex]?.Title ?? "未知")}");
                
                // 播放当前索引的歌曲
                await PlayAsync(_playlist[_currentIndex]);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "播放上一曲失败");
                return false;
            }
        }
        
        // 播放单首歌曲
        public async Task PlaySong(Song song)
        {
            if (song == null)
                return;
                
            // 设置播放列表只包含这一首歌
            SetPlaylist(new List<Song> { song });
            
            // 异步播放
            await PlayAsync(song);
        }
        
        // 播放歌曲列表
        public async Task PlaySongList(IEnumerable<Song> songs, bool shuffle = false)
        {
            if (songs == null)
                return;

            var songList = new List<Song>(songs);
            if (songList.Count == 0)
                return;

            // 设置随机播放模式
            IsShuffled = shuffle;
            
            // 设置播放列表
            SetPlaylist(songList);
            
            // 获取开始索引
            int startIndex = 0;
            if (shuffle && songList.Count > 1)
            {
                // 如果是随机播放，随机选择一首开始
                startIndex = new Random().Next(songList.Count);
            }
            
            // 异步播放第一首
            await PlayAsync(songList[startIndex]);
        }

        // 设置音量
        public void SetVolume(int volume)
        {
            Volume = volume;
        }
    }
} 