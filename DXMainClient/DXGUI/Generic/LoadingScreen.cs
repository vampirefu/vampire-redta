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
        }

        private static readonly object locker = new object();

        private MapLoader mapLoader;

        private PrivateMessagingPanel privateMessagingPanel;

        private bool visibleSpriteCursor;

        // Updater removed
        private Task mapLoadTask;
        private readonly CnCNetManager cncnetManager;
        private readonly IServiceProvider serviceProvider;
        private string configuredBackgroundPath;
        private LibVLC loadingVideoLibVlc;
        private MediaPlayer loadingVideoPlayer;
        private Media loadingVideoMedia;
        private VideoView loadingVideoView;
        private Control loadingVideoHostControl;

        ContentManager content;
        public override void Initialize()
        {
            ClientRectangle = new Rectangle(0, 0, 1280, 768);
            Name = "LoadingScreen";

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
                // 这里先拦截视频路径，只保存配置值，后续 Initialize() 再交给 LibVLC 播放。
                configuredBackgroundPath = GetAbsolutePathFromIni(iniFile, value);
                Logger.Log("LoadingScreen: Video background configured: " + configuredBackgroundPath);
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

            if (mapLoadTask.Status == TaskStatus.RanToCompletion)
                Finish();
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
                // 这里改用 LibVLCSharp.WinForms，而不是自己抓帧再转 XNA Texture2D。
                // VLC 直接负责 mp4 解码、音视频同步和硬件/软件渲染，避免 WPF RenderTargetBitmap
                // 在部分显卡或系统上抓到黑帧、透明帧的问题。
                loadingVideoHostControl = Control.FromHandle(WindowManager.Game.Window.Handle);
                if (loadingVideoHostControl == null)
                {
                    Logger.Log("LoadingScreen: Could not find WinForms host control for video background.");
                    return false;
                }

                string libVlcDirectory = GetLibVlcDirectory();
                Logger.Log("LoadingScreen: Initializing LibVLC from: " + libVlcDirectory);

                // 显式传入 VLC 原生目录，避免客户端主程序目录和 clientdx.dll 所在目录不一致时，
                // LibVLCSharp 去错误的位置查找 libvlc.dll、libvlccore.dll 或 plugins。
                Core.Initialize(libVlcDirectory);

                loadingVideoLibVlc = new LibVLC("--no-video-title-show", "--quiet");
                loadingVideoPlayer = new MediaPlayer(loadingVideoLibVlc)
                {
                    Volume = Math.Clamp((int)(UserINISettings.Instance.ClientVolume * 100.0), 0, 100)
                };

                loadingVideoMedia = new Media(loadingVideoLibVlc, new Uri(configuredBackgroundPath));
                loadingVideoMedia.AddOption(":input-repeat=65535");
                loadingVideoMedia.AddOption(":no-video-title-show");

                loadingVideoView = new VideoView
                {
                    MediaPlayer = loadingVideoPlayer,
                    Dock = DockStyle.Fill,
                    Enabled = false,
                    BackColor = System.Drawing.Color.Black
                };

                // VideoView 是原生 WinForms 控件，会由 LibVLC 直接画到窗口上。
                // 载入界面期间把它铺满宿主窗口；Finish() 会移除并释放，避免挡住主菜单。
                loadingVideoHostControl.Controls.Add(loadingVideoView);
                loadingVideoView.BringToFront();

                if (!loadingVideoPlayer.Play(loadingVideoMedia))
                {
                    Logger.Log("LoadingScreen: LibVLC failed to start video playback.");
                    StopLoadingVideo();
                    return false;
                }

                Logger.Log("LoadingScreen: LibVLC video background started: " + configuredBackgroundPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("LoadingScreen: Initializing LibVLC video background failed! " + ex);
                StopLoadingVideo();
                return false;
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

            RemoveLoadingVideoView();

            loadingVideoMedia?.Dispose();
            loadingVideoMedia = null;

            loadingVideoPlayer?.Dispose();
            loadingVideoPlayer = null;

            loadingVideoLibVlc?.Dispose();
            loadingVideoLibVlc = null;
        }

        private void RemoveLoadingVideoView()
        {
            if (loadingVideoView == null)
                return;

            void RemoveView()
            {
                // VideoView 必须从 WinForms 控件树移除后再 Dispose，
                // 否则主菜单出现后原生视频窗口可能还留在最上层挡住 XNA UI。
                loadingVideoHostControl?.Controls.Remove(loadingVideoView);
                loadingVideoView.Dispose();
                loadingVideoView = null;
                loadingVideoHostControl = null;
            }

            if (loadingVideoHostControl != null && loadingVideoHostControl.InvokeRequired)
                loadingVideoHostControl.Invoke((Action)RemoveView);
            else
                RemoveView();
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
