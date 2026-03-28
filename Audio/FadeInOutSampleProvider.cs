using System;
using NAudio.Wave;

namespace MusicPlayerApp.Audio
{
    /// <summary>
    /// 简化的淡入淡出处理类，减少音频处理导致的问题
    /// </summary>
    public class FadeInOutSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly WaveFormat waveFormat;
        
        // 淡入淡出参数
        private int fadeInSamplePosition;
        private int fadeInSampleCount;
        private int fadeOutSamplePosition;
        private int fadeOutSampleCount;
        private bool isFadingIn;
        private bool isFadingOut;
        
        // 添加播放启动标志，首次缓冲区处理特殊优化
        private bool isFirstBuffer = true;
        private const int SKIP_FRAMES_COUNT = 5; // 首次播放时跳过的处理帧数
        private int skippedFrames = 0;
        
        public FadeInOutSampleProvider(ISampleProvider source, bool startWithZeroVolume = false)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.waveFormat = source.WaveFormat;
            
            // 初始化淡入淡出参数
            fadeInSamplePosition = 0;
            fadeInSampleCount = 0;
            fadeOutSamplePosition = 0;
            fadeOutSampleCount = 0;
            isFadingIn = false;
            isFadingOut = false;
            
            // 如果需要从零音量开始，设置为静音但不开始淡入
            if (startWithZeroVolume)
            {
                isFadingIn = true;
                fadeInSampleCount = 1; // 会在BeginFadeIn中重新设置
            }
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            // 从源读取样本
            int samplesRead = source.Read(buffer, offset, count);
            
            if (samplesRead <= 0)
                return 0;
                
            // 首次播放优化 - 对前几帧不执行淡入淡出处理以加快启动
            if (isFirstBuffer)
            {
                if (skippedFrames < SKIP_FRAMES_COUNT)
                {
                    skippedFrames++;
                    
                    // 对于首次播放的前几帧，如果正在淡入，直接将音量设为中等值
                    // 避免从零开始的卡顿感
                    if (isFadingIn && count > 0)
                    {
                        float initialGain = 0.3f; // 设置一个初始增益值，避免从零开始的突兀
                        for (int i = 0; i < samplesRead; i++)
                        {
                            buffer[offset + i] *= initialGain;
                        }
                    }
                    
                    return samplesRead;
                }
                
                isFirstBuffer = false;
            }
            
            // 如果没有淡入淡出操作，直接返回
            if (!isFadingIn && !isFadingOut)
                return samplesRead;
            
            // 应用淡入淡出
            int sampleFrames = samplesRead / waveFormat.Channels;
            
            for (int sampleFrame = 0; sampleFrame < sampleFrames; sampleFrame++)
            {
                float gain = 1.0f;
                
                // 计算淡入增益 - 使用线性淡入，简化计算
                if (isFadingIn && fadeInSampleCount > 0)
                {
                    gain = (float)fadeInSamplePosition / fadeInSampleCount;
                    fadeInSamplePosition++;
                    
                    if (fadeInSamplePosition >= fadeInSampleCount)
                    {
                        isFadingIn = false;
                        gain = 1.0f;
                    }
                }
                
                // 计算淡出增益 - 使用线性淡出，简化计算
                if (isFadingOut && fadeOutSampleCount > 0)
                {
                    gain = 1.0f - ((float)fadeOutSamplePosition / fadeOutSampleCount);
                    fadeOutSamplePosition++;
                    
                    if (fadeOutSamplePosition >= fadeOutSampleCount)
                    {
                        isFadingOut = false;
                        gain = 0.0f;
                    }
                }
                
                // 对当前帧的所有通道应用增益
                int baseIndex = (sampleFrame * waveFormat.Channels) + offset;
                
                // 处理所有通道
                for (int channel = 0; channel < waveFormat.Channels; channel++)
                {
                    buffer[baseIndex + channel] *= gain;
                }
            }
            
            return samplesRead;
        }

        /// <summary>
        /// 开始淡入效果
        /// </summary>
        /// <param name="durationMs">淡入持续时间（毫秒）</param>
        public void BeginFadeIn(double durationMs)
        {
            // 对于非常短的淡入效果，直接设置增益为1以减少卡顿
            if (durationMs < 20)
            {
                isFadingIn = false;
                return;
            }
            
            // 重置首次缓冲区标志
            isFirstBuffer = true;
            skippedFrames = 0;
            
            // 计算淡入所需的样本数
            int sampleRate = waveFormat.SampleRate;
            fadeInSampleCount = (int)((durationMs * sampleRate) / 1000);
            fadeInSamplePosition = 0;
            isFadingIn = true;
            isFadingOut = false; // 取消可能的淡出
        }

        /// <summary>
        /// 开始淡出效果
        /// </summary>
        /// <param name="durationMs">淡出持续时间（毫秒）</param>
        public void BeginFadeOut(double durationMs)
        {
            // 对于非常短的淡出效果，直接设置增益为0以减少卡顿
            if (durationMs < 20)
            {
                isFadingIn = false;
                isFadingOut = false;
                return;
            }
            
            // 计算淡出所需的样本数
            int sampleRate = waveFormat.SampleRate;
            fadeOutSampleCount = (int)((durationMs * sampleRate) / 1000);
            fadeOutSamplePosition = 0;
            isFadingOut = true;
            isFadingIn = false; // 取消可能的淡入
        }
    }
} 