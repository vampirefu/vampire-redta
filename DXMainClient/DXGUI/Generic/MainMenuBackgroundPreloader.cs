using ClientCore;
using Rampastring.Tools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// 后台预解码 GIF 主菜单背景。
    /// 在 LoadingScreen 阶段就启动后台线程把 GIF 解码成 RGBA 字节数组，
    /// MainMenu.Initialize() 时只需把字节上传成 Texture2D，
    /// 避免在主线程上同步解码 100+ 帧 GIF 造成载入画面卡死。
    /// </summary>
    internal static class MainMenuBackgroundPreloader
    {
        private static readonly object _sync = new object();
        private static Task _task;
        private static PreloadedBackground _result;
        private static Exception _error;
        private static bool _started;
        private static string _startedPath;

        /// <summary>
        /// 启动后台 GIF 解码任务。重复调用同一路径会直接返回，不重复启动。
        /// </summary>
        public static void EnsureStarted(string gifAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(gifAbsolutePath))
                return;

            lock (_sync)
            {
                if (_started && string.Equals(_startedPath, gifAbsolutePath, StringComparison.OrdinalIgnoreCase))
                    return;

                _started = true;
                _startedPath = gifAbsolutePath;
                _result = null;
                _error = null;

                _task = Task.Run(() => DecodeGif(gifAbsolutePath));
            }
        }

        /// <summary>
        /// 查询预解码是否已完成。
        /// - 返回 true 且 result 非空：解码成功，可直接使用
        /// - 返回 true 且 result 为空：解码失败或已结束无结果，调用方应走 PNG 兜底并不再重试
        /// - 返回 false：仍在解码中，调用方可暂用 PNG 兜底，下一帧再查询
        /// </summary>
        public static bool TryGetResult(out PreloadedBackground result)
        {
            lock (_sync)
            {
                if (_result != null)
                {
                    result = _result;
                    return true;
                }

                if (_error != null || (_task != null && _task.IsCompleted))
                {
                    // 任务已结束但没有结果，调用方应走兜底并不再轮询
                    result = null;
                    return true;
                }

                result = null;
                return false;
            }
        }

        /// <summary>
        /// 重置预加载器状态。仅在客户端重启/换主题时调用。
        /// </summary>
        public static void Reset()
        {
            lock (_sync)
            {
                _started = false;
                _startedPath = null;
                _result = null;
                _error = null;
                _task = null;
            }
        }

        private static void DecodeGif(string path)
        {
            try
            {
                Logger.Log($"MainMenuBackgroundPreloader: 开始后台解码 GIF: {path}");
                var startedAt = DateTime.UtcNow;

                using Image image = Image.Load(path);
                int width = image.Width;
                int height = image.Height;
                int frameCount = image.Frames.Count;

                var frames = new PreloadedFrame[frameCount];

                for (int i = 0; i < frameCount; i++)
                {
                    using Image frameImage = image.Frames.CloneFrame(i);
                    using Image<Rgba32> rgba = frameImage.CloneAs<Rgba32>();

                    byte[] pixels = new byte[width * height * 4];
                    rgba.CopyPixelDataTo(pixels);

                    // GIF 背景图不需要透明度，强制不透明避免 SpriteBatch 混合出现暗边。
                    // 与 LoadingScreen 视频帧的处理方式一致。
                    for (int p = 3; p < pixels.Length; p += 4)
                        pixels[p] = 255;

                    TimeSpan duration = GetGifFrameDuration(
                        SixLabors.ImageSharp.MetadataExtensions.GetGifMetadata(image.Frames[i].Metadata));

                    frames[i] = new PreloadedFrame(pixels, duration);
                }

                var background = new PreloadedBackground(width, height, frames);

                lock (_sync)
                {
                    _result = background;
                }

                var elapsed = DateTime.UtcNow - startedAt;
                Logger.Log($"MainMenuBackgroundPreloader: 解码完成，{frameCount} 帧，{width}x{height}，耗时 {elapsed.TotalSeconds:0.00} 秒");
            }
            catch (Exception ex)
            {
                Logger.Log($"MainMenuBackgroundPreloader: 解码 GIF 失败! {ex.Message}");
                lock (_sync)
                {
                    _error = ex;
                }
            }
        }

        private static TimeSpan GetGifFrameDuration(GifFrameMetadata frameMetadata)
        {
            int frameDelay = frameMetadata.FrameDelay;

            if (frameDelay <= 0)
                frameDelay = 10;

            return TimeSpan.FromMilliseconds(frameDelay * 10);
        }
    }

    /// <summary>
    /// 已解码的 GIF 背景数据。帧像素为 RGBA32（与 MonoGame SurfaceFormat.Color 对应）。
    /// </summary>
    internal sealed class PreloadedBackground
    {
        public int Width { get; }
        public int Height { get; }
        public PreloadedFrame[] Frames { get; }

        public PreloadedBackground(int width, int height, PreloadedFrame[] frames)
        {
            Width = width;
            Height = height;
            Frames = frames;
        }
    }

    /// <summary>
    /// 单帧已解码数据。RgbaPixels 长度应为 Width * Height * 4。
    /// </summary>
    internal sealed class PreloadedFrame
    {
        public byte[] RgbaPixels { get; }
        public TimeSpan Duration { get; }

        public PreloadedFrame(byte[] rgbaPixels, TimeSpan duration)
        {
            RgbaPixels = rgbaPixels;
            Duration = duration;
        }
    }
}
