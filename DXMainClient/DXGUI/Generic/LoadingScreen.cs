using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClientCore;
using ClientCore.CnCNet5;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.DXGUI.Multiplayer;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.Online;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace DTAClient.DXGUI.Generic
{
    public class LoadingScreen : XNAWindow
    {
        public LoadingScreen(
            CnCNetManager cncnetManager,
            WindowManager windowManager,
            IServiceProvider serviceProvider,
            MapLoader mapLoader
        ) : base(windowManager)
        {
            this.cncnetManager = cncnetManager;
            this.serviceProvider = serviceProvider;
            this.mapLoader = mapLoader;
            content = new ContentManager(serviceProvider, "Content");
            graphicsDevice = serviceProvider.GetService<GraphicsDevice>();
        }

        private static readonly object locker = new object();

        private MapLoader mapLoader;

        private PrivateMessagingPanel privateMessagingPanel;

        private bool visibleSpriteCursor;

        // Updater removed
        private Task mapLoadTask;
        private readonly CnCNetManager cncnetManager;
        private readonly IServiceProvider serviceProvider;
        private readonly GraphicsDevice graphicsDevice;
        private readonly object videoFrameLock = new object();
        private string configuredBackgroundPath;
        private LibVLC loadingVideoLibVlc;
        private MediaPlayer loadingVideoPlayer;
        private Media loadingVideoMedia;
        private Texture2D loadingVideoTexture;
        private IntPtr loadingVideoFrameMemory = IntPtr.Zero;
        private IntPtr loadingVideoFramePointer = IntPtr.Zero;
        private byte[] pendingVideoFrame;
        private byte[] textureUploadFrame;
        private int loadingVideoWidth;
        private int loadingVideoHeight;
        private int loadingVideoPitch;
        private bool hasPendingVideoFrame;
        private bool hasReceivedFirstVideoFrame;
        private bool hasDrawnFirstVideoFrame;
        private double minimumVideoDisplaySeconds = -1.0; // -1 表示未设置，将从视频时长自动获取
        private bool minimumVideoDisplaySecondsSetFromIni;
        private bool skipRequested;
        private KeyboardState lastKeyboardState;
        private double loadingVideoVolumePercent = 100.0;
        private string loadingVideoAudioOutput = "directsound";
        private DateTime loadingScreenStartedAtUtc;
        private DateTime firstVideoFrameUploadedAtUtc;
        private bool loggedWaitingForFirstVideoFrame;
        private bool loggedWaitingForMinimumVideoDisplay;
        private MediaPlayer.LibVLCVideoLockCb loadingVideoLockCallback;
        private MediaPlayer.LibVLCVideoUnlockCb loadingVideoUnlockCallback;
        private MediaPlayer.LibVLCVideoDisplayCb loadingVideoDisplayCallback;

        ContentManager content;
        public override void Initialize()
        {
            ClientRectangle = new Rectangle(0, 0, 1280, 768);
            Name = "LoadingScreen";
            loadingScreenStartedAtUtc = DateTime.UtcNow;

            base.Initialize();

            configuredBackgroundPath ??= GetConfiguredBackgroundPath();
            CenterOnParent();

            if (!TryStartConfiguredVideoBackground())
                LoadFallbackWallpaper();

            // Updater removed: no updater initialization
            mapLoadTask = mapLoader.LoadMapsAsync();

            if (Cursor.Visible)
            {
                Cursor.Visible = false;
                visibleSpriteCursor = true;
            }
        }

        // Updater removed: version checks disabled

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            if (key == "BackgroundTexture" && IsVideoFile(value))
            {
                // XNAWindow 的通用 INI 解析默认会把 BackgroundTexture 当图片交给 AssetLoader。
                // 当配置值是 loadingscreen.mp4 时，如果继续调用基类，就会出现
                // “This image format is not supported”，并且视频逻辑还没启动就已经走错分支。
                // 这里先拦截视频路径，只保存配置值，后续 Initialize() 再交给 LibVLC 解码。
                configuredBackgroundPath = GetAbsolutePathFromIni(iniFile, value);
                Logger.Log("LoadingScreen: Video background configured: " + configuredBackgroundPath);
                return;
            }

            if (key == "MinimumVideoDisplaySeconds")
            {
                // 现在视频帧已经能进 XNA 纹理，但 AGWar 的地图和统计加载很快，
                // 载入界面可能在第一帧刚画出来后立刻 Finish()，视觉上就是“一闪而过”。
                // 这个参数用于保证视频背景至少展示一段时间；
                // 默认值 -1 表示自动从视频时长获取，INI 可覆盖为具体秒数。
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedSeconds))
                {
                    minimumVideoDisplaySeconds = Math.Max(0.0, parsedSeconds);
                    minimumVideoDisplaySecondsSetFromIni = true;
                    Logger.Log("LoadingScreen: Minimum video display seconds configured from INI: " + minimumVideoDisplaySeconds);
                }
                else
                {
                    Logger.Log("LoadingScreen: Invalid MinimumVideoDisplaySeconds value: " + value);
                }

                return;
            }

            if (key == "LoadingVideoAudioOutput")
            {
                // LibVLC 在 Windows 下有多个音频输出模块，例如 directsound、mmdevice、waveout。
                // 之前只设置了音量，实际使用哪个输出模块完全交给 LibVLC 自动选择；
                // 这样在某些机器上可能视频帧正常解码，但音频没有送到系统默认设备。
                // 这里允许 INI 覆盖，默认值在字段里是 directsound，方便无需重新编译就切换模块排查。
                loadingVideoAudioOutput = value.Trim();
                Logger.Log("LoadingScreen: Loading video audio output configured: " + loadingVideoAudioOutput);
                return;
            }

            if (key == "LoadingVideoVolumePercent")
            {
                // 载入视频是 LoadingScreen.ini 配置的素材，和客户端 UI 音效不是同一个用途。
                // 之前直接套用 ClientVolume；日志里看到 ClientVolume=0.1 时，VLC 音量只有 10，
                // 对很多开场视频来说会接近听不到。这里给载入视频单独的百分比音量，
                // 默认值在字段里是 100，需要静音或降低音量时可在 INI 里显式配置 0-100。
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedPercent))
                {
                    loadingVideoVolumePercent = Math.Clamp(parsedPercent, 0.0, 100.0);
                    Logger.Log("LoadingScreen: Loading video volume percent configured: " + loadingVideoVolumePercent);
                }
                else
                {
                    Logger.Log("LoadingScreen: Invalid LoadingVideoVolumePercent value: " + value);
                }

                return;
            }

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        private void Finish()
        {
            StopLoadingVideo();

            ProgramConstants.GAME_VERSION = ClientConfiguration.Instance.ModMode ?
                "N/A" : ClientConfiguration.Instance.LocalGame;

            MainMenu mainMenu = serviceProvider.GetService<MainMenu>();

            WindowManager.AddAndInitializeControl(mainMenu);
            mainMenu.PostInit();

            if (UserINISettings.Instance.AutomaticCnCNetLogin &&
                NameValidator.IsNameValid(ProgramConstants.PLAYERNAME) == null)
            {
                cncnetManager.Connect();
            }

            if (!UserINISettings.Instance.PrivacyPolicyAccepted)
            {
                WindowManager.AddAndInitializeControl(new PrivacyNotification(WindowManager));
            }

            WindowManager.RemoveControl(this);

            Cursor.Visible = visibleSpriteCursor;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // 检测任意键按下以跳过载入画面
            if (!skipRequested)
            {
                KeyboardState currentKeyboardState = Keyboard.KeyboardState;
                if (lastKeyboardState != null)
                {
                    foreach (var key in currentKeyboardState.GetPressedKeys())
                    {
                        if (lastKeyboardState.IsKeyUp(key))
                        {
                            skipRequested = true;
                            Logger.Log("LoadingScreen: Skip requested by key press: " + key);
                            break;
                        }
                    }
                }
                lastKeyboardState = currentKeyboardState;
            }

            if (mapLoadTask.Status == TaskStatus.RanToCompletion && CanFinishLoadingScreen())
                Finish();
        }


        private bool CanFinishLoadingScreen()
        {
            // 按任意键跳过：用户主动跳过时，不再等待最短展示时间
            if (skipRequested)
            {
                Logger.Log("LoadingScreen: Skipping loading screen by user key press.");
                return true;
            }

            if (loadingVideoPlayer == null)
                return true;

            // minimumVideoDisplaySeconds 为 -1 时表示还没获取到视频时长，
            // 需要等待 Playing 事件中获取时长后设置，或等超时后放行
            if (minimumVideoDisplaySeconds < 0.0)
            {
                // 视频时长还未获取到，但地图已加载完。
                // 短暂等待以避免一闪而过，2 秒后如果还没有时长信息就放行。
                double secondsSinceStart = (DateTime.UtcNow - loadingScreenStartedAtUtc).TotalSeconds;
                if (secondsSinceStart < 2.0)
                    return false;

                Logger.Log("LoadingScreen: Video duration not available before timeout; finishing without minimum video delay.");
                return true;
            }

            if (minimumVideoDisplaySeconds <= 0.0)
                return true;

            DateTime now = DateTime.UtcNow;

            if (!hasDrawnFirstVideoFrame)
            {
                // 如果 VLC 已经启动但第一帧还没上传，短暂等待，避免地图加载太快时直接切走。
                // 这里设置 2 秒上限，防止异常视频或解码失败时永久卡在载入界面。
                double secondsSinceStart = (now - loadingScreenStartedAtUtc).TotalSeconds;
                if (secondsSinceStart < 2.0)
                {
                    if (!loggedWaitingForFirstVideoFrame)
                    {
                        loggedWaitingForFirstVideoFrame = true;
                        Logger.Log("LoadingScreen: Waiting for first video frame before finishing.");
                    }

                    return false;
                }

                Logger.Log("LoadingScreen: First video frame was not uploaded before timeout; finishing without minimum video delay.");
                return true;
            }

            double displayedSeconds = (now - firstVideoFrameUploadedAtUtc).TotalSeconds;
            if (displayedSeconds < minimumVideoDisplaySeconds)
            {
                if (!loggedWaitingForMinimumVideoDisplay)
                {
                    loggedWaitingForMinimumVideoDisplay = true;
                    Logger.Log($"LoadingScreen: Map loading finished, keeping video visible for at least {minimumVideoDisplaySeconds:0.###} seconds.");
                }

                return false;
            }

            return true;
        }

        public override void Draw(GameTime gameTime)
        {
            UploadPendingVideoFrameToTexture();

            if (loadingVideoTexture != null)
            {
                // 视频帧已经被上传成 XNA Texture2D 后，必须走 XNA 自己的绘制管线。
                // 这样不会再受 WinForms 原生子窗口、DirectX 后备缓冲区层级或置顶关系影响。
                DrawTexture(loadingVideoTexture, new Rectangle(0, 0, Width, Height), Color.White);
            }

            base.Draw(gameTime);
        }

        private string GetConfiguredBackgroundPath()
        {
            if (string.IsNullOrWhiteSpace(ThemeIni?.FileName))
                return null;

            string configuredPath = ThemeIni.GetStringValue(Name, "BackgroundTexture", string.Empty);
            if (string.IsNullOrWhiteSpace(configuredPath))
                return null;

            // LoadingScreen.ini 中的 BackgroundTexture 允许继续写相对路径。
            // 相对路径以当前 INI 所在目录为基准，示例：
            // Resources/LoadingScreen.ini + loadingscreen.mp4 =>
            // Resources/loadingscreen.mp4。这样打包目录和玩家本地目录都能保持一致。
            return GetAbsolutePathFromIni(ThemeIni, configuredPath);
        }

        private string GetAbsolutePathFromIni(IniFile iniFile, string configuredPath)
        {
            if (Path.IsPathRooted(configuredPath))
                return configuredPath;

            string iniDirectory = Path.GetDirectoryName(iniFile.FileName);
            return Path.GetFullPath(Path.Combine(iniDirectory, configuredPath));
        }

        private bool IsVideoFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return
                extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        }

        private string GetLibVlcDirectory()
        {
            // VideoLAN.LibVLC.Windows 会把 VLC 原生文件放在输出目录的 libvlc\win-x64、
            // libvlc\win-x86 或 libvlc\win-arm64 下。客户端运行时是从
            // Resources\Binaries\Windows 加载 clientdx.dll，不一定以该目录作为
            // AppContext.BaseDirectory，所以不能依赖 Core.Initialize() 的默认搜索路径。
            string assemblyDirectory = Path.GetDirectoryName(typeof(LoadingScreen).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
                assemblyDirectory = AppContext.BaseDirectory;

            string platformDirectoryName = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X64 => "win-x64",
                System.Runtime.InteropServices.Architecture.X86 => "win-x86",
                System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
                _ => "win-x64"
            };

            string libVlcDirectory = Path.Combine(assemblyDirectory, "libvlc", platformDirectoryName);
            if (Directory.Exists(libVlcDirectory))
                return libVlcDirectory;

            // 保留一个兼容兜底：如果以后打包脚本把 libvlc.dll 直接放在 libvlc 根目录，
            // 这里仍然能给 LibVLCSharp 一个有效目录，同时日志会把实际路径打印出来方便排查。
            return Path.Combine(assemblyDirectory, "libvlc");
        }

        private bool TryStartConfiguredVideoBackground()
        {
            if (string.IsNullOrWhiteSpace(configuredBackgroundPath))
                return false;

            if (!IsVideoFile(configuredBackgroundPath))
                return false;

            if (!File.Exists(configuredBackgroundPath))
            {
                Logger.Log("LoadingScreen: Video background file does not exist: " + configuredBackgroundPath);
                return false;
            }

            try
            {
                string libVlcDirectory = GetLibVlcDirectory();
                Logger.Log("LoadingScreen: Initializing LibVLC from: " + libVlcDirectory);

                // 显式传入 VLC 原生目录，避免客户端主程序目录和 clientdx.dll 所在目录不一致时，
                // LibVLCSharp 去错误的位置查找 libvlc.dll、libvlccore.dll 或 plugins。
                Core.Initialize(libVlcDirectory);

                InitializeVideoFrameBuffers();

                loadingVideoLibVlc = new LibVLC("--no-video-title-show", "--quiet");
                LogAvailableLoadingVideoAudioOutputs();

                int loadingVideoVolume = Math.Clamp((int)Math.Round(loadingVideoVolumePercent), 0, 100);
                loadingVideoPlayer = new MediaPlayer(loadingVideoLibVlc);
                loadingVideoPlayer.EncounteredError += (sender, args) => Logger.Log("LoadingScreen: LibVLC media player encountered an error during loading video playback.");
                loadingVideoPlayer.Playing += (sender, args) =>
                {
                    // 有些音频后端在真正开始播放前还没有活动音频流，Mute/Volume 读取值可能不可靠。
                    // 播放事件触发后再补一次音量和静音状态，保证音频流建立后仍然使用客户端音量。
                    MediaPlayer player = loadingVideoPlayer;
                    if (player == null)
                        return;

                    player.Mute = false;
                    player.Volume = loadingVideoVolume;

                    // 从视频时长自动设置 minimumVideoDisplaySeconds
                    // MediaPlayer.Length 在 Playing 事件触发后可用，返回毫秒
                    if (!minimumVideoDisplaySecondsSetFromIni && minimumVideoDisplaySeconds < 0.0)
                    {
                        long durationMs = player.Length;
                        if (durationMs > 0)
                        {
                            minimumVideoDisplaySeconds = durationMs / 1000.0;
                            Logger.Log($"LoadingScreen: Auto-detected video duration: {minimumVideoDisplaySeconds:0.###} seconds ({durationMs} ms)");
                        }
                        else
                        {
                            // Length 有时在 Playing 事件中还不一定就绪，延迟一帧再取
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                await System.Threading.Tasks.Task.Delay(200);
                                MediaPlayer p = loadingVideoPlayer;
                                if (p != null && !minimumVideoDisplaySecondsSetFromIni && minimumVideoDisplaySeconds < 0.0)
                                {
                                    long d = p.Length;
                                    if (d > 0)
                                    {
                                        minimumVideoDisplaySeconds = d / 1000.0;
                                        Logger.Log($"LoadingScreen: Auto-detected video duration (delayed): {minimumVideoDisplaySeconds:0.###} seconds ({d} ms)");
                                    }
                                    else
                                    {
                                        minimumVideoDisplaySeconds = 4.0; // 回退默认值
                                        Logger.Log("LoadingScreen: Could not detect video duration, using fallback: 4 seconds");
                                    }
                                }
                            });
                        }
                    }

                    Logger.Log($"LoadingScreen: LibVLC loading video is playing. audioTrackCount={player.AudioTrackCount}, audioTrack={player.AudioTrack}, muted={player.Mute}, volume={player.Volume}");
                };
                loadingVideoPlayer.Muted += (sender, args) => Logger.Log("LoadingScreen: LibVLC loading video reported muted.");
                loadingVideoPlayer.VolumeChanged += (sender, args) =>
                {
                    // VolumeChanged 也来自 LibVLC 线程，载入界面结束时播放器可能已经被释放。
                    // 这里用本地引用做保护，只记录仍然存活的播放器状态。
                    MediaPlayer player = loadingVideoPlayer;
                    if (player != null)
                        Logger.Log("LoadingScreen: LibVLC loading video volume changed to: " + player.Volume);
                };

                // SetAudioOutput 必须在 Play() 之前调用；LibVLC 文档明确说明播放中切换不会立刻生效。
                // 默认使用 directsound，因为它在旧 DirectX/XNA 客户端环境里通常比自动选择更稳定。
                bool audioOutputSelected = !string.IsNullOrWhiteSpace(loadingVideoAudioOutput) &&
                    loadingVideoPlayer.SetAudioOutput(loadingVideoAudioOutput);
                if (!audioOutputSelected && !loadingVideoAudioOutput.Equals("mmdevice", StringComparison.OrdinalIgnoreCase))
                {
                    // 如果 INI 指定的模块不可用，回退到 Windows 默认设备模块 mmdevice，避免因为配置拼错直接无声。
                    audioOutputSelected = loadingVideoPlayer.SetAudioOutput("mmdevice");
                    Logger.Log("LoadingScreen: Falling back to LibVLC loading video audio output: mmdevice, success=" + audioOutputSelected);
                }

                loadingVideoPlayer.Mute = false;
                loadingVideoPlayer.Volume = loadingVideoVolume;
                Logger.Log($"LoadingScreen: LibVLC loading video audio configured. requestedOutput={loadingVideoAudioOutput}, selected={audioOutputSelected}, clientVolume={UserINISettings.Instance.ClientVolume:0.###}, loadingVideoVolumePercent={loadingVideoVolumePercent:0.###}, vlcVolume={loadingVideoVolume}, muted={loadingVideoPlayer.Mute}");

                loadingVideoLockCallback = LockVideoFrame;
                loadingVideoUnlockCallback = UnlockVideoFrame;
                loadingVideoDisplayCallback = DisplayVideoFrame;

                // 这里不再使用 LibVLCSharp.WinForms.VideoView。
                // 日志已经证明 LibVLC 可以播放，但 VideoView 作为 WinForms 原生子窗口无法可靠显示在
                // MonoGame/DirectX 的交换链上方。改用 video callbacks 后，VLC 只负责解码，最终显示
                // 由 LoadingScreen.Draw() 上传 Texture2D 并绘制，和普通 XNA 背景图片走同一条渲染管线。
                loadingVideoPlayer.SetVideoFormat("RV32", (uint)loadingVideoWidth, (uint)loadingVideoHeight, (uint)loadingVideoPitch);
                loadingVideoPlayer.SetVideoCallbacks(loadingVideoLockCallback, loadingVideoUnlockCallback, loadingVideoDisplayCallback);

                loadingVideoMedia = new Media(loadingVideoLibVlc, new Uri(configuredBackgroundPath));
                loadingVideoMedia.AddOption(":input-repeat=65535");
                loadingVideoMedia.AddOption(":no-video-title-show");
                StartLoadingVideoMediaDiagnostics(loadingVideoMedia);

                if (!loadingVideoPlayer.Play(loadingVideoMedia))
                {
                    Logger.Log("LoadingScreen: LibVLC failed to start video callback playback.");
                    StopLoadingVideo();
                    return false;
                }

                Logger.Log($"LoadingScreen: LibVLC callback video background started: {configuredBackgroundPath}, {loadingVideoWidth}x{loadingVideoHeight}, pitch={loadingVideoPitch}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Initializing LibVLC callback video background failed! " + ex);
                StopLoadingVideo();
                return false;
            }
        }

        private void LogAvailableLoadingVideoAudioOutputs()
        {
            try
            {
                foreach (var audioOutput in loadingVideoLibVlc.AudioOutputs)
                {
                    // 记录 LibVLC 当前实际枚举到的音频输出模块。仅检查 plugins 目录存在还不够；
                    // 如果模块没有被 LibVLC 成功加载，这里就不会出现对应名称。
                    Logger.Log($"LoadingScreen: LibVLC audio output available: {audioOutput.Name} ({audioOutput.Description})");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Enumerating LibVLC audio outputs failed! " + ex.Message);
            }
        }

        private void StartLoadingVideoMediaDiagnostics(Media media)
        {
            try
            {
                // ffprobe 不一定存在于玩家机器上，所以用 LibVLC 自己解析 mp4 轨道。
                // 解析放到后台任务，不阻塞载入界面；日志会告诉我们素材是否真的包含 Audio track。
                _ = media.Parse(MediaParseOptions.ParseLocal, 1500)
                    .ContinueWith(task => LogLoadingVideoMediaTracks(media, task), TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Starting LibVLC media diagnostics failed! " + ex.Message);
            }
        }

        private void LogLoadingVideoMediaTracks(Media media, Task<MediaParsedStatus> parseTask)
        {
            try
            {
                if (parseTask.IsFaulted)
                {
                    Logger.Log("LoadingScreen: LibVLC media track parsing failed! " + parseTask.Exception?.GetBaseException().Message);
                    return;
                }

                int audioTrackCount = 0;
                int videoTrackCount = 0;
                Logger.Log("LoadingScreen: LibVLC media parse status: " + parseTask.Result);

                foreach (MediaTrack track in media.Tracks)
                {
                    string codecDescription = media.CodecDescription(track.TrackType, track.Codec);
                    if (track.TrackType == TrackType.Audio)
                    {
                        audioTrackCount++;
                        Logger.Log($"LoadingScreen: LibVLC media audio track detected. id={track.Id}, codec={codecDescription}, channels={track.Data.Audio.Channels}, rate={track.Data.Audio.Rate}, bitrate={track.Bitrate}, language={track.Language}");
                    }
                    else if (track.TrackType == TrackType.Video)
                    {
                        videoTrackCount++;
                        Logger.Log($"LoadingScreen: LibVLC media video track detected. id={track.Id}, codec={codecDescription}, bitrate={track.Bitrate}");
                    }
                }

                Logger.Log($"LoadingScreen: LibVLC media track summary. audioTracks={audioTrackCount}, videoTracks={videoTrackCount}");
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Logging LibVLC media tracks failed! " + ex.Message);
            }
        }

        private void InitializeVideoFrameBuffers()
        {
            // 解码尺寸使用当前渲染分辨率。载入界面背景本来就是全屏图，直接让 VLC 转成同尺寸帧，
            // Draw() 阶段只需要一次贴图绘制，不再做额外缩放或裁剪计算。
            loadingVideoWidth = Math.Max(1, WindowManager.RenderResolutionX);
            loadingVideoHeight = Math.Max(1, WindowManager.RenderResolutionY);
            loadingVideoPitch = loadingVideoWidth * 4;

            int frameSize = checked(loadingVideoPitch * loadingVideoHeight);
            loadingVideoFrameMemory = System.Runtime.InteropServices.Marshal.AllocHGlobal(frameSize + 31);
            long alignedAddress = (loadingVideoFrameMemory.ToInt64() + 31) & ~31L;
            loadingVideoFramePointer = new IntPtr(alignedAddress);

            pendingVideoFrame = new byte[frameSize];
            textureUploadFrame = new byte[frameSize];

            Logger.Log($"LoadingScreen: Allocated LibVLC video callback buffer: {loadingVideoWidth}x{loadingVideoHeight}, bytes={frameSize}");
        }

        private IntPtr LockVideoFrame(IntPtr opaque, IntPtr planes)
        {
            if (loadingVideoFramePointer == IntPtr.Zero)
                return IntPtr.Zero;

            // LibVLC 要求 lock 回调把每个平面的起始地址写入 planes。
            // RV32 只有一个像素平面，所以这里只写第 0 个平面，地址指向 32 字节对齐的非托管缓冲区。
            System.Runtime.InteropServices.Marshal.WriteIntPtr(planes, loadingVideoFramePointer);
            return loadingVideoFramePointer;
        }

        private void UnlockVideoFrame(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
            if (loadingVideoFramePointer == IntPtr.Zero || pendingVideoFrame == null)
                return;

            lock (videoFrameLock)
            {
                // VLC 的解码线程和 XNA 的绘制线程不同，不能在回调里直接操作 Texture2D。
                // 这里先把非托管帧复制到托管 pending buffer，真正上传显存放到 Draw() 线程处理。
                System.Runtime.InteropServices.Marshal.Copy(loadingVideoFramePointer, pendingVideoFrame, 0, pendingVideoFrame.Length);
                hasPendingVideoFrame = true;
            }

            if (!hasReceivedFirstVideoFrame)
            {
                hasReceivedFirstVideoFrame = true;
                Logger.Log("LoadingScreen: First LibVLC callback video frame received.");
            }
        }

        private void DisplayVideoFrame(IntPtr opaque, IntPtr picture)
        {
            // 显示时机由 LibVLC 调度，但实际显示必须交给 XNA 主线程。
            // UnlockVideoFrame 已经保存了最新帧，Draw() 每帧检查并上传即可。
        }

        private void UploadPendingVideoFrameToTexture()
        {
            if (!hasPendingVideoFrame || pendingVideoFrame == null || textureUploadFrame == null)
                return;

            lock (videoFrameLock)
            {
                if (!hasPendingVideoFrame)
                    return;

                Buffer.BlockCopy(pendingVideoFrame, 0, textureUploadFrame, 0, pendingVideoFrame.Length);
                hasPendingVideoFrame = false;
            }

            // RV32 的第 4 个字节在部分 VLC 输出里不是可用 alpha。
            // XNA 默认混合会读取 alpha；如果这里保持 0，画面就会像完全透明一样“没有播放”。
            for (int i = 3; i < textureUploadFrame.Length; i += 4)
                textureUploadFrame[i] = 255;

            if (loadingVideoTexture == null || loadingVideoTexture.Width != loadingVideoWidth || loadingVideoTexture.Height != loadingVideoHeight)
            {
                loadingVideoTexture?.Dispose();
                loadingVideoTexture = new Texture2D(graphicsDevice ?? WindowManager.Game.GraphicsDevice, loadingVideoWidth, loadingVideoHeight, false, SurfaceFormat.Color);
            }

            loadingVideoTexture.SetData(textureUploadFrame);

            if (!hasDrawnFirstVideoFrame)
            {
                hasDrawnFirstVideoFrame = true;
                firstVideoFrameUploadedAtUtc = DateTime.UtcNow;
                Logger.Log("LoadingScreen: First LibVLC callback video frame uploaded to XNA texture.");
            }
        }

        private void StopLoadingVideo()
        {
            try
            {
                loadingVideoPlayer?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Stopping LibVLC video background failed! " + ex.Message);
            }

            loadingVideoMedia?.Dispose();
            loadingVideoMedia = null;

            loadingVideoPlayer?.Dispose();
            loadingVideoPlayer = null;

            loadingVideoLibVlc?.Dispose();
            loadingVideoLibVlc = null;

            loadingVideoTexture?.Dispose();
            loadingVideoTexture = null;

            FreeVideoFrameBuffers();

            loadingVideoLockCallback = null;
            loadingVideoUnlockCallback = null;
            loadingVideoDisplayCallback = null;
            hasPendingVideoFrame = false;
        }

        private void FreeVideoFrameBuffers()
        {
            if (loadingVideoFrameMemory != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(loadingVideoFrameMemory);
                loadingVideoFrameMemory = IntPtr.Zero;
                loadingVideoFramePointer = IntPtr.Zero;
            }

            pendingVideoFrame = null;
            textureUploadFrame = null;
        }

        private void LoadFallbackWallpaper()
        {
            string[] wallpaper = Directory.GetFiles("Resources/" + ClientConfiguration.Instance.GetThemePath(UserINISettings.Instance.ClientTheme) + "Wallpaper");

            if (UserINISettings.Instance.Random_wallpaper)
            {
                Random ran = new Random();
                int i = ran.Next(0, wallpaper.Length);

                BackgroundTexture = AssetLoader.LoadTexture(wallpaper[i]);
            }
            else
            {
                BackgroundTexture = AssetLoader.LoadTexture(wallpaper[0]);
            }
        }
    }
}
