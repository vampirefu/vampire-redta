using ClientCore;
using ClientCore.CnCNet5;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Generic;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using DTAClient.DXGUI.Multiplayer.GameLobby.CommandHandlers;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ClientCore.Enums;
using DTAConfig;
using SixLabors.ImageSharp;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    using UserChannelPair = Tuple<string, string>;
    using InvitationIndex = Dictionary<Tuple<string, string>, WeakReference>;

    internal class CnCNetLobby : XNAWindow, ISwitchable
    {
        public event EventHandler UpdateCheck;

        public CnCNetLobby(WindowManager windowManager, CnCNetManager connectionManager,
            CnCNetGameLobby gameLobby, CnCNetGameLoadingLobby gameLoadingLobby,
            TopBar topBar, PrivateMessagingWindow pmWindow, TunnelHandler tunnelHandler,
            GameCollection gameCollection, CnCNetUserData cncnetUserData,
            OptionsWindow optionsWindow)
            : base(windowManager)
        {
            this.connectionManager = connectionManager;
            this.gameLobby = gameLobby;
            this.gameLoadingLobby = gameLoadingLobby;
            this.tunnelHandler = tunnelHandler;
            this.topBar = topBar;
            this.pmWindow = pmWindow;
            this.gameCollection = gameCollection;
            this.cncnetUserData = cncnetUserData;
            this.optionsWindow = optionsWindow;

            ctcpCommandHandlers = new CommandHandlerBase[]
            {
                new StringCommandHandler(ProgramConstants.GAME_INVITE_CTCP_COMMAND, HandleGameInviteCommand),
                new NoParamCommandHandler(ProgramConstants.GAME_INVITATION_FAILED_CTCP_COMMAND, HandleGameInvitationFailedNotification)
            };

            topBar.LogoutEvent += LogoutEvent;
        }

        private CnCNetManager connectionManager;
        private CnCNetUserData cncnetUserData;
        private readonly OptionsWindow optionsWindow;

        private PlayerListBox lbPlayerList;
        private ChatListBox lbChatMessages;
        private GameListBox lbGameList;
        private GlobalContextMenu globalContextMenu;

        private XNAClientButton btnLogout;
        private XNAClientButton btnNewGame;
        private XNAClientButton btnJoinGame;

        private XNAChatTextBox tbChatInput;

        private XNALabel lblColor;
        private XNALabel lblCurrentChannel;
        private XNALabel lblOnline;
        private XNALabel lblOnlineCount;

        private XNAClientDropDown ddColor;
        private XNAClientDropDown ddCurrentChannel;

        private XNASuggestionTextBox tbGameSearch;

        private XNAClientStateButton<SortDirection> btnGameSortAlpha;

        private XNAClientToggleButton btnGameFilterOptions;

        private DarkeningPanel gameCreationPanel; 

        private Channel currentChatChannel;

        private GameCollection gameCollection;

        private Color cAdminNameColor;

        private Texture2D unknownGameIcon;
        private Texture2D adminGameIcon;

        private EnhancedSoundEffect sndGameCreated;
        private EnhancedSoundEffect sndGameInviteReceived;

        private IRCColor[] chatColors;

        private CnCNetGameLobby gameLobby;
        private CnCNetGameLoadingLobby gameLoadingLobby;

        private TunnelHandler tunnelHandler;

        private CnCNetLoginWindow loginWindow;

        private TopBar topBar;

        private PrivateMessagingWindow pmWindow;

        private PasswordRequestWindow passwordRequestWindow;

        private bool isInGameRoom = false;
        private bool updateDenied = false;

        private string localGameID;
        private CnCNetGame localGame;

        private List<string> followedGames = new List<string>();

        private bool isJoiningGame = false;
        private HostedCnCNetGame gameOfLastJoinAttempt;

        private CancellationTokenSource gameCheckCancellation;

        private CommandHandlerBase[] ctcpCommandHandlers;

        private InvitationIndex invitationIndex;

        private GameFiltersPanel panelGameFilters;

        private void GameList_ClientRectangleUpdated(object sender, EventArgs e)
        {
            panelGameFilters.ClientRectangle = lbGameList.ClientRectangle;
        }

        private void LogoutEvent(object sender, EventArgs e)
        {
            isJoiningGame = false;
        }

        public override void Initialize()
        {
            invitationIndex = new InvitationIndex();

            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX - 64,
                WindowManager.RenderResolutionY - 64);

            Name = nameof(CnCNetLobby);
            BackgroundTexture = AssetLoader.LoadTexture("cncnetlobbybg.png");
            localGameID = ClientConfiguration.Instance.LocalGame;
            localGame = gameCollection.GameList.Find(g => g.InternalName.ToUpper() == localGameID.ToUpper());

            btnNewGame = new XNAClientButton(WindowManager);
            btnNewGame.Name = nameof(btnNewGame);
            btnNewGame.ClientRectangle = new Rectangle(12, Height - 29, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnNewGame.Text = "新建游戏";
            btnNewGame.AllowClick = false;
            btnNewGame.LeftClick += BtnNewGame_LeftClick;

            btnJoinGame = new XNAClientButton(WindowManager);
            btnJoinGame.Name = nameof(btnJoinGame);
            btnJoinGame.ClientRectangle = new Rectangle(btnNewGame.Right + 12,
                btnNewGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnJoinGame.Text = "加入游戏";
            btnJoinGame.AllowClick = false;
            btnJoinGame.LeftClick += BtnJoinGame_LeftClick;

            btnLogout = new XNAClientButton(WindowManager);
            btnLogout.Name = nameof(btnLogout);
            btnLogout.ClientRectangle = new Rectangle(Width - 145, btnNewGame.Y,
                UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLogout.Text = "登出";
            btnLogout.LeftClick += BtnLogout_LeftClick;

            var gameListRectangle = new Rectangle(
                btnNewGame.X, 41,
                btnJoinGame.Right - btnNewGame.X, btnNewGame.Y - 47
            );

            panelGameFilters = new GameFiltersPanel(WindowManager);
            panelGameFilters.ClientRectangle = gameListRectangle;
            panelGameFilters.Disable();

            lbGameList = new GameListBox(WindowManager, localGameID, HostedGameMatches);
            lbGameList.Name = nameof(lbGameList);
            lbGameList.ClientRectangle = gameListRectangle;
            lbGameList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbGameList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbGameList.DoubleLeftClick += LbGameList_DoubleLeftClick;
            lbGameList.AllowMultiLineItems = false;
            lbGameList.ClientRectangleUpdated += GameList_ClientRectangleUpdated;

            lbPlayerList = new PlayerListBox(WindowManager, gameCollection);
            lbPlayerList.Name = nameof(lbPlayerList);
            lbPlayerList.ClientRectangle = new Rectangle(Width - 202,
                20, 190,
                btnLogout.Y - 26);
            lbPlayerList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbPlayerList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbPlayerList.LineHeight = 16;
            lbPlayerList.DoubleLeftClick += LbPlayerList_DoubleLeftClick;
            lbPlayerList.RightClick += LbPlayerList_RightClick;

            globalContextMenu = new GlobalContextMenu(WindowManager, connectionManager, cncnetUserData, pmWindow);
            globalContextMenu.JoinEvent += (sender, args) => JoinUser(args.IrcUser, connectionManager.MainChannel);

            lbChatMessages = new ChatListBox(WindowManager);
            lbChatMessages.Name = nameof(lbChatMessages);
            lbChatMessages.ClientRectangle = new Rectangle(lbGameList.Right + 12, lbGameList.Y,
                lbPlayerList.X - lbGameList.Right - 24, lbPlayerList.Height);
            lbChatMessages.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbChatMessages.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbChatMessages.LineHeight = 16;
            lbChatMessages.LeftClick += (sender, args) => lbGameList.SelectedIndex = -1;
            lbChatMessages.RightClick += LbChatMessages_RightClick;

            tbChatInput = new XNAChatTextBox(WindowManager);
            tbChatInput.Name = nameof(tbChatInput);
            tbChatInput.ClientRectangle = new Rectangle(lbChatMessages.X,
                btnNewGame.Y, lbChatMessages.Width,
                btnNewGame.Height);
            tbChatInput.Suggestion = "输入聊天消息...";
            tbChatInput.Enabled = false;
            tbChatInput.MaximumTextLength = 200;
            tbChatInput.EnterPressed += TbChatInput_EnterPressed;

            lblColor = new XNALabel(WindowManager);
            lblColor.Name = nameof(lblColor);
            lblColor.ClientRectangle = new Rectangle(lbChatMessages.X, 14, 0, 0);
            lblColor.FontIndex = 1;
            lblColor.Text = "您的颜色:";

            ddColor = new XNAClientDropDown(WindowManager);
            ddColor.Name = nameof(ddColor);
            ddColor.ClientRectangle = new Rectangle(lblColor.X + 95, 12,
                150, 21);

            chatColors = connectionManager.GetIRCColors();

            foreach (IRCColor color in connectionManager.GetIRCColors())
            {
                if (!color.Selectable)
                    continue;

                XNADropDownItem ddItem = new XNADropDownItem();
                ddItem.Text = color.Name;
                ddItem.TextColor = color.XnaColor;
                ddItem.Tag = color;

                ddColor.AddItem(ddItem);
            }

            int selectedColor = UserINISettings.Instance.ChatColor;

            ddColor.SelectedIndex = selectedColor >= ddColor.Items.Count || selectedColor < 0
                ? ClientConfiguration.Instance.DefaultPersonalChatColorIndex :
                selectedColor;
            SetChatColor();
            ddColor.SelectedIndexChanged += DdColor_SelectedIndexChanged;

            ddCurrentChannel = new XNAClientDropDown(WindowManager);
            ddCurrentChannel.Name = nameof(ddCurrentChannel);
            ddCurrentChannel.ClientRectangle = new Rectangle(
                lbChatMessages.Right - 200,
                ddColor.Y, 200, 21);
            ddCurrentChannel.SelectedIndexChanged += DdCurrentChannel_SelectedIndexChanged;
            ddCurrentChannel.AllowDropDown = false;

            lblCurrentChannel = new XNALabel(WindowManager);
            lblCurrentChannel.Name = nameof(lblCurrentChannel);
            lblCurrentChannel.ClientRectangle = new Rectangle(
                ddCurrentChannel.X - 150,
                ddCurrentChannel.Y + 2, 0, 0);
            lblCurrentChannel.FontIndex = 1;
            lblCurrentChannel.Text = "当前游戏频道:";

            lblOnline = new XNALabel(WindowManager);
            lblOnline.Name = nameof(lblOnline);
            lblOnline.ClientRectangle = new Rectangle(310, 14, 0, 0);
            lblOnline.Text = "在线人数:";
            lblOnline.FontIndex = 1;
            lblOnline.Disable();

            lblOnlineCount = new XNALabel(WindowManager);
            lblOnlineCount.Name = nameof(lblOnlineCount);
            lblOnlineCount.ClientRectangle = new Rectangle(lblOnline.X + 50, 14, 0, 0);
            lblOnlineCount.FontIndex = 1;
            lblOnlineCount.Disable();

            tbGameSearch = new XNASuggestionTextBox(WindowManager);
            tbGameSearch.Name = nameof(tbGameSearch);
            tbGameSearch.ClientRectangle = new Rectangle(lbGameList.X,
                12, lbGameList.Width - 62, 21);
            tbGameSearch.Suggestion = "按名称,地图,模式,玩家筛选...";
            tbGameSearch.MaximumTextLength = 64;
            tbGameSearch.InputReceived += TbGameSearch_InputReceived;
            tbGameSearch.Disable();

            btnGameSortAlpha = new XNAClientStateButton<SortDirection>(WindowManager, new Dictionary<SortDirection, Texture2D>()
            {
                { SortDirection.None , AssetLoader.LoadTexture("sortAlphaNone.png")},
                { SortDirection.Asc , AssetLoader.LoadTexture("sortAlphaAsc.png")},
                { SortDirection.Desc , AssetLoader.LoadTexture("sortAlphaDesc.png")},
            });
            btnGameSortAlpha.Name = nameof(btnGameSortAlpha);
            btnGameSortAlpha.ClientRectangle = new Rectangle(
                tbGameSearch.X + tbGameSearch.Width + 10, tbGameSearch.Y,
                21, 21
            );
            btnGameSortAlpha.LeftClick += BtnGameSortAlpha_LeftClick;
            btnGameSortAlpha.SetToolTipText("按名称排列");
            RefreshGameSortAlphaBtn();

            btnGameFilterOptions = new XNAClientToggleButton(WindowManager);
            btnGameFilterOptions.Name = nameof(btnGameFilterOptions);
            btnGameFilterOptions.ClientRectangle = new Rectangle(
                btnGameSortAlpha.X + btnGameSortAlpha.Width + 10, tbGameSearch.Y,
                21, 21
            );
            btnGameFilterOptions.CheckedTexture = AssetLoader.LoadTexture("filterActive.png");
            btnGameFilterOptions.UncheckedTexture = AssetLoader.LoadTexture("filterInactive.png");
            btnGameFilterOptions.LeftClick += BtnGameFilterOptions_LeftClick;
            btnGameFilterOptions.SetToolTipText("游戏筛选");
            RefreshGameFiltersBtn();

            InitializeGameList();

            AddChild(btnNewGame);
            AddChild(btnJoinGame);
            AddChild(btnLogout);
            AddChild(lbPlayerList);
            AddChild(lbChatMessages);
            AddChild(lbGameList);
            AddChild(panelGameFilters);
            AddChild(tbChatInput);
            AddChild(lblColor);
            AddChild(ddColor);
            AddChild(lblCurrentChannel);
            AddChild(ddCurrentChannel);
            AddChild(globalContextMenu);
            AddChild(lblOnline);
            AddChild(lblOnlineCount);
            AddChild(tbGameSearch);
            AddChild(btnGameSortAlpha);
            AddChild(btnGameFilterOptions);


            panelGameFilters.VisibleChanged += GameFiltersPanel_VisibleChanged;

            CnCNetPlayerCountTask.CnCNetGameCountUpdated += OnCnCNetGameCountUpdated;
            UpdateOnlineCount(CnCNetPlayerCountTask.PlayerCount);

            pmWindow.SetJoinUserAction(JoinUser);

            base.Initialize();

            WindowManager.CenterControlOnScreen(this);

            PostUIInit();
        }

        private void BtnGameSortAlpha_LeftClick(object sender, EventArgs e)
        {
            UserINISettings.Instance.SortState.Value = (int)btnGameSortAlpha.GetState();

            RefreshGameSortAlphaBtn();
            SortAndRefreshHostedGames();
            UserINISettings.Instance.SaveSettings();
        }

        private void SortAndRefreshHostedGames()
        {
            lbGameList.SortAndRefreshHostedGames();
        }

        private void BtnGameFilterOptions_LeftClick(object sender, EventArgs e)
        {
            if (panelGameFilters.Visible)
                panelGameFilters.Cancel();
            else
                panelGameFilters.Show();
        }

        private void RefreshGameSortAlphaBtn()
        {
            if (Enum.IsDefined(typeof(SortDirection), UserINISettings.Instance.SortState.Value))
                btnGameSortAlpha.SetState((SortDirection)UserINISettings.Instance.SortState.Value);
        }

        private void RefreshGameFiltersBtn()
        {
            btnGameFilterOptions.Checked = UserINISettings.Instance.IsGameFiltersApplied();
        }

        private void GameFiltersPanel_VisibleChanged(object sender, EventArgs e)
        {
            if (panelGameFilters.Visible)
                return;

            RefreshGameFiltersBtn();
            SortAndRefreshHostedGames();
        }

        private void TbGameSearch_InputReceived(object sender, EventArgs e)
        {
            SortAndRefreshHostedGames();
            lbGameList.ViewTop = 0;
        }


        private bool HostedGameMatches(GenericHostedGame hg)
        {
            // 好友列表优先于以下其他筛选条件
            if (UserINISettings.Instance.ShowFriendGamesOnly)
                return hg.Players.Any(p => cncnetUserData.IsFriend(p));

            if (UserINISettings.Instance.HideLockedGames.Value && hg.Locked)
                return false;

            if (UserINISettings.Instance.HideIncompatibleGames.Value && hg.Incompatible)
                return false;

            if (UserINISettings.Instance.HidePasswordedGames.Value && hg.Passworded)
                return false;

            if (hg.MaxPlayers > UserINISettings.Instance.MaxPlayerCount.Value)
                return false;

            return
                string.IsNullOrWhiteSpace(tbGameSearch?.Text) ||
                tbGameSearch.Text == tbGameSearch.Suggestion ||
                hg.RoomName.ToUpper().Contains(tbGameSearch.Text.ToUpper()) ||
                hg.GameMode.ToUpper().Equals(tbGameSearch.Text.ToUpper()) ||
                hg.Map.ToUpper().Contains(tbGameSearch.Text.ToUpper()) ||
                hg.Players.Any(pl => pl.ToUpper().Equals(tbGameSearch.Text.ToUpper()));
        }


        private void OnCnCNetGameCountUpdated(object sender, PlayerCountEventArgs e) => UpdateOnlineCount(e.PlayerCount);

        private void UpdateOnlineCount(int playerCount) => lblOnlineCount.Text = playerCount.ToString();

        private void InitializeGameList()
        {
            int i = 0;

            foreach (var game in gameCollection.GameList)
            {
                if (!game.Supported || string.IsNullOrEmpty(game.ChatChannel))
                {
                    i++;
                    continue;
                }

                var item = new XNADropDownItem();
                item.Text = game.UIName;
                item.Texture = game.Texture;

                ddCurrentChannel.AddItem(item);

                var chatChannel = connectionManager.FindChannel(game.ChatChannel);

                if (chatChannel == null)
                {
                    chatChannel = connectionManager.CreateChannel(game.UIName, game.ChatChannel,
                        true, true, "ra1-derp");
                    connectionManager.AddChannel(chatChannel);
                }

                item.Tag = chatChannel;

                if (!string.IsNullOrEmpty(game.GameBroadcastChannel))
                {
                    var gameBroadcastChannel = connectionManager.FindChannel(game.GameBroadcastChannel);

                    if (gameBroadcastChannel == null)
                    {
                        gameBroadcastChannel = connectionManager.CreateChannel(
                            string.Format("{0}广播频道", game.UIName),
                            game.GameBroadcastChannel, true, false, null);
                        connectionManager.AddChannel(gameBroadcastChannel);
                    }

                    gameBroadcastChannel.CTCPReceived += GameBroadcastChannel_CTCPReceived;
                    gameBroadcastChannel.UserLeft += GameBroadcastChannel_UserLeftOrQuit;
                    gameBroadcastChannel.UserQuitIRC += GameBroadcastChannel_UserLeftOrQuit;
                    gameBroadcastChannel.UserKicked += GameBroadcastChannel_UserLeftOrQuit;
                }

                if (game.InternalName.ToUpper() == localGameID.ToUpper())
                {
                    ddCurrentChannel.SelectedIndex = i;
                }

                i++;
            }

            if (connectionManager.MainChannel == null)
            {
                // 如果未找到频道，则将CnCNet频道设置为主频道
                ddCurrentChannel.SelectedIndex = ddCurrentChannel.Items.Count - 1;
            }
        }

        private void PostUIInit()
        {
            sndGameCreated = new EnhancedSoundEffect("gamecreated.wav");
            sndGameInviteReceived = new EnhancedSoundEffect("pm.wav");

            cAdminNameColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.AdminNameColor);

            var assembly = Assembly.GetAssembly(typeof(GameCollection));
            using Stream unknownIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
            using Stream cncnetIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncneticon.png");

            unknownGameIcon = AssetLoader.TextureFromImage(Image.Load(unknownIconStream));
            adminGameIcon = AssetLoader.TextureFromImage(Image.Load(cncnetIconStream));

            connectionManager.WelcomeMessageReceived += ConnectionManager_WelcomeMessageReceived;
            connectionManager.Disconnected += ConnectionManager_Disconnected;
            connectionManager.PrivateCTCPReceived += ConnectionManager_PrivateCTCPReceived;

            cncnetUserData.UserFriendToggled += RefreshPlayerList;
            cncnetUserData.UserIgnoreToggled += RefreshPlayerList;

            gameCreationPanel = new DarkeningPanel(WindowManager);
            AddChild(gameCreationPanel);

            GameCreationWindow gcw = new GameCreationWindow(WindowManager, tunnelHandler);
            gameCreationPanel.AddChild(gcw);
            gameCreationPanel.Tag = gcw;
            gcw.Cancelled += Gcw_Cancelled;
            gcw.GameCreated += Gcw_GameCreated;
            gcw.LoadedGameCreated += Gcw_LoadedGameCreated;

            gameCreationPanel.Hide();

            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, Renderer.GetSafeString(
                    string.Format("***客户端版本{0}***", Assembly.GetAssembly(typeof(CnCNetLobby)).GetName().Version),
                    lbChatMessages.FontIndex)));

            connectionManager.BannedFromChannel += ConnectionManager_BannedFromChannel;

            loginWindow = new CnCNetLoginWindow(WindowManager);
            loginWindow.Connect += LoginWindow_Connect;
            loginWindow.Cancelled += LoginWindow_Cancelled;

            var loginWindowPanel = new DarkeningPanel(WindowManager);
            loginWindowPanel.Alpha = 0.0f;

            AddChild(loginWindowPanel);
            loginWindowPanel.AddChild(loginWindow);
            loginWindow.Disable();

            passwordRequestWindow = new PasswordRequestWindow(WindowManager, pmWindow);
            passwordRequestWindow.PasswordEntered += PasswordRequestWindow_PasswordEntered;

            var passwordRequestWindowPanel = new DarkeningPanel(WindowManager);
            passwordRequestWindowPanel.Alpha = 0.0f;
            AddChild(passwordRequestWindowPanel);
            passwordRequestWindowPanel.AddChild(passwordRequestWindow);
            passwordRequestWindow.Disable();

            gameLobby.GameLeft += GameLobby_GameLeft;
            gameLoadingLobby.GameLeft += GameLoadingLobby_GameLeft;

            UserINISettings.Instance.SettingsSaved += Instance_SettingsSaved;

            GameProcessLogic.GameProcessStarted += SharedUILogic_GameProcessStarted;
            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;
        }

        /// <summary>
        /// 当IRC服务器通知本地用户已被禁止加入其尝试进入的频道时显示消息。
        /// </summary>
        private void ConnectionManager_BannedFromChannel(object sender, ChannelEventArgs e)
        {
            var game = lbGameList.HostedGames.Find(hg => ((HostedCnCNetGame)hg).ChannelName == e.ChannelName);

            if (game == null)
            {
                var chatChannel = connectionManager.FindChannel(e.ChannelName);
                chatChannel?.AddMessage(new ChatMessage(Color.White, string.Format(
                    "无法加入聊天频道{0}，您已被封禁！", chatChannel.UIName)));
                return;
            }

            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, string.Format(
                "无法加入游戏{0}，您已被房主封禁！", game.RoomName)));

            isJoiningGame = false;
            if (gameOfLastJoinAttempt != null)
            {
                if (gameOfLastJoinAttempt.IsLoadedGame)
                    gameLoadingLobby.Clear();
                else
                    gameLobby.Clear();
            }
        }

        private void SharedUILogic_GameProcessStarted()
        {
            connectionManager.SendCustomMessage(new QueuedMessage("AWAY " + (char)58 + "In-game",
                QueuedMessageType.SYSTEM_MESSAGE, 0));
        }

        private void SharedUILogic_GameProcessExited()
        {
            connectionManager.SendCustomMessage(new QueuedMessage("AWAY",
                QueuedMessageType.SYSTEM_MESSAGE, 0));
        }

        private void Instance_SettingsSaved(object sender, EventArgs e)
        {
            if (!connectionManager.IsConnected)
                return;

            foreach (CnCNetGame game in gameCollection.GameList)
            {
                if (!game.Supported)
                    continue;

                if (game.InternalName.ToUpper() == localGameID)
                    continue;

                if (followedGames.Contains(game.InternalName) &&
                    !UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper()))
                {
                    connectionManager.FindChannel(game.GameBroadcastChannel).Leave();
                    followedGames.Remove(game.InternalName);
                }
                else if (!followedGames.Contains(game.InternalName) &&
                    UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper()))
                {
                    connectionManager.FindChannel(game.GameBroadcastChannel).Join();
                    followedGames.Add(game.InternalName);
                }
            }
        }

        private void LbPlayerList_RightClick(object sender, EventArgs e)
        {
            lbPlayerList.SelectedIndex = lbPlayerList.HoveredIndex;

            if (lbPlayerList.SelectedIndex < 0 ||
                lbPlayerList.SelectedIndex >= lbPlayerList.Items.Count)
            {
                return;
            }

            var user = (ChannelUser)lbPlayerList.SelectedItem.Tag;

            globalContextMenu.Show(user, GetCursorPoint());
        }

        private void LbChatMessages_RightClick(object sender, EventArgs e)
        {
            var item = lbChatMessages.HoveredItem;
            var chatMessage = item?.Tag as ChatMessage;

            ShowPlayerMessageContextMenu(chatMessage);
        }

        private void ShowPlayerMessageContextMenu(ChatMessage chatMessage)
        {
            lbChatMessages.SelectedIndex = lbChatMessages.HoveredIndex;

            globalContextMenu.Show(chatMessage, GetCursorPoint());
        }

        /// <summary>
        /// 通过向玩家列表中的用户发送私信来启用私聊。
        /// </summary>
        private void LbPlayerList_DoubleLeftClick(object sender, EventArgs e)
        {
            if (lbPlayerList.SelectedItem == null)
                return;

            var channelUser = (ChannelUser)lbPlayerList.SelectedItem.Tag;

            pmWindow.InitPM(channelUser.IRCUser.Name);
        }

        /// <summary>
        /// 当用户在该对话框上点击连接后隐藏登录对话框。
        /// </summary>
        private void LoginWindow_Connect(object sender, EventArgs e)
        {
            connectionManager.Connect();
            loginWindow.Disable();

            SetLogOutButtonText();
        }

        /// <summary>
        /// 如果用户在登录对话框中取消连接CnCNet，则隐藏登录窗口和CnCNet大厅。
        /// </summary>
        private void LoginWindow_Cancelled(object sender, EventArgs e)
        {
            topBar.SwitchToPrimary();
            loginWindow.Disable();
        }

        private void GameLoadingLobby_GameLeft(object sender, EventArgs e)
        {
            topBar.SwitchToSecondary();
            isInGameRoom = false;
            SetLogOutButtonText();

            // 保持好友窗口更新，以便它可以禁用邀请选项
            pmWindow.ClearInviteChannelInfo();
        }

        private void GameLobby_GameLeft(object sender, EventArgs e)
        {
            topBar.SwitchToSecondary();
            isInGameRoom = false;
            SetLogOutButtonText();

            // 保持好友窗口更新，以便它可以禁用邀请选项
            pmWindow.ClearInviteChannelInfo();
        }

        private void SetLogOutButtonText()
        {
            if (isInGameRoom)
            {
                btnLogout.Text = "游戏大厅";
                return;
            }

            if (UserINISettings.Instance.PersistentMode)
            {
                btnLogout.Text = "首页";
                return;
            }

            btnLogout.Text = "登出";
        }

        private void BtnJoinGame_LeftClick(object sender, EventArgs e) => JoinSelectedGame();

        private void LbGameList_DoubleLeftClick(object sender, EventArgs e) => JoinSelectedGame();

        private void PasswordRequestWindow_PasswordEntered(object sender, PasswordEventArgs e) => _JoinGame(e.HostedGame, e.Password);

        private string GetJoinGameErrorBase()
        {
            if (isJoiningGame)
                return "无法加入游戏-游戏正在进行中.如果您觉得是出错了,请登出并重新进入.";

            if (ProgramConstants.IsInGame)
                return "无法在游戏运行时加入游戏.";

            return null;
        }
        /// <summary>
        /// 检查用户是否可以加入游戏。
        /// 如果可以则返回null，否则返回说明用户无法加入游戏原因的错误消息。
        /// </summary>
        /// <param name="gameIndex">游戏列表框中的游戏索引。</param>
        private string GetJoinGameErrorByIndex(int gameIndex)
        {
            if (gameIndex < 0 || gameIndex >= lbGameList.HostedGames.Count)
                return "无效的游戏索引";

            return GetJoinGameErrorBase();
        }

        /// <summary>
        /// 如果游戏不可加入则返回错误消息，否则返回null。
        /// </summary>
        /// <param name="hg">托管游戏。</param>
        /// <returns>错误消息或null。</returns>
        private string GetJoinGameError(HostedCnCNetGame hg)
        {
            if (hg.Game.InternalName.ToUpper() != localGameID.ToUpper())
                return string.Format("所选游戏是{0}!", gameCollection.GetGameNameFromInternalName(hg.Game.InternalName));

            if (hg.Locked)
                return "所选游戏已锁定!";

            if (hg.IsLoadedGame && !hg.Players.Contains(ProgramConstants.PLAYERNAME))
                return "您不在已储存游戏里!";

            return GetJoinGameErrorBase();
        }

        private void JoinSelectedGame()
        {
            var listedGame = (HostedCnCNetGame)lbGameList.SelectedItem?.Tag;
            if (listedGame == null)
                return;
            var hostedGameIndex = lbGameList.HostedGames.IndexOf(listedGame);
            JoinGameByIndex(hostedGameIndex, string.Empty);
        }

        private bool JoinGameByIndex(int gameIndex, string password)
        {
            string error = GetJoinGameErrorByIndex(gameIndex);
            if (!string.IsNullOrEmpty(error))
            {
                connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, error));
                return false;
            }

            return JoinGame((HostedCnCNetGame)lbGameList.HostedGames[gameIndex], password, connectionManager.MainChannel);
        }

        /// <summary>
        /// 尝试加入游戏。
        /// </summary>
        /// <param name="hg">要加入的游戏。</param>
        /// <param name="password">用于加入的密码。</param>
        /// <param name="messageView">用于写入错误消息的消息视图/列表。</param>
        /// <returns>是否成功。</returns>
        private bool JoinGame(HostedCnCNetGame hg, string password, IMessageView messageView)
        {
            string error = GetJoinGameError(hg);
            if (!string.IsNullOrEmpty(error))
            {
                messageView.AddMessage(new ChatMessage(Color.White, error));
                return false;
            }

            if (isInGameRoom)
            {
                topBar.SwitchToPrimary();
                return false;
            }

            // if (hg.GameVersion != ProgramConstants.GAME_VERSION)
            // TODO 显示警告

            if (hg.Passworded)
            {
                // 仅在我们没有提供密码（邀请）时才显示密码对话框
                if (string.IsNullOrEmpty(password))
                {
                    passwordRequestWindow.SetHostedGame(hg);
                    passwordRequestWindow.Enable();
                    return true;
                }
            }
            else
            {
                if (!hg.IsLoadedGame)
                {
                    password = Utilities.CalculateSHA1ForString
                        (hg.ChannelName + hg.RoomName).Substring(0, 10);
                }
                else
                {
                    IniFile spawnSGIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "Saved Games", "spawnSG.ini"));
                    password = Utilities.CalculateSHA1ForString(
                        spawnSGIni.GetStringValue("Settings", "GameID", string.Empty)).Substring(0, 10);
                }
            }

            _JoinGame(hg, password);

            return true;
        }

        private void _JoinGame(HostedCnCNetGame hg, string password)
        {
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White,
                string.Format("尝试加入游戏{0}...", hg.RoomName)));
            isJoiningGame = true;
            gameOfLastJoinAttempt = hg;

            Channel gameChannel = connectionManager.CreateChannel(hg.RoomName, hg.ChannelName, false, true, password);
            connectionManager.AddChannel(gameChannel);

            if (hg.IsLoadedGame)
            {
                gameLoadingLobby.SetUp(false, hg.TunnelServer, gameChannel, hg.HostName);
                gameChannel.UserAdded += GameLoadingChannel_UserAdded;
                //gameChannel.MessageAdded += GameLoadingChannel_MessageAdded;
                gameChannel.InvalidPasswordEntered += GameChannel_InvalidPasswordEntered_LoadedGame;
            }
            else
            {
                gameLobby.SetUp(gameChannel, false, hg.MaxPlayers, hg.TunnelServer, hg.HostName, hg.Passworded);
                gameChannel.UserAdded += GameChannel_UserAdded;
                gameChannel.InvalidPasswordEntered += GameChannel_InvalidPasswordEntered_NewGame;
                gameChannel.InviteOnlyErrorOnJoin += GameChannel_InviteOnlyErrorOnJoin;
                gameChannel.ChannelFull += GameChannel_ChannelFull;
                gameChannel.TargetChangeTooFast += GameChannel_TargetChangeTooFast;
            }

            connectionManager.SendCustomMessage(new QueuedMessage("JOIN " + hg.ChannelName + " " + password,
                QueuedMessageType.INSTANT_MESSAGE, 0));
        }

        private void GameChannel_TargetChangeTooFast(object sender, MessageEventArgs e)
        {
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, e.Message));
            ClearGameJoinAttempt((Channel)sender);
        }

        private void GameChannel_ChannelFull(object sender, EventArgs e) =>
            // 我们在这里会做完全相同的事情，所以可以直接调用下面的方法
            GameChannel_InviteOnlyErrorOnJoin(sender, e);

        private void GameChannel_InviteOnlyErrorOnJoin(object sender, EventArgs e)
        {
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, "所选游戏已锁定!"));
            var channel = (Channel)sender;

            var game = FindGameByChannelName(channel.ChannelName);
            if (game != null)
            {
                game.Locked = true;
                SortAndRefreshHostedGames();
            }

            ClearGameJoinAttempt((Channel)sender);
        }

        private HostedCnCNetGame FindGameByChannelName(string channelName)
        {
            var game = lbGameList.HostedGames.Find(hg => ((HostedCnCNetGame)hg).ChannelName == channelName);
            if (game == null)
                return null;

            return (HostedCnCNetGame)game;
        }

        private void GameChannel_InvalidPasswordEntered_NewGame(object sender, EventArgs e)
        {
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, "密码不正确!"));
            ClearGameJoinAttempt((Channel)sender);
        }

        private void GameChannel_UserAdded(object sender, Online.ChannelUserEventArgs e)
        {
            Channel gameChannel = (Channel)sender;

            if (e.User.IRCUser.Name == ProgramConstants.PLAYERNAME)
            {
                ClearGameChannelEvents(gameChannel);
                gameLobby.OnJoined();
                isInGameRoom = true;
                SetLogOutButtonText();
            }
        }

        private void ClearGameJoinAttempt(Channel channel)
        {
            ClearGameChannelEvents(channel);
            gameLobby.Clear();
        }

        private void ClearGameChannelEvents(Channel channel)
        {
            channel.UserAdded -= GameChannel_UserAdded;
            channel.InvalidPasswordEntered -= GameChannel_InvalidPasswordEntered_NewGame;
            channel.InviteOnlyErrorOnJoin -= GameChannel_InviteOnlyErrorOnJoin;
            channel.ChannelFull -= GameChannel_ChannelFull;
            channel.TargetChangeTooFast -= GameChannel_TargetChangeTooFast;
            isJoiningGame = false;
        }

        private void BtnNewGame_LeftClick(object sender, EventArgs e)
        {
            if (isInGameRoom)
            {
                topBar.SwitchToPrimary();
                return;
            }

            gameCreationPanel.Show();
            var gcw = (GameCreationWindow)gameCreationPanel.Tag;

            gcw.Refresh();
        }

        private void Gcw_GameCreated(object sender, GameCreationEventArgs e)
        {
            if (gameLobby.Enabled || gameLoadingLobby.Enabled)
                return;

            string channelName = RandomizeChannelName();
            string password = e.Password;
            bool isCustomPassword = true;
            if (string.IsNullOrEmpty(password))
            {
                password = Rampastring.Tools.Utilities.CalculateSHA1ForString(
                    channelName + e.GameRoomName).Substring(0, 10);
                isCustomPassword = false;
            }

            Channel gameChannel = connectionManager.CreateChannel(e.GameRoomName, channelName, false, true, password);
            connectionManager.AddChannel(gameChannel);
            gameLobby.SetUp(gameChannel, true, e.MaxPlayers, e.Tunnel, ProgramConstants.PLAYERNAME, isCustomPassword);
            gameChannel.UserAdded += GameChannel_UserAdded;
            //gameChannel.MessageAdded += GameChannel_MessageAdded;
            connectionManager.SendCustomMessage(new QueuedMessage("JOIN " + channelName + " " + password,
                QueuedMessageType.INSTANT_MESSAGE, 0));
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White,
               string.Format("正在创建游戏{0}...", e.GameRoomName)));

            gameCreationPanel.Hide();

            // 更新好友窗口，以便它可以启用邀请选项
            pmWindow.SetInviteChannelInfo(channelName, e.GameRoomName, string.IsNullOrEmpty(e.Password) ? string.Empty : e.Password);
        }

        private void Gcw_LoadedGameCreated(object sender, GameCreationEventArgs e)
        {
            if (gameLobby.Enabled || gameLoadingLobby.Enabled)
                return;

            string channelName = RandomizeChannelName();

            Channel gameLoadingChannel = connectionManager.CreateChannel(e.GameRoomName, channelName, false, true, e.Password);
            connectionManager.AddChannel(gameLoadingChannel);
            gameLoadingLobby.SetUp(true, e.Tunnel, gameLoadingChannel, ProgramConstants.PLAYERNAME);
            gameLoadingChannel.UserAdded += GameLoadingChannel_UserAdded;
            connectionManager.SendCustomMessage(new QueuedMessage("JOIN " + channelName + " " + e.Password,
                QueuedMessageType.INSTANT_MESSAGE, 0));
            connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White,
               string.Format("正在创建游戏{0}...", e.GameRoomName)));

            gameCreationPanel.Hide();

            // 更新好友窗口，以便它可以启用邀请选项
            pmWindow.SetInviteChannelInfo(channelName, e.GameRoomName, string.IsNullOrEmpty(e.Password) ? string.Empty : e.Password);
        }

        private void GameChannel_InvalidPasswordEntered_LoadedGame(object sender, EventArgs e)
        {
            var channel = (Channel)sender;
            channel.UserAdded -= GameLoadingChannel_UserAdded;
            channel.InvalidPasswordEntered -= GameChannel_InvalidPasswordEntered_LoadedGame;
            gameLoadingLobby.Clear();
            isJoiningGame = false;
        }

        private void GameLoadingChannel_UserAdded(object sender, ChannelUserEventArgs e)
        {
            Channel gameLoadingChannel = (Channel)sender;

            if (e.User.IRCUser.Name == ProgramConstants.PLAYERNAME)
            {
                gameLoadingChannel.UserAdded -= GameLoadingChannel_UserAdded;
                gameLoadingChannel.InvalidPasswordEntered -= GameChannel_InvalidPasswordEntered_LoadedGame;

                gameLoadingLobby.OnJoined();
                isInGameRoom = true;
                isJoiningGame = false;
            }
        }

        /// <summary>
        /// 生成并返回一个随机的、未使用的频道名称。
        /// </summary>
        /// <returns>基于当前正在玩的游戏的随机频道名称。</returns>
        private string RandomizeChannelName()
        {
            while (true)
            {
                string channelName = string.Format("{0}-游戏{1}", gameCollection.GetGameChatChannelNameFromIdentifier(localGameID), new Random().Next(1000000, 9999999));
                int index = lbGameList.HostedGames.FindIndex(c => ((HostedCnCNetGame)c).ChannelName == channelName);
                if (index == -1)
                    return channelName;
            }
        }

        private void Gcw_Cancelled(object sender, EventArgs e) => gameCreationPanel.Hide();

        private void TbChatInput_EnterPressed(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbChatInput.Text))
                return;

            IRCColor selectedColor = (IRCColor)ddColor.SelectedItem.Tag;

            currentChatChannel.SendChatMessage(tbChatInput.Text, selectedColor);

            tbChatInput.Text = string.Empty;
        }

        private void SetChatColor()
        {
            IRCColor selectedColor = (IRCColor)ddColor.SelectedItem.Tag;
            tbChatInput.TextColor = selectedColor.XnaColor;
            gameLobby.ChangeChatColor(selectedColor);
            gameLoadingLobby.ChangeChatColor(selectedColor);
            UserINISettings.Instance.ChatColor.Value = ddColor.SelectedIndex;
        }

        private void DdColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetChatColor();
            UserINISettings.Instance.SaveSettings();
        }

        private void ConnectionManager_Disconnected(object sender, EventArgs e)
        {
            btnNewGame.AllowClick = false;
            btnJoinGame.AllowClick = false;
            ddCurrentChannel.AllowDropDown = false;
            tbChatInput.Enabled = false;
            lbPlayerList.Clear();

            lbGameList.ClearGames();
            followedGames.Clear();

            gameCreationPanel.Hide();

            // 切换到默认频道
            if (localGame != null)
            {
                int gameIndex = ddCurrentChannel.Items.FindIndex(i => i.Text == localGame.UIName);
                if (gameIndex > -1)
                    ddCurrentChannel.SelectedIndex = gameIndex;
            }

            if (gameCheckCancellation != null)
                gameCheckCancellation.Cancel();
        }

        private void ConnectionManager_WelcomeMessageReceived(object sender, EventArgs e)
        {
            btnNewGame.AllowClick = true;
            btnJoinGame.AllowClick = true;
            ddCurrentChannel.AllowDropDown = true;
            tbChatInput.Enabled = true;

            Channel cncnetChannel = connectionManager.FindChannel("#cncnet");
            cncnetChannel.Join();

            string localGameChatChannelName = gameCollection.GetGameChatChannelNameFromIdentifier(localGameID);
            connectionManager.FindChannel(localGameChatChannelName).Join();

            string localGameBroadcastChannel = gameCollection.GetGameBroadcastingChannelNameFromIdentifier(localGameID);
            connectionManager.FindChannel(localGameBroadcastChannel).Join();

            foreach (CnCNetGame game in gameCollection.GameList)
            {
                if (!game.Supported)
                    continue;

                if (game.InternalName.ToUpper() != localGameID)
                {
                    if (UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper()))
                    {
                        connectionManager.FindChannel(game.GameBroadcastChannel).Join();
                        followedGames.Add(game.InternalName);
                    }
                }
            }

            gameCheckCancellation = new CancellationTokenSource();
            CnCNetGameCheck gameCheck = new CnCNetGameCheck();
            gameCheck.InitializeService(gameCheckCancellation);
        }

        private void ConnectionManager_PrivateCTCPReceived(object sender, PrivateCTCPEventArgs e)
        {
            foreach (CommandHandlerBase cmdHandler in ctcpCommandHandlers)
            {
                if (cmdHandler.Handle(e.Sender, e.Message))
                    return;
            }

            Logger.Log("Unhandled private CTCP command: " + e.Message + " from " + e.Sender);
        }

        private void HandleGameInviteCommand(string sender, string argumentsString)
        {
            // 参数以分号分隔
            var arguments = argumentsString.Split(';');

            // 我们期望收到一个频道名称、一个（人类可读的）游戏名称，以及可选的密码
            if (arguments.Length < 2 || arguments.Length > 3)
                return;

            string channelName = arguments[0];
            string gameName = arguments[1];
            string password = (arguments.Length == 3) ? arguments[2] : string.Empty;

            if (!CanReceiveInvitationMessagesFrom(sender))
                return;

            var gameIndex = lbGameList.HostedGames.FindIndex(hg => ((HostedCnCNetGame)hg).ChannelName == channelName);

            // 同时强制执行用户关于是否接受非好友邀请的偏好设置
            // 这与CanReceiveInvitationMessagesFrom()分开处理，因为我们仍
            // 希望让主持知道我们无法收到邀请
            if (!string.IsNullOrEmpty(GetJoinGameErrorByIndex(gameIndex)) ||
                (UserINISettings.Instance.AllowGameInvitesFromFriendsOnly &&
                !cncnetUserData.IsFriend(sender)))
            {
                // 让主持知道我们无法接受
                // 注意：拒绝情况下不会到达这里
                connectionManager.SendCustomMessage(new QueuedMessage("PRIVMSG " + sender + " :\u0001" +
                    ProgramConstants.GAME_INVITATION_FAILED_CTCP_COMMAND + "\u0001",
                    QueuedMessageType.CHAT_MESSAGE, 0));

                return;
            }

            // 如果此用户/频道组合已有一个未处理的邀请，
            // 我们不想再显示另一个
            // 我们不会通知主持，因为他们的旧邀请对我们
            // 仍然可用
            var invitationIdentity = new UserChannelPair(sender, channelName);

            if (invitationIndex.ContainsKey(invitationIdentity))
            {
                return;
            }

            var gameInviteChoiceBox = new ChoiceNotificationBox(WindowManager);

            WindowManager.AddAndInitializeControl(gameInviteChoiceBox);

            // 在左上角显示邀请；它将一直保留直到被操作或目标游戏关闭
            gameInviteChoiceBox.Show(
                "游戏邀请",
                GetUserTexture(sender),
                sender,
                string.Format("加入{0}?", gameName),
                "确定", "取消", 0);

            // 将邀请添加到索引中，以便在目标游戏关闭时可以移除
            // 也允许我们在该邀请仍然未处理时静默忽略来自同一人的新邀请
            invitationIndex[invitationIdentity] =
                new WeakReference(gameInviteChoiceBox);

            gameInviteChoiceBox.AffirmativeClickedAction = delegate (ChoiceNotificationBox choiceBox)
            {
                // 如果我们当前在游戏大厅中，首先离开该频道
                if (isInGameRoom)
                {
                    gameLobby.LeaveGameLobby();
                }

                // JoinGameByIndex会进行边界检查，所以如果游戏不存在传入-1也是安全的
                if (!JoinGameByIndex(lbGameList.HostedGames.FindIndex(hg => ((HostedCnCNetGame)hg).ChannelName == channelName), password))
                {
                    XNAMessageBox.Show(WindowManager,
                        "加入失败",
                        string.Format("无法加入{0}的游戏.游戏已锁定或关闭.", sender));
                }

                // 清理索引，因为此邀请不再存在
                invitationIndex.Remove(invitationIdentity);
            };

            gameInviteChoiceBox.NegativeClickedAction = delegate (ChoiceNotificationBox choiceBox)
            {
                // 清理索引，因为此邀请不再存在
                invitationIndex.Remove(invitationIdentity);
            };

            sndGameInviteReceived.Play();
        }

        private void HandleGameInvitationFailedNotification(string sender)
        {
            if (!CanReceiveInvitationMessagesFrom(sender))
                return;

            if (isInGameRoom && !ProgramConstants.IsInGame)
            {
                gameLobby.AddWarning(
                    string.Format("{0}无法接收您的游戏邀请.其可能已在游戏中或者仅接收好友邀请.请确保您的游戏未锁定并在大厅可见然后再重试.", sender));
            }
        }

        private void DdCurrentChannel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (currentChatChannel != null)
            {
                currentChatChannel.UserAdded -= RefreshPlayerList;
                currentChatChannel.UserLeft -= RefreshPlayerList;
                currentChatChannel.UserQuitIRC -= RefreshPlayerList;
                currentChatChannel.UserKicked -= RefreshPlayerList;
                currentChatChannel.UserListReceived -= RefreshPlayerList;
                currentChatChannel.MessageAdded -= CurrentChatChannel_MessageAdded;
                currentChatChannel.UserGameIndexUpdated -= CurrentChatChannel_UserGameIndexUpdated;

                if (currentChatChannel.ChannelName != "#cncnet" &&
                    currentChatChannel.ChannelName != gameCollection.GetGameChatChannelNameFromIdentifier(localGameID))
                {
                    // 移除分配给用户的频道，以免在私聊用户列表中出现幽灵用户
                    currentChatChannel.Users.DoForAllUsers(user =>
                    {
                        connectionManager.RemoveChannelFromUser(user.IRCUser.Name, currentChatChannel.ChannelName);
                    });

                    currentChatChannel.Leave();
                }
            }

            currentChatChannel = (Channel)ddCurrentChannel.SelectedItem.Tag;
            currentChatChannel.UserAdded += RefreshPlayerList;
            currentChatChannel.UserLeft += RefreshPlayerList;
            currentChatChannel.UserQuitIRC += RefreshPlayerList;
            currentChatChannel.UserKicked += RefreshPlayerList;
            currentChatChannel.UserListReceived += RefreshPlayerList;
            currentChatChannel.MessageAdded += CurrentChatChannel_MessageAdded;
            currentChatChannel.UserGameIndexUpdated += CurrentChatChannel_UserGameIndexUpdated;
            connectionManager.SetMainChannel(currentChatChannel);

            lbPlayerList.TopIndex = 0;

            lbChatMessages.TopIndex = 0;
            lbChatMessages.Clear();
            currentChatChannel.Messages.ForEach(msg => AddMessageToChat(msg));

            RefreshPlayerList(this, EventArgs.Empty);

            if (currentChatChannel.ChannelName != "#cncnet" &&
                currentChatChannel.ChannelName != gameCollection.GetGameChatChannelNameFromIdentifier(localGameID))
            {
                currentChatChannel.Join();
            }
        }

        private void RefreshPlayerList(object sender, EventArgs e)
        {
            string selectedUserName = lbPlayerList.SelectedItem == null ?
                string.Empty : lbPlayerList.SelectedItem.Text;

            lbPlayerList.Clear();

            var current = currentChatChannel.Users.GetFirst();
            while (current != null)
            {
                var user = current.Value;
                user.IRCUser.IsFriend = cncnetUserData.IsFriend(user.IRCUser.Name);
                user.IRCUser.IsIgnored = cncnetUserData.IsIgnored(user.IRCUser.Ident);
                lbPlayerList.AddUser(user);
                current = current.Next;
            }

            if (selectedUserName != string.Empty)
            {
                lbPlayerList.SelectedIndex = lbPlayerList.Items.FindIndex(
                    i => i.Text == selectedUserName);
            }
        }

        /// <summary>
        /// 刷新玩家列表中单个用户的信息。
        /// </summary>
        /// <param name="user">当前聊天频道上的用户。</param>
        private void RefreshPlayerListUser(ChannelUser user)
        {
            user.IRCUser.IsFriend = cncnetUserData.IsFriend(user.IRCUser.Name);
            user.IRCUser.IsIgnored = cncnetUserData.IsIgnored(user.IRCUser.Ident);
            lbPlayerList.UpdateUserInfo(user);
        }

        private void CurrentChatChannel_UserGameIndexUpdated(object sender, ChannelUserEventArgs e)
        {
            var ircUser = e.User.IRCUser;
            var item = lbPlayerList.Items.Find(i => i.Text.StartsWith(ircUser.Name));

            if (ircUser.GameID < 0 || ircUser.GameID >= gameCollection.GameList.Count)
                item.Texture = unknownGameIcon;
            else
                item.Texture = gameCollection.GameList[ircUser.GameID].Texture;
        }

        private void AddMessageToChat(ChatMessage message)
        {
            if (!string.IsNullOrEmpty(message.SenderIdent) &&
                cncnetUserData.IsIgnored(message.SenderIdent) &&
                !message.SenderIsAdmin)
            {
                lbChatMessages.AddMessage(new ChatMessage(Color.Silver, string.Format("消息被屏蔽-{0}", message.SenderName)));
            }
            else
            {
                lbChatMessages.AddMessage(message);
            }
        }

        private void CurrentChatChannel_MessageAdded(object sender, IRCMessageEventArgs e) =>
            AddMessageToChat(e.Message);

        /// <summary>
        /// 当主持退出CnCNet或离开游戏广播频道时，从列表中移除游戏。
        /// </summary>
        private void GameBroadcastChannel_UserLeftOrQuit(object sender, UserNameEventArgs e)
        {
            int gameIndex = lbGameList.HostedGames.FindIndex(hg => hg.HostName == e.UserName);

            if (gameIndex > -1)
            {
                lbGameList.RemoveGame(gameIndex);

                // 关闭任何不再有效的未处理邀请
                DismissInvalidInvitations();
            }
        }

        private void GameBroadcastChannel_CTCPReceived(object sender, ChannelCTCPEventArgs e)
        {
            var channel = (Channel)sender;

            var channelUser = channel.Users.Find(e.UserName);

            if (channelUser == null)
                return;

            if (localGame != null &&
                channel.ChannelName == localGame.GameBroadcastChannel &&
                !updateDenied &&
                channelUser.IsAdmin &&
                !isInGameRoom &&
                e.Message.StartsWith("UPDATE ") &&
                e.Message.Length > 7)
            {
                string version = e.Message.Substring(7);
                if (version != ProgramConstants.GAME_VERSION)
                {
                    var updateMessageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "更新可用",
                        "有更新可用.您想现在更新吗?");
                    updateMessageBox.NoClickedAction = UpdateMessageBox_NoClicked;
                    updateMessageBox.YesClickedAction = UpdateMessageBox_YesClicked;
                }
            }

            if (!e.Message.StartsWith("GAME "))
                return;

            string msg = e.Message.Substring(5); // 截取GAME之后的部分
            string[] splitMessage = msg.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (splitMessage.Length != 11)
            {
                Logger.Log("Ignoring CTCP game message because of an invalid amount of parameters.");
                return;
            }

            try
            {
                string revision = splitMessage[0];
                if (revision != ProgramConstants.CNCNET_PROTOCOL_REVISION)
                    return;
                string gameVersion = splitMessage[1];
                int maxPlayers = Conversions.IntFromString(splitMessage[2], 0);
                string gameRoomChannelName = splitMessage[3];
                string gameRoomDisplayName = splitMessage[4];
                bool locked = Conversions.BooleanFromString(splitMessage[5].Substring(0, 1), true);
                bool isCustomPassword = Conversions.BooleanFromString(splitMessage[5].Substring(1, 1), false);
                bool isClosed = Conversions.BooleanFromString(splitMessage[5].Substring(2, 1), true);
                bool isLoadedGame = Conversions.BooleanFromString(splitMessage[5].Substring(3, 1), false);
                bool isLadder = Conversions.BooleanFromString(splitMessage[5].Substring(4, 1), false);
                string[] players = splitMessage[6].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> playerNames = players.ToList();
                string mapName = splitMessage[7];
                string gameMode = splitMessage[8];

                string[] tunnelAddressAndPort = splitMessage[9].Split(':');
                string tunnelAddress = tunnelAddressAndPort[0];
                int tunnelPort = int.Parse(tunnelAddressAndPort[1]);

                string loadedGameId = splitMessage[10];

                CnCNetGame cncnetGame = gameCollection.GameList.Find(g => g.GameBroadcastChannel == channel.ChannelName);

                CnCNetTunnel tunnel = tunnelHandler.Tunnels.Find(t => t.Address == tunnelAddress && t.Port == tunnelPort);

                if (tunnel == null)
                    return;

                if (cncnetGame == null)
                    return;

                HostedCnCNetGame game = new HostedCnCNetGame(gameRoomChannelName, revision, gameVersion, maxPlayers,
                    gameRoomDisplayName, isCustomPassword, true, players,
                    e.UserName, mapName, gameMode);
                game.IsLoadedGame = isLoadedGame;
                game.MatchID = loadedGameId;
                game.LastRefreshTime = DateTime.Now;
                game.IsLadder = isLadder;
                game.Game = cncnetGame;
                game.Locked = locked || (game.IsLoadedGame && !game.Players.Contains(ProgramConstants.PLAYERNAME));
                game.Incompatible = cncnetGame == localGame && game.GameVersion != ProgramConstants.GAME_VERSION;
                game.TunnelServer = tunnel;

                if (isClosed)
                {
                    int index = lbGameList.HostedGames.FindIndex(hg => hg.HostName == e.UserName);

                    if (index > -1)
                    {
                        lbGameList.RemoveGame(index);

                        // 关闭任何不再有效的未处理邀请
                        DismissInvalidInvitations();
                    }

                    return;
                }

                // 根据主持名称在内部游戏列表中查找游戏；
                // 如果找到则刷新该游戏信息，否则作为新游戏添加
                int gameIndex = lbGameList.HostedGames.FindIndex(hg => hg.HostName == e.UserName);

                if (gameIndex > -1)
                {
                    lbGameList.HostedGames[gameIndex] = game;
                }
                else
                {
                    if (UserINISettings.Instance.PlaySoundOnGameHosted &&
                        cncnetGame.InternalName == localGameID.ToLower() &&
                        !ProgramConstants.IsInGame && !game.Locked)
                    {
                        SoundPlayer.Play(sndGameCreated);
                    }

                    lbGameList.AddGame(game);
                }
                SortAndRefreshHostedGames();
            }
            catch (Exception ex)
            {
                Logger.Log("Game parsing error: " + ex.Message);
            }
        }

        private void UpdateMessageBox_YesClicked(XNAMessageBox messageBox) =>
            UpdateCheck?.Invoke(this, EventArgs.Empty);

        private void UpdateMessageBox_NoClicked(XNAMessageBox messageBox) => updateDenied = true;

        private void BtnLogout_LeftClick(object sender, EventArgs e)
        {
            if (isInGameRoom)
            {
                topBar.SwitchToPrimary();
                return;
            }

            if (connectionManager.IsConnected &&
                !UserINISettings.Instance.PersistentMode)
            {
                connectionManager.Disconnect();
            }

            topBar.SwitchToPrimary();
        }

        public void SwitchOn()
        {
            Enable();

            if (!connectionManager.IsConnected && !connectionManager.IsAttemptingConnection)
            {
                loginWindow.Enable();
                loginWindow.LoadSettings();
            }

            SetLogOutButtonText();
        }

        public void SwitchOff() => Disable();

        public string GetSwitchName() => "联机大厅";

        private bool CanReceiveInvitationMessagesFrom(string username)
        {
            IRCUser iu = connectionManager.UserList.Find(u => u.Name == username);

            // 我们不接受来自不共享任何频道的人的邀请消息
            if (iu == null)
            {
                return false;
            }

            // 我们不希望收到已屏蔽用户的邀请消息
            if (cncnetUserData.IsIgnored(iu.Ident))
            {
                return false;
            }

            return true;
        }

        private Texture2D GetUserTexture(string username)
        {
            Texture2D senderGameIcon = unknownGameIcon;

            IRCUser iu = connectionManager.UserList.Find(u => u.Name == username);

            if (iu != null && iu.GameID >= 0 && iu.GameID < gameCollection.GameList.Count)
            {
                senderGameIcon = gameCollection.GameList[iu.GameID].Texture;
            }

            return senderGameIcon;
        }

        private void DismissInvalidInvitations()
        {
            var toDismiss = new List<UserChannelPair>();

            foreach (KeyValuePair<UserChannelPair, WeakReference> invitation in invitationIndex)
            {
                var gameIndex =
                    lbGameList.HostedGames.FindIndex(hg =>
                    ((HostedCnCNetGame)hg).HostName == invitation.Key.Item1 &&
                    ((HostedCnCNetGame)hg).ChannelName == invitation.Key.Item2);

                if (gameIndex == -1)
                {
                    toDismiss.Add(invitation.Key);
                }
            }

            foreach (UserChannelPair invitationIdentity in toDismiss)
            {
                DismissInvitation(invitationIdentity);
            }
        }

        private void DismissInvitation(UserChannelPair invitationIdentity)
        {
            if (invitationIndex.ContainsKey(invitationIdentity))
            {
                var invitationNotification = invitationIndex[invitationIdentity].Target as ChoiceNotificationBox;

                if (invitationNotification != null)
                {
                    WindowManager.RemoveControl(invitationNotification);
                }

                invitationIndex.Remove(invitationIdentity);
            }
        }

        /// <summary>
        /// 尝试查找指定用户所在的已托管游戏
        /// </summary>
        /// <param name="user">要查找游戏的用户。</param>
        /// <returns>找到的托管游戏或null。</returns>
        private HostedCnCNetGame GetHostedGameForUser(IRCUser user)
        {
            return lbGameList.HostedGames.Select(g => (HostedCnCNetGame)g).FirstOrDefault(g => g.Players.Contains(user.Name));
        }

        /// <summary>
        /// 根据指定用户当前是否在游戏中来加入其游戏。
        /// </summary>
        /// <param name="user">要加入的用户。</param>
        /// <param name="messageView">用于写入错误消息的消息视图/列表。</param>
        private void JoinUser(IRCUser user, IMessageView messageView)
        {
            if (user == null)
            {
                // 可能在用户离线时被选中
                messageView.AddMessage(new ChatMessage(Color.White, "用户当前不可用!"));
                return;
            }
            var game = GetHostedGameForUser(user);
            if (game == null)
            {
                messageView.AddMessage(new ChatMessage(Color.White, string.Format("{0}未在游戏中!", user.Name)));
                return;
            }

            JoinGame(game, string.Empty, messageView);
        }
    }
}
