using System;
using System.Collections.Generic;
using System.Windows.Input;
using Newtonsoft.Json;
using MusicPlayerApp.Services;
using MusicPlayerApp.Commands;

namespace MusicPlayerApp.ViewModels
{
    public class EqualizerViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private readonly MediaPlayerService _playerService;
        
        // 均衡器设置
        private int _band32Hz;
        private int _band64Hz;
        private int _band125Hz;
        private int _band250Hz;
        private int _band500Hz;
        private int _band1KHz;
        private int _band2KHz;
        private int _band4KHz;
        private int _band8KHz;
        private int _band16KHz;
        private int _bassBoost;
        private int _spatialEffect;
        private string _selectedPreset;
        
        // 命令
        public ICommand ApplyEqualizerCommand { get; }
        public ICommand ResetEqualizerCommand { get; }
        public ICommand SelectPresetCommand { get; }
        
        // 属性
        public int Band32Hz
        {
            get => _band32Hz;
            set { _band32Hz = value; OnPropertyChanged(nameof(Band32Hz)); }
        }
        
        public int Band64Hz
        {
            get => _band64Hz;
            set { _band64Hz = value; OnPropertyChanged(nameof(Band64Hz)); }
        }
        
        public int Band125Hz
        {
            get => _band125Hz;
            set { _band125Hz = value; OnPropertyChanged(nameof(Band125Hz)); }
        }
        
        public int Band250Hz
        {
            get => _band250Hz;
            set { _band250Hz = value; OnPropertyChanged(nameof(Band250Hz)); }
        }
        
        public int Band500Hz
        {
            get => _band500Hz;
            set { _band500Hz = value; OnPropertyChanged(nameof(Band500Hz)); }
        }
        
        public int Band1KHz
        {
            get => _band1KHz;
            set { _band1KHz = value; OnPropertyChanged(nameof(Band1KHz)); }
        }
        
        public int Band2KHz
        {
            get => _band2KHz;
            set { _band2KHz = value; OnPropertyChanged(nameof(Band2KHz)); }
        }
        
        public int Band4KHz
        {
            get => _band4KHz;
            set { _band4KHz = value; OnPropertyChanged(nameof(Band4KHz)); }
        }
        
        public int Band8KHz
        {
            get => _band8KHz;
            set { _band8KHz = value; OnPropertyChanged(nameof(Band8KHz)); }
        }
        
        public int Band16KHz
        {
            get => _band16KHz;
            set { _band16KHz = value; OnPropertyChanged(nameof(Band16KHz)); }
        }
        
        public int BassBoost
        {
            get => _bassBoost;
            set { _bassBoost = value; OnPropertyChanged(nameof(BassBoost)); }
        }
        
        public int SpatialEffect
        {
            get => _spatialEffect;
            set { _spatialEffect = value; OnPropertyChanged(nameof(SpatialEffect)); }
        }
        
        public string SelectedPreset
        {
            get => _selectedPreset;
            set { _selectedPreset = value; OnPropertyChanged(nameof(SelectedPreset)); ApplyPreset(value); }
        }
        
        // 构造函数
        public EqualizerViewModel()
        {
            _userService = ServiceLocator.Instance.GetService<UserService>();
            _playerService = MediaPlayerService.Instance;
            
            // 初始化命令
            ApplyEqualizerCommand = new RelayCommand(ApplyEqualizer);
            ResetEqualizerCommand = new RelayCommand(ResetEqualizer);
            SelectPresetCommand = new RelayCommand<string>(SelectPreset);
            
            // 加载用户的均衡器设置
            LoadEqualizerSettings();
        }
        
