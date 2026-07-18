using ClientCore;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.Online;
using DTAConfig;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
// Updater removed
using System.Drawing.Design;
using System.Text;
using System.Globalization;
using System.Xml.Linq;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// 客户端的主菜单。
    /// </summary>
    class MainMenu : XNAWindow, ISwitchable
    {
        private const float MEDIA_PLAYER_VOLUME_FADE_STEP = 0.01f;
        private const float MEDIA_PLAYER_VOLUME_EXIT_FADE_STEP = 0.025f;


        /// <summary>
        /// 创建主菜单的新实例。
        /// </summary>
        public MainMenu(
            WindowManager windowManager,
            SkirmishLobby skirmishLobby,
            LANLobby lanLobby,
            TopBar topBar,
            OptionsWindow optionsWindow,
            CnCNetLobby cncnetLobby,
            CnCNetManager connectionManager,
            DiscordHandler discordHandler,
            CnCNetGameLoadingLobby cnCNetGameLoadingLobby,
            CnCNetGameLobby cnCNetGameLobby,
            PrivateMessagingPanel privateMessagingPanel,
            PrivateMessagingWindow privateMessagingWindow,
            GameInProgressWindow gameInProgressWindow
        ) : base(windowManager)
        {
            this.lanLobby = lanLobby;
            this.topBar = topBar;
            this.connectionManager = connectionManager;
            this.optionsWindow = optionsWindow;
            this.cncnetLobby = cncnetLobby;
            this.discordHandler = discordHandler;
            this.skirmishLobby = skirmishLobby;
            this.cnCNetGameLoadingLobby = cnCNetGameLoadingLobby;
            this.cnCNetGameLobby = cnCNetGameLobby;
            this.privateMessagingPanel = privateMessagingPanel;
            this.privateMessagingWindow = privateMessagingWindow;
            this.gameInProgressWindow = gameInProgressWindow;
            this.cncnetLobby.UpdateCheck += CncnetLobby_UpdateCheck;
            isMediaPlayerAvailable = IsMediaPlayerAvailable();
        }

        private MainMenuDarkeningPanel innerPanel;

        private XNALabel lblCnCNetPlayerCount;
        private XNALinkLabel lblVersion;
        /// <summary>
        /// 平台声明
        /// </summary>
        private XNALabel lblSoftState;

        private CnCNetLobby cncnetLobby;

        private SkirmishLobby skirmishLobby;

        private LANLobby lanLobby;

        private CnCNetManager connectionManager;

        private OptionsWindow optionsWindow;

        private DiscordHandler discordHandler;

        private TopBar topBar;
        private readonly CnCNetGameLoadingLobby cnCNetGameLoadingLobby;
        private readonly CnCNetGameLobby cnCNetGameLobby;
        private readonly PrivateMessagingPanel privateMessagingPanel;
        private readonly PrivateMessagingWindow privateMessagingWindow;
        private readonly GameInProgressWindow gameInProgressWindow;

        private XNAMessageBox firstRunMessageBox;

        private bool _updateInProgress;
        private bool UpdateInProgress
        {
            get { return _updateInProgress; }
            set
            {
                _updateInProgress = value;
                topBar.SetSwitchButtonsClickable(!_updateInProgress);
                topBar.SetOptionsButtonClickable(!_updateInProgress);
                SetButtonHotkeys(!_updateInProgress);
            }
        }

        private bool customComponentDialogQueued = false;


        private Song themeSong;

        private static readonly object locker = new object();

        private bool isMusicFading = false;

        private Texture2D[] animatedBackgroundFrames;
        private TimeSpan[] animatedBackgroundFrameDurations;
        private int animatedBackgroundFrameIndex;
        private TimeSpan animatedBackgroundFrameElapsed;

        /// <summary>
        /// 后台 GIF 预解码尚未完成时为 true。
        /// 此时背景用 PNG 兜底，Update() 每帧检查预加载器，完成后切换到 GIF 动画。
        /// </summary>
        private bool pendingAnimatedBackgroundSwap;

        private readonly bool isMediaPlayerAvailable;

        private CancellationTokenSource cncnetPlayerCountCancellationSource;

        // 主菜单按钮
        private XNAClientButton btnNewCampaign;
        private XNAClientButton btnLoadGame;
        private XNAClientButton btnSkirmish;
        private XNAClientButton btnCnCNet;
        private XNAClientButton btnLan;
        private XNAClientButton btnOptions;
        private XNAClientButton btnMapEditor;
        private XNAClientButton btnStatistics;
        private XNAClientButton btnCredits;
        private XNAClientButton btnExtras;

        /// <summary>
        /// 初始化主菜单的控件。
        /// </summary>
        public override void Initialize()
        {
            topBar.SetSecondarySwitch(cncnetLobby);
            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;

            Name = nameof(MainMenu);
            InitializeBackgroundTexture();
            ClientRectangle = new Rectangle(0, 0, BackgroundTexture.Width, BackgroundTexture.Height);

            WindowManager.CenterControlOnScreen(this);

            btnNewCampaign = new XNAClientButton(WindowManager);
            btnNewCampaign.Name = nameof(btnNewCampaign);
            btnNewCampaign.IdleTexture = AssetLoader.LoadTexture("MainMenu/campaign.png");
            btnNewCampaign.HoverTexture = AssetLoader.LoadTexture("MainMenu/campaign_c.png");
            btnNewCampaign.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnNewCampaign.LeftClick += BtnNewCampaign_LeftClick;
            btnNewCampaign.Text = "战役";

            btnLoadGame = new XNAClientButton(WindowManager);
            btnLoadGame.Name = nameof(btnLoadGame);
            btnLoadGame.IdleTexture = AssetLoader.LoadTexture("MainMenu/loadmission.png");
            btnLoadGame.HoverTexture = AssetLoader.LoadTexture("MainMenu/loadmission_c.png");
            btnLoadGame.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnLoadGame.LeftClick += BtnLoadGame_LeftClick;
            btnLoadGame.Text = "载入存档";

            btnSkirmish = new XNAClientButton(WindowManager);
            btnSkirmish.Name = nameof(btnSkirmish);
            btnSkirmish.IdleTexture = AssetLoader.LoadTexture("MainMenu/skirmish.png");
            btnSkirmish.HoverTexture = AssetLoader.LoadTexture("MainMenu/skirmish_c.png");
            btnSkirmish.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnSkirmish.LeftClick += BtnSkirmish_LeftClick;
            btnSkirmish.Text = "遭遇战";

            btnCnCNet = new XNAClientButton(WindowManager);
            btnCnCNet.Name = nameof(btnCnCNet);
            btnCnCNet.IdleTexture = AssetLoader.LoadTexture("MainMenu/cncnet.png");
            btnCnCNet.HoverTexture = AssetLoader.LoadTexture("MainMenu/cncnet_c.png");
            btnCnCNet.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnCnCNet.LeftClick += BtnCnCNet_LeftClick;
            btnCnCNet.Text = "联机大厅";

            btnLan = new XNAClientButton(WindowManager);
            btnLan.Name = nameof(btnLan);
            btnLan.IdleTexture = AssetLoader.LoadTexture("MainMenu/lan.png");
            btnLan.HoverTexture = AssetLoader.LoadTexture("MainMenu/lan_c.png");
            btnLan.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnLan.Text = "局域网大厅";
            btnLan.LeftClick += BtnLan_LeftClick;

            btnOptions = new XNAClientButton(WindowManager);
            btnOptions.Name = nameof(btnOptions);
            btnOptions.IdleTexture = AssetLoader.LoadTexture("MainMenu/options.png");
            btnOptions.HoverTexture = AssetLoader.LoadTexture("MainMenu/options_c.png");
            btnOptions.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnOptions.LeftClick += BtnOptions_LeftClick;
            btnOptions.Text = "设置";

            btnMapEditor = new XNAClientButton(WindowManager);
            btnMapEditor.Name = nameof(btnMapEditor);
            btnMapEditor.IdleTexture = AssetLoader.LoadTexture("MainMenu/mapeditor.png");
            btnMapEditor.HoverTexture = AssetLoader.LoadTexture("MainMenu/mapeditor_c.png");
            btnMapEditor.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnMapEditor.LeftClick += BtnMapEditor_LeftClick;
            btnMapEditor.Text = "地图编辑器";

            btnStatistics = new XNAClientButton(WindowManager);
            btnStatistics.Name = nameof(btnStatistics);
            btnStatistics.IdleTexture = AssetLoader.LoadTexture("MainMenu/statistics.png");
            btnStatistics.HoverTexture = AssetLoader.LoadTexture("MainMenu/statistics_c.png");
            btnStatistics.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnStatistics.LeftClick += BtnStatistics_LeftClick;
            btnStatistics.Text = "统计数据";

            btnCredits = new XNAClientButton(WindowManager);
            btnCredits.Name = nameof(btnCredits);
            btnCredits.IdleTexture = AssetLoader.LoadTexture("MainMenu/credits.png");
            btnCredits.HoverTexture = AssetLoader.LoadTexture("MainMenu/credits_c.png");
            btnCredits.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnCredits.LeftClick += BtnCredits_LeftClick;
            btnCredits.Text = "查看鸣谢";

            btnExtras = new XNAClientButton(WindowManager);
            btnExtras.Name = nameof(btnExtras);
            btnExtras.IdleTexture = AssetLoader.LoadTexture("MainMenu/extras.png");
            btnExtras.HoverTexture = AssetLoader.LoadTexture("MainMenu/extras_c.png");
            btnExtras.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnExtras.LeftClick += BtnExtras_LeftClick;

            var btnExit = new XNAClientButton(WindowManager);
            btnExit.Name = nameof(btnExit);
            btnExit.IdleTexture = AssetLoader.LoadTexture("MainMenu/exitgame.png");
            btnExit.HoverTexture = AssetLoader.LoadTexture("MainMenu/exitgame_c.png");
            btnExit.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnExit.LeftClick += BtnExit_LeftClick;
            btnExit.Text = "退出";

            XNALabel lblCnCNetStatus = new XNALabel(WindowManager);
            lblCnCNetStatus.Name = nameof(lblCnCNetStatus);
            lblCnCNetStatus.Text = "DTA玩家在CnCNet:";
            lblCnCNetStatus.ClientRectangle = new Rectangle(12, 9, 0, 0);

            lblCnCNetPlayerCount = new XNALabel(WindowManager);
            lblCnCNetPlayerCount.Name = nameof(lblCnCNetPlayerCount);
            lblCnCNetPlayerCount.Text = "-";

            lblVersion = new XNALinkLabel(WindowManager);
            lblVersion.Name = nameof(lblVersion);
            lblVersion.LeftClick += LblVersion_LeftClick;

            //软件声明
            lblSoftState = new XNALabel(WindowManager);
            lblSoftState.Name = nameof(lblSoftState);

            // 更新 UI 已移除

            AddChild(btnNewCampaign);
            AddChild(btnLoadGame);
            AddChild(btnSkirmish);
            AddChild(btnCnCNet);
            AddChild(btnLan);
            AddChild(btnOptions);
            AddChild(btnMapEditor);
            AddChild(btnStatistics);
            AddChild(btnCredits);
            AddChild(btnExtras);
            AddChild(btnExit);
            AddChild(lblCnCNetStatus);
            AddChild(lblCnCNetPlayerCount);
            AddChild(lblSoftState);

            if (!ClientConfiguration.Instance.ModMode)
            {
                // 更新器已移除：仅显示版本标签，无更新功能
                AddChild(lblVersion);
            }

            string FA2Path = ProgramConstants.GamePath + ClientConfiguration.Instance.MapEditorExePath;
            if (!File.Exists(FA2Path))
            {
                Logger.Log("未找到编辑器");
                btnMapEditor.Enabled = false;
            }
            else
            {
                IniFile ini = new IniFile(ProgramConstants.GamePath + ClientConfiguration.Instance.FinalSunIniPath, Encoding.GetEncoding("GBK"));
                ini.SetStringValue("TS", "Exe", (Encoding.GetEncoding("GBK").GetString(Encoding.Default.GetBytes(ProgramConstants.GamePath)) + "gamemd.exe").Replace('/', '\\')); //编辑器路径中/替换为\，因为写入INI时需要用反斜杠转义
                ini.WriteIniFile();
            }


            base.Initialize(); // 从 INI 读取控件属性
            lblSoftState.Text = "本平台及Mod均不收费";
            lblSoftState.ClientRectangle = new Rectangle(1000, 740, lblSoftState.Width, lblSoftState.Height);

            innerPanel = new MainMenuDarkeningPanel(WindowManager, discordHandler);
            innerPanel.ClientRectangle = new Rectangle(0, 0, Width, Height);
            innerPanel.DrawOrder = int.MaxValue;
            innerPanel.UpdateOrder = int.MaxValue;
            AddChild(innerPanel);
            innerPanel.Hide();

            lblVersion.Text = string.Empty;

            ClientRectangle = new Rectangle((WindowManager.RenderResolutionX - Width) / 2,
                (WindowManager.RenderResolutionY - Height) / 2,
                Width, Height);
            innerPanel.ClientRectangle = new Rectangle(0, 0,
                Math.Max(WindowManager.RenderResolutionX, Width),
                Math.Max(WindowManager.RenderResolutionY, Height));

            CnCNetPlayerCountTask.CnCNetGameCountUpdated += CnCNetInfoController_CnCNetGameCountUpdated;
            cncnetPlayerCountCancellationSource = new CancellationTokenSource();
            CnCNetPlayerCountTask.InitializeService(cncnetPlayerCountCancellationSource);

            WindowManager.GameClosing += WindowManager_GameClosing;

            skirmishLobby.Exited += SkirmishLobby_Exited;
            lanLobby.Exited += LanLobby_Exited;
            optionsWindow.EnabledChanged += OptionsWindow_EnabledChanged;


            GameProcessLogic.GameProcessStarted += SharedUILogic_GameProcessStarted;
            GameProcessLogic.GameProcessStarting += SharedUILogic_GameProcessStarting;

            UserINISettings.Instance.SettingsSaved += SettingsSaved;

            // 更新器已移除：重启处理已禁用

            SetButtonHotkeys(true);


        }

        private void SetButtonHotkeys(bool enableHotkeys)
        {
            if (!Initialized)
                return;

            if (enableHotkeys)
            {
                btnNewCampaign.HotKey = Keys.C;
                btnLoadGame.HotKey = Keys.L;
                btnSkirmish.HotKey = Keys.S;
                btnCnCNet.HotKey = Keys.M;
                btnLan.HotKey = Keys.N;
                btnOptions.HotKey = Keys.O;
                btnMapEditor.HotKey = Keys.E;
                btnStatistics.HotKey = Keys.T;
                btnCredits.HotKey = Keys.R;
                btnExtras.HotKey = Keys.X;
            }
            else
            {
                btnNewCampaign.HotKey = Keys.None;
                btnLoadGame.HotKey = Keys.None;
                btnSkirmish.HotKey = Keys.None;
                btnCnCNet.HotKey = Keys.None;
                btnLan.HotKey = Keys.None;
                btnOptions.HotKey = Keys.None;
                btnMapEditor.HotKey = Keys.None;
                btnStatistics.HotKey = Keys.None;
                btnCredits.HotKey = Keys.None;
                btnExtras.HotKey = Keys.None;
            }
        }

        private void OptionsWindow_EnabledChanged(object sender, EventArgs e)
        {
            if (!optionsWindow.Enabled)
            {
                // 更新器已移除：无自定义组件对话框
            }
        }

        /// <summary>
        /// 刷新设置。在游戏进程启动时调用。
        /// </summary>
        private void SharedUILogic_GameProcessStarting()
        {
            UserINISettings.Instance.ReloadSettings();

            try
            {
                optionsWindow.RefreshSettings();
            }
            catch (Exception ex)
            {
                Logger.Log("Refreshing settings failed! Exception message: " + ex.Message);
                // 我们不想在启动游戏时显示对话框
                //XNAMessageBox.Show(WindowManager, "Saving settings failed",
                //    "Saving settings failed! Error message: " + ex.Message);
            }
        }

        // 更新器重启处理已移除

        /// <summary>
        /// 保存设置时应用配置更改（音乐播放和音量）。
        /// </summary>
        private void SettingsSaved(object sender, EventArgs e)
        {
            if (isMediaPlayerAvailable)
            {
                if (MediaPlayer.State == MediaState.Playing)
                {
                    if (!UserINISettings.Instance.PlayMainMenuMusic)
                        isMusicFading = true;
                }
                else if (topBar.GetTopMostPrimarySwitchable() == this &&
                    topBar.LastSwitchType == SwitchType.PRIMARY)
                {
                    PlayMusic();
                }
            }

            if (!connectionManager.IsConnected)
                ProgramConstants.PLAYERNAME = UserINISettings.Instance.PlayerName;

            if (UserINISettings.Instance.DiscordIntegration)
                discordHandler.Connect();
            else
                discordHandler.Disconnect();
        }

        private void CheckMap()
        {

        }

        private void InitializeBackgroundTexture()
        {
            string backgroundPath = MainMenuBackgroundSelector.Select(BackgroundFileExists);

            if (Path.GetExtension(backgroundPath).Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                TryLoadAnimatedBackground(backgroundPath))
            {
                BackgroundTexture = animatedBackgroundFrames[0];
                return;
            }

            BackgroundTexture = AssetLoader.LoadTexture(MainMenuBackgroundSelector.PngBackgroundPath);
        }

        private bool TryLoadAnimatedBackground(string assetPath)
        {
            FileInfo backgroundFile = GetBackgroundFile(assetPath);

            if (!backgroundFile.Exists)
                return false;

            // 确保 LoadingScreen 阶段启动的后台预解码任务已经启动。
            // 如果已经启动过同一路径，EnsureStarted 是幂等的。
            MainMenuBackgroundPreloader.EnsureStarted(backgroundFile.FullName);

            // 如果后台解码已完成，立即把 RGBA 字节数组上传成 Texture2D（仅 ~0.3 秒 GPU 上传）。
            if (TryApplyPreloadedBackground())
                return true;

            // 后台仍在解码：先用 PNG 兜底，Update() 每帧轮询，解码完成后无缝切换到 GIF。
            pendingAnimatedBackgroundSwap = true;
            Logger.Log("MainMenu: GIF 后台解码尚未完成，先用 PNG 兜底，解码完成后切换。");
            return false;
        }

        /// <summary>
        /// 尝试把预加载器已完成的结果上传成 Texture2D 数组。
        /// 成功返回 true；如果预加载器未启动、未完成或失败，返回 false。
        /// </summary>
        private bool TryApplyPreloadedBackground()
        {
            if (!MainMenuBackgroundPreloader.TryGetResult(out PreloadedBackground preloaded))
                return false;

            if (preloaded == null)
            {
                // 预解码失败或已结束无结果，调用方走兜底
                return false;
            }

            try
            {
                GraphicsDevice device = WindowManager.Game.GraphicsDevice
                    ?? throw new InvalidOperationException("GraphicsDevice 不可用");

                var frames = new Texture2D[preloaded.Frames.Length];
                var durations = new TimeSpan[preloaded.Frames.Length];

                for (int i = 0; i < preloaded.Frames.Length; i++)
                {
                    frames[i] = new Texture2D(device, preloaded.Width, preloaded.Height, false, SurfaceFormat.Color);
                    frames[i].SetData(preloaded.Frames[i].RgbaPixels);
                    durations[i] = preloaded.Frames[i].Duration;
                }

                animatedBackgroundFrames = frames;
                animatedBackgroundFrameDurations = durations;
                animatedBackgroundFrameIndex = 0;
                animatedBackgroundFrameElapsed = TimeSpan.Zero;

                Logger.Log($"MainMenu: 已应用预解码 GIF 背景，{frames.Length} 帧。");
                return frames.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.Log("MainMenu: 应用预解码 GIF 背景失败! " + ex.Message);
                animatedBackgroundFrames = null;
                animatedBackgroundFrameDurations = null;
                return false;
            }
        }

        private static FileInfo GetBackgroundFile(string assetPath)
        {
            FileInfo themeFile = SafePath.GetFile(ProgramConstants.GetResourcePath(), assetPath);

            if (themeFile.Exists)
                return themeFile;

            return SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), assetPath);
        }

        private static bool BackgroundFileExists(string assetPath) => GetBackgroundFile(assetPath).Exists;

        /// <summary>
        /// 检查 Mod 运行所需但未随 Mod 分发的文件
        /// （通常是尤里的复仇 Mod 无法独立运行的基础游戏文件）。
        /// </summary>
        private void CheckRequiredFiles()
        {
            List<string> absentFiles = ClientConfiguration.Instance.RequiredFiles.ToList()
                .FindAll(f => !string.IsNullOrWhiteSpace(f) && !SafePath.GetFile(ProgramConstants.GamePath, f).Exists);

            if (absentFiles.Count > 0)
                XNAMessageBox.Show(WindowManager, "文件缺失",
                    ("您缺少运行此Mod所需的尤里的复仇文件!" + Environment.NewLine +
                    "尤里的复仇Mod不是独立的," + Environment.NewLine +
                    "您需要将以下尤里的复仇(v. 1.001)文件放置在Mod文件夹中才能运行:") +
                    Environment.NewLine + Environment.NewLine +
                    String.Join(Environment.NewLine, absentFiles) +
                    Environment.NewLine + Environment.NewLine +
                    "缺失这些文件您将无法进行游戏.");
        }

        private void CheckForbiddenFiles()
        {
            List<string> presentFiles = ClientConfiguration.Instance.ForbiddenFiles.ToList()
                .FindAll(f => !string.IsNullOrWhiteSpace(f) && SafePath.GetFile(ProgramConstants.GamePath, f).Exists);

            if (presentFiles.Count > 0)
                XNAMessageBox.Show(WindowManager, "检测到冲突文件",
                    "以下冲突文件:" +
                    Environment.NewLine + Environment.NewLine +
                    String.Join(Environment.NewLine, presentFiles) +
                    Environment.NewLine + Environment.NewLine +
                    "这些文件会导致Mod选择器失效。@@如要修改数据请修改rules_custom.ini与art_custom.ini."
                    );
        }

        /// <summary>
        /// 检查客户端是否首次运行。
        /// 如果是，则显示对话框询问用户是否要配置设置。
        /// </summary>
        private void CheckIfFirstRun()
        {
            if (UserINISettings.Instance.IsFirstRun)
            {
                UserINISettings.Instance.IsFirstRun.Value = false;
                UserINISettings.Instance.SaveSettings();

                firstRunMessageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "首次安装",
                    string.Format(("您刚刚安装了{0}." + Environment.NewLine +
                    "强烈建议您在游戏前进行一些必要的设置." +
                    Environment.NewLine + "您想立刻设置吗?"), ClientConfiguration.Instance.LocalGame).Replace("@", Environment.NewLine));
                firstRunMessageBox.YesClickedAction = FirstRunMessageBox_YesClicked;
                firstRunMessageBox.NoClickedAction = FirstRunMessageBox_NoClicked;
            }

            optionsWindow.PostInit();
        }

        private void FirstRunMessageBox_NoClicked(XNAMessageBox messageBox)
        {
            if (customComponentDialogQueued)
                customComponentDialogQueued = false; // 更新器已移除
        }

        private void FirstRunMessageBox_YesClicked(XNAMessageBox messageBox) => optionsWindow.Open();

        private void SharedUILogic_GameProcessStarted() => MusicOff();

        private void WindowManager_GameClosing(object sender, EventArgs e) => Clean();

        private void SkirmishLobby_Exited(object sender, EventArgs e)
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();
        }

        private void LanLobby_Exited(object sender, EventArgs e)
        {
            topBar.SetLanMode(false);

            if (UserINISettings.Instance.AutomaticCnCNetLogin)
                connectionManager.Connect();

            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();
        }

        private void CnCNetInfoController_CnCNetGameCountUpdated(object sender, PlayerCountEventArgs e)
        {
            lock (locker)
            {
                if (e.PlayerCount == -1)
                    lblCnCNetPlayerCount.Text = "不可用";
                else
                    lblCnCNetPlayerCount.Text = e.PlayerCount.ToString();
            }
        }

        /// <summary>
        /// 尝试在用户关闭游戏时"优雅地"清理客户端会话。
        /// </summary>
        private void Clean()
        {
            // 更新器已移除：无更新事件取消订阅
            if (cncnetPlayerCountCancellationSource != null) cncnetPlayerCountCancellationSource.Cancel();
            topBar.Clean();
            if (UpdateInProgress)
                UpdateInProgress = false;

            if (connectionManager.IsConnected)
                connectionManager.Disconnect();
        }

        /// <summary>
        /// 开始播放音乐，如果启用了自动更新则发起更新检查，
        /// 并检查客户端是否首次运行。
        /// 在所有内部客户端 UI 逻辑初始化完成后调用。
        /// </summary>
        public void PostInit()
        {
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, skirmishLobby);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, cnCNetGameLoadingLobby);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, cnCNetGameLobby);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, cncnetLobby);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, lanLobby);
            optionsWindow.SetTopBar(topBar);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, optionsWindow);
            WindowManager.AddAndInitializeControl(privateMessagingPanel);
            privateMessagingPanel.AddChild(privateMessagingWindow);
            topBar.SetTertiarySwitch(privateMessagingWindow);
            topBar.SetOptionsWindow(optionsWindow);
            WindowManager.AddAndInitializeControl(gameInProgressWindow);

            skirmishLobby.Disable();
            cncnetLobby.Disable();
            cnCNetGameLobby.Disable();
            cnCNetGameLoadingLobby.Disable();
            lanLobby.Disable();
            privateMessagingWindow.Disable();
            optionsWindow.Disable();

            WindowManager.AddAndInitializeControl(topBar);
            topBar.AddPrimarySwitchable(this);

            SwitchMainMenuMusicFormat();

            themeSong = AssetLoader.LoadSong(ClientConfiguration.Instance.MainMenuMusicName);

            PlayMusic();

            // 更新器已移除：更新检查已禁用
            CheckMap();
            CheckRequiredFiles();
            CheckForbiddenFiles();
            CheckIfFirstRun();
        }

        private void SwitchMainMenuMusicFormat()
        {
            FileInfo wmaMainMenuMusicFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.BASE_RESOURCE_PATH,
                FormattableString.Invariant($"{ClientConfiguration.Instance.MainMenuMusicName}.wma"));

            if (!wmaMainMenuMusicFile.Exists)
                return;

            FileInfo wmaBackupMainMenuMusicFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.BASE_RESOURCE_PATH,
                FormattableString.Invariant($"{ClientConfiguration.Instance.MainMenuMusicName}.bak"));

            if (!wmaBackupMainMenuMusicFile.Exists)
                wmaMainMenuMusicFile.CopyTo(wmaBackupMainMenuMusicFile.FullName);

            wmaBackupMainMenuMusicFile.CopyTo(wmaMainMenuMusicFile.FullName, true);
        }

        // 更新器功能已移除

        private void BtnOptions_LeftClick(object sender, EventArgs e)
            => optionsWindow.Open();

        private void BtnNewCampaign_LeftClick(object sender, EventArgs e)
        {
            innerPanel.Show(innerPanel.CampaignSelector);
            optionsWindow.tabControl.MakeSelectable(4);
        }

        private void BtnLoadGame_LeftClick(object sender, EventArgs e)
        => innerPanel.Show(innerPanel.GameLoadingWindow);

        private void BtnLan_LeftClick(object sender, EventArgs e)
        {
            lanLobby.Open();

            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();

            if (connectionManager.IsConnected)
                connectionManager.Disconnect();

            topBar.SetLanMode(true);
        }

        private void BtnCnCNet_LeftClick(object sender, EventArgs e) => topBar.SwitchToSecondary();

        private void BtnSkirmish_LeftClick(object sender, EventArgs e)
        {
            skirmishLobby.Open();
            optionsWindow.tabControl.MakeSelectable(4);
            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();
        }

        private void BtnMapEditor_LeftClick(object sender, EventArgs e) => LaunchMapEditor();

        private void BtnStatistics_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.StatisticsWindow);

        private void BtnCredits_LeftClick(object sender, EventArgs e)
        {
            ProcessLauncher.StartShellProcess(MainClientConstants.CREDITS_URL);
        }

        private void BtnExtras_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.ExtrasWindow);

        private void BtnExit_LeftClick(object sender, EventArgs e)
        {
            WindowManager.HideWindow();
            FadeMusicExit();
        }

        private void SharedUILogic_GameProcessExited() =>
            AddCallback(new Action(HandleGameProcessExited), null);

        private void HandleGameProcessExited()
        {
            innerPanel.GameLoadingWindow.ListSaves();
            innerPanel.Hide();

            // 如果菜单音乐被禁用，检查主菜单是否为顶部栏的
            // 最上层窗口，只有是时才播放音乐
            // LAN 模式下顶部栏被禁用，所以检测 LAN 游戏大厅
            // 我们通过检查顶部栏是否启用来判断
            if (!UserINISettings.Instance.StopMusicOnMenu ||
                (topBar.Enabled && topBar.LastSwitchType == SwitchType.PRIMARY &&
                topBar.GetTopMostPrimarySwitchable() == this))
                PlayMusic();
        }

        /// <summary>
        /// 切换到主菜单并执行更新检查。
        /// </summary>
        private void CncnetLobby_UpdateCheck(object sender, EventArgs e)
        {
            CheckForUpdates();
            topBar.SwitchToPrimary();
        }

        public override void Update(GameTime gameTime)
        {
            if (isMusicFading)
                FadeMusic(gameTime);

            UpdateAnimatedBackground(gameTime);

            base.Update(gameTime);
        }

        private void UpdateAnimatedBackground(GameTime gameTime)
        {
            // 后台 GIF 解码还没完成时，每帧轮询预加载器；完成后把 RGBA 字节上传成 Texture2D 并切换背景。
            if (pendingAnimatedBackgroundSwap)
            {
                if (TryApplyPreloadedBackground())
                {
                    pendingAnimatedBackgroundSwap = false;
                    BackgroundTexture = animatedBackgroundFrames[0];
                }
                else
                {
                    // 仍在解码，继续用 PNG 兜底
                    return;
                }
            }

            if (animatedBackgroundFrames == null || animatedBackgroundFrames.Length < 2)
                return;

            animatedBackgroundFrameElapsed += gameTime.ElapsedGameTime;

            while (animatedBackgroundFrameElapsed >= animatedBackgroundFrameDurations[animatedBackgroundFrameIndex])
            {
                animatedBackgroundFrameElapsed -= animatedBackgroundFrameDurations[animatedBackgroundFrameIndex];
                animatedBackgroundFrameIndex++;

                if (animatedBackgroundFrameIndex >= animatedBackgroundFrames.Length)
                    animatedBackgroundFrameIndex = 0;

                BackgroundTexture = animatedBackgroundFrames[animatedBackgroundFrameIndex];
            }
        }

        public override void Draw(GameTime gameTime)
        {
            lock (locker)
            {
                base.Draw(gameTime);
            }
        }

        /// <summary>
        /// 尝试开始播放菜单音乐。
        /// </summary>
        private void PlayMusic()
        {
            if (!isMediaPlayerAvailable)
                return; // SharpDX 在 Vista 上无法播放音乐

            if (themeSong != null && UserINISettings.Instance.PlayMainMenuMusic)
            {
                isMusicFading = false;
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Volume = (float)UserINISettings.Instance.ClientVolume;

                try
                {
                    MediaPlayer.Play(themeSong);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Log("Playing main menu music failed! " + ex.Message);
                }
            }
        }

        private void LblVersion_LeftClick(object sender, EventArgs e)
        {
            // 更新器已移除：点击版本号无操作
        }

        private void CheckForUpdates()
        {
            // 更新器已移除：更新检查已禁用
        }

        /// <summary>
        /// 降低菜单音乐的音量，如果音量低到听不见则停止播放。
        /// </summary>
        /// <param name="gameTime">提供时间值的快照。</param>
        private void FadeMusic(GameTime gameTime)
        {
            if (!isMediaPlayerAvailable || !isMusicFading || themeSong == null)
                return;

            // 在 1 秒内淡出
            float step = SoundPlayer.Volume * (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (MediaPlayer.Volume > step)
                MediaPlayer.Volume -= step;
            else
            {
                MediaPlayer.Stop();
                isMusicFading = false;
            }
        }

        /// <summary>
        /// 退出客户端。如果正在播放音乐则快速淡出。
        /// </summary>
        private void FadeMusicExit()
        {
            if (!isMediaPlayerAvailable || themeSong == null)
            {
                ExitClient();
                return;
            }

            float step = MEDIA_PLAYER_VOLUME_EXIT_FADE_STEP * (float)UserINISettings.Instance.ClientVolume;

            if (MediaPlayer.Volume > step)
            {
                MediaPlayer.Volume -= step;
                AddCallback(new Action(FadeMusicExit), null);
            }
            else
            {
                MediaPlayer.Stop();
                ExitClient();
            }
        }

        private void ExitClient()
        {
            Logger.Log("Exiting.");
            WindowManager.CloseGame();
            Thread.Sleep(1000);
            Environment.Exit(0);
        }

        public void SwitchOn()
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();

            // 更新检查已移除
        }

        public void SwitchOff()
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();
        }

        private void MusicOff()
        {
            try
            {
                if (isMediaPlayerAvailable &&
                    MediaPlayer.State == MediaState.Playing)
                {
                    isMusicFading = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Turning music off failed! Message: " + ex.Message);
            }
        }

        /// <summary>
        /// 检查媒体播放器当前是否可用。
        /// 在 Windows Vista 或其他没有适当媒体播放器组件的系统上不可用。
        /// </summary>
        /// <returns>如果媒体播放器可用则为 true，否则为 false。</returns>
        private bool IsMediaPlayerAvailable()
        {
            try
            {
                MediaState state = MediaPlayer.State;
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("Error encountered when checking media player availability. Error message: " + e.Message);
                return false;
            }
        }

        private void LaunchMapEditor()
        {
            OSVersion osVersion = ClientConfiguration.Instance.GetOperatingSystemVersion();
            Process mapEditorProcess = new Process();

            string strCmdText = string.Format("/c cd /d {0} && FinalAlert2SP.exe", ProgramConstants.GamePath + "FinalAlert2SP");

            mapEditorProcess.StartInfo.FileName = "cmd.exe";
            mapEditorProcess.StartInfo.Arguments = strCmdText;
            mapEditorProcess.StartInfo.UseShellExecute = false;   //是否使用操作系统shell启动
            mapEditorProcess.StartInfo.CreateNoWindow = true;   //是否在新窗口中启动该进程的值 (不显示程序窗口)
            mapEditorProcess.Start();
            mapEditorProcess.WaitForExit();  //等待程序执行完退出进程
            mapEditorProcess.Close();
        }

        public string GetSwitchName() => "首页";
    }
}