        // 加载均衡器设置
        private void LoadEqualizerSettings()
        {
            try
            {
                if (_userService == null || _userService.CurrentUser == null)
                {
                    // 使用默认设置
                    ResetToDefault();
                    return;
                }
                
                // 从用户设置中获取均衡器设置
                var settingsJson = _userService.CurrentUser.Settings.EqualizerSettings;
                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson == "{}")
                {
                    // 使用默认设置
                    ResetToDefault();
                    return;
                }
                
                // 反序列化JSON
                var settings = JsonConvert.DeserializeObject<EqualizerSettings>(settingsJson);
                
                // 应用设置
                Band32Hz = settings.Band32Hz;
                Band64Hz = settings.Band64Hz;
                Band125Hz = settings.Band125Hz;
                Band250Hz = settings.Band250Hz;
                Band500Hz = settings.Band500Hz;
                Band1KHz = settings.Band1KHz;
                Band2KHz = settings.Band2KHz;
                Band4KHz = settings.Band4KHz;
                Band8KHz = settings.Band8KHz;
                Band16KHz = settings.Band16KHz;
                BassBoost = settings.BassBoost;
                SpatialEffect = settings.SpatialEffect;
                SelectedPreset = settings.Preset;
                
                // TODO: 应用到音频输出
                // 这里应该调用NAudio或其他音频处理库来应用均衡器设置
            }
            catch (Exception ex)
            {
                App.Logger.Warn(ex, "加载均衡器设置失败");
                // 使用默认设置
                ResetToDefault();
            }
        }
        
        // 保存均衡器设置
        private void SaveEqualizerSettings()
        {
            try
            {
                if (_userService == null || _userService.CurrentUser == null)
                {
                    App.Logger.Warn("无法保存均衡器设置：用户未登录");
                    return;
                }
                
                // 创建设置对象
                var settings = new EqualizerSettings
                {
                    Band32Hz = Band32Hz,
                    Band64Hz = Band64Hz,
                    Band125Hz = Band125Hz,
                    Band250Hz = Band250Hz,
                    Band500Hz = Band500Hz,
                    Band1KHz = Band1KHz,
                    Band2KHz = Band2KHz,
                    Band4KHz = Band4KHz,
                    Band8KHz = Band8KHz,
                    Band16KHz = Band16KHz,
                    BassBoost = BassBoost,
                    SpatialEffect = SpatialEffect,
                    Preset = SelectedPreset
                };
                
                // 序列化为JSON
                var settingsJson = JsonConvert.SerializeObject(settings);
                
                // 保存到用户设置
                _userService.CurrentUser.Settings.EqualizerSettings = settingsJson;
                _userService.SaveUserSettingsAsync(_userService.CurrentUser.Settings).Wait();
                
                App.Logger.Info("均衡器设置已保存");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "保存均衡器设置失败");
            }
        }
        
        // 应用均衡器设置
        private void ApplyEqualizer()
        {
            try
            {
                // 保存设置
                SaveEqualizerSettings();
                
                // TODO: 应用到音频输出
                // 这里应该调用NAudio或其他音频处理库来应用均衡器设置
                
                App.Logger.Info("均衡器设置已应用");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "应用均衡器设置失败");
            }
        }
        
        // 重置均衡器设置
        private void ResetEqualizer()
        {
            ResetToDefault();
            ApplyEqualizer();
        }
        
        // 重置为默认值
        private void ResetToDefault()
        {
            Band32Hz = 0;
            Band64Hz = 0;
            Band125Hz = 0;
            Band250Hz = 0;
            Band500Hz = 0;
            Band1KHz = 0;
            Band2KHz = 0;
            Band4KHz = 0;
            Band8KHz = 0;
            Band16KHz = 0;
            BassBoost = 50;
            SpatialEffect = 30;
            SelectedPreset = "自定义";
        }
        
        // 选择预设
        private void SelectPreset(string preset)
        {
            SelectedPreset = preset;
        }
        
        // 应用预设
        private void ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "流行":
                    Band32Hz = 4;
                    Band64Hz = 3;
                    Band125Hz = 2;
                    Band250Hz = 0;
                    Band500Hz = -1;
                    Band1KHz = -1;
                    Band2KHz = 0;
                    Band4KHz = 2;
                    Band8KHz = 3;
                    Band16KHz = 4;
                    BassBoost = 60;
                    SpatialEffect = 40;
                    break;
                    
                case "摇滚":
                    Band32Hz = 5;
                    Band64Hz = 4;
                    Band125Hz = 3;
                    Band250Hz = 1;
                    Band500Hz = -1;
                    Band1KHz = -2;
                    Band2KHz = 0;
                    Band4KHz = 2;
                    Band8KHz = 3;
                    Band16KHz = 3;
                    BassBoost = 70;
                    SpatialEffect = 50;
                    break;
                    
                case "古典":
                    Band32Hz = 3;
                    Band64Hz = 3;
                    Band125Hz = 2;
                    Band250Hz = 1;
                    Band500Hz = 0;
                    Band1KHz = 0;
                    Band2KHz = 0;
                    Band4KHz = 1;
                    Band8KHz = 2;
                    Band16KHz = 2;
                    BassBoost = 40;
                    SpatialEffect = 60;
                    break;
                    
                case "电子":
                    Band32Hz = 6;
                    Band64Hz = 5;
                    Band125Hz = 2;
                    Band250Hz = 0;
                    Band500Hz = -2;
                    Band1KHz = 0;
                    Band2KHz = 2;
                    Band4KHz = 3;
                    Band8KHz = 4;
                    Band16KHz = 5;
                    BassBoost = 80;
                    SpatialEffect = 70;
                    break;
                    
                case "爵士":
                    Band32Hz = 2;
                    Band64Hz = 1;
                    Band125Hz = 0;
                    Band250Hz = 0;
                    Band500Hz = 0;
                    Band1KHz = 1;
                    Band2KHz = 2;
                    Band4KHz = 3;
                    Band8KHz = 2;
                    Band16KHz = 1;
                    BassBoost = 30;
                    SpatialEffect = 40;
                    break;
                    
                case "嘻哈":
                    Band32Hz = 8;
                    Band64Hz = 7;
                    Band125Hz = 5;
                    Band250Hz = 2;
                    Band500Hz = 0;
                    Band1KHz = 1;
                    Band2KHz = 0;
                    Band4KHz = 2;
                    Band8KHz = 3;
                    Band16KHz = 3;
                    BassBoost = 90;
                    SpatialEffect = 40;
                    break;
                    
                case "自定义":
                    // 保持当前值不变
                    break;
                
                default:
                    ResetToDefault();
                    break;
            }
        }
    }
    
    // 均衡器设置类
    public class EqualizerSettings
    {
        public int Band32Hz { get; set; }
        public int Band64Hz { get; set; }
        public int Band125Hz { get; set; }
        public int Band250Hz { get; set; }
        public int Band500Hz { get; set; }
        public int Band1KHz { get; set; }
        public int Band2KHz { get; set; }
        public int Band4KHz { get; set; }
        public int Band8KHz { get; set; }
        public int Band16KHz { get; set; }
        public int BassBoost { get; set; }
        public int SpatialEffect { get; set; }
        public string Preset { get; set; } = "自定义";
    }
} 