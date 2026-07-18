using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using Microsoft.Xna.Framework;
using ClientCore;
using System.IO;
using Rampastring.Tools;
using ClientCore.Statistics;
using DTAClient.DXGUI.Generic;
using DTAClient.Domain.Multiplayer;
using ClientGUI;
using System.Text;
using DTAClient.Domain;
using Microsoft.Xna.Framework.Graphics;
using DTAClient.DXGUI.Helpers;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    /// <summary>
    /// 多人游戏大厅（CnCNet和局域网）的通用基类。
    /// </summary>
    public abstract class MultiplayerGameLobby : GameLobbyBase, ISwitchable
    {
        private const int MAX_DICE = 10;
        private const int MAX_DIE_SIDES = 100;

        public MultiplayerGameLobby(WindowManager windowManager, string iniName,
            TopBar topBar, MapLoader mapLoader, DiscordHandler discordHandler)
            : base(windowManager, iniName, mapLoader, true, discordHandler)
        {
            TopBar = topBar;

            chatBoxCommands = new List<ChatBoxCommand>
            {
                new ChatBoxCommand("HIDEMAPS", "隐藏地图列表(仅游戏主持)", true,
                    s => HideMapList()),
                new ChatBoxCommand("SHOWMAPS", "显示地图列表(仅游戏主持)", true,
                    s => ShowMapList()),
                new ChatBoxCommand("FRAMESENDRATE", "更改orderlag/FrameSendRate(默认7)(仅游戏主持)", true,
                    s => SetFrameSendRate(s)),
                new ChatBoxCommand("MAXAHEAD", "更改MaxAhead(默认0)(仅游戏主持)", true,
                    s => SetMaxAhead(s)),
                new ChatBoxCommand("PROTOCOLVERSION", "更改ProtocolVersion(默认2)(仅游戏主持)", true,
                    s => SetProtocolVersion(s)),
                new ChatBoxCommand("LOADMAP", "从/Maps/Custom/文件夹加载对应地图名的地图.", true, LoadCustomMap),
                new ChatBoxCommand("RANDOMSTARTS", "启用完全随机位置(仅泰伯利亚之日).", true,
                    s => SetStartingLocationClearance(s)),
                new ChatBoxCommand("ROLL", "掷骰子,例/roll3d6", false, RollDiceCommand),
                new ChatBoxCommand("SAVEOPTIONS", "保存预设用于日后加载", false, HandleGameOptionPresetSaveCommand),
                new ChatBoxCommand("LOADOPTIONS", "加载预设", true, HandleGameOptionPresetLoadCommand)
            };
        }

        protected XNAPlayerSlotIndicator[] StatusIndicators;

        protected ChatListBox lbChatMessages;
        protected XNAChatTextBox tbChatInput;
        protected XNAClientButton btnLockGame;
        protected XNAClientCheckBox chkAutoReady;

        protected bool IsHost = false;

        private bool locked = false;
        protected bool Locked
        {
            get => locked;
            set
            {
                bool oldLocked = locked;
                locked = value;
                if (oldLocked != value)
                {
                    CopyPlayerDataToUI();
                    UpdateDiscordPresence();
                }
            }
        }

        // protected bool DisableSpectatorReadyChecking = false;

        protected EnhancedSoundEffect sndJoinSound;
        protected EnhancedSoundEffect sndLeaveSound;
        protected EnhancedSoundEffect sndMessageSound;
        protected EnhancedSoundEffect sndGetReadySound;
        protected EnhancedSoundEffect sndReturnSound;

        protected Texture2D[] PingTextures;

        protected TopBar TopBar;

        protected int FrameSendRate { get; set; } = 1;

        /// <summary>
        /// 控制MaxAhead参数。默认值0表示不将该值写入spawn.ini，
        /// 允许生成器计算并分配MaxAhead值。
        /// </summary>
        protected int MaxAhead { get; set; }

        protected int ProtocolVersion { get; set; } = 2;

        protected List<ChatBoxCommand> chatBoxCommands;

        private FileSystemWatcher fsw;

        private bool gameSaved = false;

        private bool lastMapChangeWasInvalid = false;

        /// <summary>
        /// 允许派生类添加自己的聊天框命令。
        /// </summary>
        /// <param name="command">要添加的命令。</param>
        protected void AddChatBoxCommand(ChatBoxCommand command) => chatBoxCommands.Add(command);

        public override void Initialize()
        {
            Name = nameof(MultiplayerGameLobby);

            base.Initialize();

            // DisableSpectatorReadyChecking = GameOptionsIni.GetBooleanValue("General", "DisableSpectatorReadyChecking", false);

            PingTextures = new Texture2D[5]
            {
                AssetLoader.LoadTexture("ping0.png"),
                AssetLoader.LoadTexture("ping1.png"),
                AssetLoader.LoadTexture("ping2.png"),
                AssetLoader.LoadTexture("ping3.png"),
                AssetLoader.LoadTexture("ping4.png")
            };

            InitPlayerOptionDropdowns();

            StatusIndicators = new XNAPlayerSlotIndicator[MAX_PLAYER_COUNT];

            int statusIndicatorX = ConfigIni.GetIntValue(Name, "PlayerStatusIndicatorX", 0);
            int statusIndicatorY = ConfigIni.GetIntValue(Name, "PlayerStatusIndicatorY", 0);

            for (int i = 0; i < MAX_PLAYER_COUNT; i++)
            {
                var indicatorPlayerReady = new XNAPlayerSlotIndicator(WindowManager);
                indicatorPlayerReady.Name = "playerStatusIndicator" + i;
                indicatorPlayerReady.ClientRectangle = new Rectangle(statusIndicatorX, ddPlayerTeams[i].Y + statusIndicatorY,
                    0, 0);

                PlayerOptionsPanel.AddChild(indicatorPlayerReady);

                StatusIndicators[i] = indicatorPlayerReady;
                ddPlayerSides[i].AddItem("观战", AssetLoader.LoadTexture("spectatoricon.png"));
            }

            lbChatMessages = FindChild<ChatListBox>(nameof(lbChatMessages));

            tbChatInput = FindChild<XNAChatTextBox>(nameof(tbChatInput));
            tbChatInput.MaximumTextLength = 150;
            tbChatInput.EnterPressed += TbChatInput_EnterPressed;

            btnLockGame = FindChild<XNAClientButton>(nameof(btnLockGame));
            btnLockGame.LeftClick += BtnLockGame_LeftClick;

            chkAutoReady = FindChild<XNAClientCheckBox>(nameof(chkAutoReady));
            chkAutoReady.CheckedChanged += ChkAutoReady_CheckedChanged;
            chkAutoReady.Disable();

            MapPreviewBox.LocalStartingLocationSelected += MapPreviewBox_LocalStartingLocationSelected;
            MapPreviewBox.StartingLocationApplied += MapPreviewBox_StartingLocationApplied;

            sndJoinSound = new EnhancedSoundEffect("joingame.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyJoinCooldown);
            sndLeaveSound = new EnhancedSoundEffect("leavegame.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyLeaveCooldown);
            sndMessageSound = new EnhancedSoundEffect("message.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundMessageCooldown);
            sndGetReadySound = new EnhancedSoundEffect("getready.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyGetReadyCooldown);
            sndReturnSound = new EnhancedSoundEffect("return.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyReturnCooldown);

            if (SavedGameManager.AreSavedGamesAvailable())
            {
                fsw = new FileSystemWatcher(SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "Saved Games"), "*.NET");
                fsw.Created += fsw_Created;
                fsw.Changed += fsw_Created;
                fsw.EnableRaisingEvents = false;
            }
            else
                Logger.Log("MultiplayerGameLobby: Saved games are not available!");
        }

        /// <summary>
        /// 在派生类完成自身初始化之后执行必要的初始化。
        /// </summary>
        protected void PostInitialize()
        {
            CenterOnParent();
            LoadDefaultGameModeMap();
        }

        private void fsw_Created(object sender, FileSystemEventArgs e)
        {
            AddCallback(new Action<FileSystemEventArgs>(FSWEvent), e);
        }

        private void FSWEvent(FileSystemEventArgs e)
        {
            Logger.Log("FSW Event: " + e.FullPath);

            if (Path.GetFileName(e.FullPath) == "SAVEGAME.NET")
            {
                if (!gameSaved)
                {
                    bool success = SavedGameManager.InitSavedGames();

                    if (!success)
                        return;
                }

                gameSaved = true;

                SavedGameManager.RenameSavedGame();
            }
        }

        protected override void StartGame()
        {
            if (fsw != null)
                fsw.EnableRaisingEvents = true;

            for (int pId = 0; pId < Players.Count; pId++)
                Players[pId].IsInGame = true;


            base.StartGame();
        }

        protected override void GameProcessExited()
        {
            gameSaved = false;

            if (fsw != null)
                fsw.EnableRaisingEvents = false;

            PlayerInfo pInfo = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);
            pInfo.IsInGame = false;

            base.GameProcessExited();

            if (IsHost)
            {
                GenerateGameID();
                DdGameModeMapFilter_SelectedIndexChanged(null, EventArgs.Empty); // Refresh ranks
            }
            else if (chkAutoReady.Checked)
            {
                RequestReadyStatus();
            }
        }

        private void GenerateGameID()
        {
            int i = 0;

            while (i < 20)
            {
                string s = DateTime.Now.Day.ToString() +
                    DateTime.Now.Month.ToString() +
                    DateTime.Now.Hour.ToString() +
                    DateTime.Now.Minute.ToString();

                UniqueGameID = int.Parse(i.ToString() + s);

                if (StatisticsManager.Instance.GetMatchWithGameID(UniqueGameID) == null)
                    break;

                i++;
            }
        }

        private void BtnLockGame_LeftClick(object sender, EventArgs e)
        {
            HandleLockGameButtonClick();
        }

        protected virtual void HandleLockGameButtonClick()
        {
            if (Locked)
                UnlockGame(true);
            else
                LockGame();
        }

        protected abstract void LockGame();

        protected abstract void UnlockGame(bool manual);

        private void TbChatInput_EnterPressed(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbChatInput.Text))
                return;

            if (tbChatInput.Text.StartsWith("/"))
            {
                string text = tbChatInput.Text;
                string command;
                string parameters;

                int spaceIndex = text.IndexOf(' ');

                if (spaceIndex == -1)
                {
                    command = text.Substring(1).ToUpper();
                    parameters = string.Empty;
                }
                else
                {
                    command = text.Substring(1, spaceIndex - 1);
                    parameters = text.Substring(spaceIndex + 1);
                }

                tbChatInput.Text = string.Empty;

                foreach (var chatBoxCommand in chatBoxCommands)
                {
                    if (command.ToUpper() == chatBoxCommand.Command)
                    {
                        if (!IsHost && chatBoxCommand.HostOnly)
                        {
                            AddNotice(string.Format("/{0}仅游戏主持可用.", chatBoxCommand.Command));
                            return;
                        }

                        chatBoxCommand.Action(parameters);
                        return;
                    }
                }

                StringBuilder sb = new StringBuilder("要使用命令,先输入/<command>.可用的聊天框命令:" + " ");
                foreach (var chatBoxCommand in chatBoxCommands)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(Environment.NewLine);
                    sb.Append($"{chatBoxCommand.Command}: {chatBoxCommand.Description}");
                }
                XNAMessageBox.Show(WindowManager, "聊天框命令帮助", sb.ToString());
                return;
            }

            SendChatMessage(tbChatInput.Text);
            tbChatInput.Text = string.Empty;
        }

        private void ChkAutoReady_CheckedChanged(object sender, EventArgs e)
        {
            btnLaunchGame.Enabled = !chkAutoReady.Checked;
            RequestReadyStatus();
        }

        protected void ResetAutoReadyCheckbox()
        {
            chkAutoReady.CheckedChanged -= ChkAutoReady_CheckedChanged;
            chkAutoReady.Checked = false;
            chkAutoReady.CheckedChanged += ChkAutoReady_CheckedChanged;
            btnLaunchGame.Enabled = true;
        }

        private void SetFrameSendRate(string value)
        {
            bool success = int.TryParse(value, out int intValue);

            if (!success)
            {
                AddNotice("命令语法:/FrameSendRate<number>");
                return;
            }

            FrameSendRate = intValue;
            AddNotice(string.Format("FrameSendRate已更改为{0}", intValue));

            OnGameOptionChanged();
            ClearReadyStatuses();
        }

        private void SetMaxAhead(string value)
        {
            bool success = int.TryParse(value, out int intValue);

            if (!success)
            {
                AddNotice("命令语法:/MaxAhead<number>");
                return;
            }

            MaxAhead = intValue;
            AddNotice(string.Format("MaxAhead已更改为{0}", intValue));

            OnGameOptionChanged();
            ClearReadyStatuses();
        }

        private void SetProtocolVersion(string value)
        {
            bool success = int.TryParse(value, out int intValue);

            if (!success)
            {
                AddNotice("命令语法:/ProtocolVersion<number>.");
                return;
            }

            if (!(intValue == 0 || intValue == 2))
            {
                AddNotice("ProtocolVersion只能更改为0或2.");
                return;
            }

            ProtocolVersion = intValue;
            AddNotice(string.Format("ProtocolVersion已更改为{0}", intValue));

            OnGameOptionChanged();
            ClearReadyStatuses();
        }

        private void SetStartingLocationClearance(string value)
        {
            bool removeStartingLocations = Conversions.BooleanFromString(value, RemoveStartingLocations);

            SetRandomStartingLocations(removeStartingLocations);

            OnGameOptionChanged();
            ClearReadyStatuses();
        }

        /// <summary>
        /// 启用或禁用完全随机起始位置，并相应地通知用户。
        /// </summary>
        /// <param name="newValue">完全随机起始位置的新值。</param>
        protected void SetRandomStartingLocations(bool newValue)
        {
            if (newValue != RemoveStartingLocations)
            {
                RemoveStartingLocations = newValue;
                if (RemoveStartingLocations)
                    AddNotice("游戏主持启用了完全随机位置(仅常规地图)..");
                else
                    AddNotice("游戏主持禁用了完全随机位置.");
            }
        }

        /// <summary>
        /// 处理掷骰子命令。
        /// </summary>
        /// <param name="dieType">用户为命令提供的参数。</param>
        private void RollDiceCommand(string dieType)
        {
            int dieSides = 6;
            int dieCount = 1;

            if (!string.IsNullOrEmpty(dieType))
            {
                string[] parts = dieType.Split('d');
                if (parts.Length == 2)
                {
                    if (!int.TryParse(parts[0], out dieCount) || !int.TryParse(parts[1], out dieSides))
                    {
                        AddNotice("无效骰子语法.正确格式:/roll<diecount>d<diesides>");
                        return;
                    }
                }
            }

            if (dieCount > MAX_DICE || dieCount < 1)
            {
                AddNotice("您一次只能1到10个骰子.");
                return;
            }

            if (dieSides > MAX_DIE_SIDES || dieSides < 2)
            {
                AddNotice("您一次只能2到100面.");
                return;
            }

            int[] results = new int[dieCount];
            Random random = new Random();
            for (int i = 0; i < dieCount; i++)
            {
                results[i] = random.Next(1, dieSides + 1);
            }

            BroadcastDiceRoll(dieSides, results);
        }

        /// <summary>
        /// 处理自定义地图加载命令。
        /// </summary>
        /// <param name="mapName">作为参数给出的地图名称，不含文件扩展名。</param>
        private void LoadCustomMap(string mapName)
        {
            // Logger.Log("111111");
            Map map = MapLoader.LoadCustomMap($"Maps/Custom/{mapName}", out string resultMessage);
            if (map != null)
            {
                AddNotice(resultMessage);
                ListMaps();
            }
            else
            {
                AddNotice(resultMessage, Color.Red);
            }
        }

        /// <summary>
        /// 在派生类中重写以向其他玩家广播掷骰子的结果。
        /// </summary>
        /// <param name="dieSides">骰子的面数。</param>
        /// <param name="results">掷骰子的结果。</param>
        protected abstract void BroadcastDiceRoll(int dieSides, int[] results);

        /// <summary>
        /// 解析并列出掷骰子的结果。
        /// </summary>
        /// <param name="senderName">掷骰子的玩家。</param>
        /// <param name="result">掷骰子的结果，每个骰子用逗号分隔，骰子面数作为第一个数字。</param>
        /// <example>
        /// HandleDiceRollResult("Rampastring", "6,3,5,1") 表示
        /// Rampastring掷了三个六面骰子，得到了3、5和1。
        /// </example>
        protected void HandleDiceRollResult(string senderName, string result)
        {
            if (string.IsNullOrEmpty(result))
                return;

            string[] parts = result.Split(',');
            if (parts.Length < 2 || parts.Length > MAX_DICE + 1)
                return;

            int[] intArray = Array.ConvertAll(parts, (s) => { return Conversions.IntFromString(s, -1); });
            int dieSides = intArray[0];
            if (dieSides < 1 || dieSides > MAX_DIE_SIDES)
                return;
            int[] results = new int[intArray.Length - 1];
            Array.ConstrainedCopy(intArray, 1, results, 0, results.Length);

            for (int i = 1; i < intArray.Length; i++)
            {
                if (intArray[i] < 1 || intArray[i] > dieSides)
                    return;
            }

            PrintDiceRollResult(senderName, dieSides, results);
        }

        /// <summary>
        /// 打印掷骰子的结果。
        /// </summary>
        /// <param name="senderName">掷骰子的玩家。</param>
        /// <param name="dieSides">骰子的面数。</param>
        /// <param name="results">掷骰子的结果。</param>
        protected void PrintDiceRollResult(string senderName, int dieSides, int[] results)
        {
            AddNotice(String.Format("{0}掷{1}d{2}结果{3}",
                senderName, results.Length, dieSides, string.Join(", ", results)
            ));
        }

        protected abstract void SendChatMessage(string message);

        /// <summary>
        /// 根据本地玩家是否为主机来更改游戏大厅的UI。
        /// </summary>
        /// <param name="isHost">确定本地玩家是否为游戏主机。</param>
        protected void Refresh(bool isHost)
        {
            IsHost = isHost;
            Locked = false;
            CopyPlayerDataToUI();

            UpdateMapPreviewBoxEnabledStatus();
            PlayerExtraOptionsPanel?.SetIsHost(isHost);
            //MapPreviewBox.EnableContextMenu = IsHost;

            btnLaunchGame.Text = IsHost ? BTN_LAUNCH_GAME : BTN_LAUNCH_READY;

            if (IsHost)
            {
                ShowMapList();
                BtnSaveLoadGameOptions?.Enable();

                btnLockGame.Text = "锁定游戏";
                btnLockGame.Enabled = true;
                btnLockGame.Visible = true;
                chkAutoReady.Disable();

                foreach (GameLobbyDropDown dd in DropDowns)
                {
                    dd.InputEnabled = true;
                    dd.SelectedIndex = dd.UserSelectedIndex;
                }

                foreach (GameLobbyCheckBox checkBox in CheckBoxes)
                {
                    checkBox.AllowChanges = true;
                    checkBox.Checked = checkBox.UserChecked;
                }

                GenerateGameID();
            }
            else
            {
                HideMapList();
                BtnSaveLoadGameOptions?.Disable();

                btnLockGame.Enabled = false;
                btnLockGame.Visible = false;
                ReadINIForControl(chkAutoReady);

                foreach (GameLobbyDropDown dd in DropDowns)
                    dd.InputEnabled = false;

                foreach (GameLobbyCheckBox checkBox in CheckBoxes)
                    checkBox.AllowChanges = false;
            }

            LoadDefaultGameModeMap();

            lbChatMessages.Clear();
            lbChatMessages.TopIndex = 0;

            lbChatMessages.AddItem("输入/查看所有可用的聊天框命令.", Color.Silver, true);

            if (SavedGameManager.GetSaveGameCount() > 0)
            {
                lbChatMessages.AddItem("检测到之前已储存的多人游戏.先前的已储存游戏会被删除并生成新的.",
                    Color.Yellow, true);
            }
        }

        private void HideMapList()
        {
            lbChatMessages.Name = "lbChatMessages_Player";
            tbChatInput.Name = "tbChatInput_Player";
            MapPreviewBox.Name = "MapPreviewBox";
            lblMapName.Name = "lblMapName";
            //lblMapAuthor.Name = "lblMapAuthor";
            lblGameMode.Name = "lblGameMode";
            lblMapSize.Name = "lblMapSize";

            ReadINIForControl(btnPickRandomMap);
            ReadINIForControl(lbChatMessages);
            ReadINIForControl(tbChatInput);
            ReadINIForControl(lbGameModeMapList);
            ReadINIForControl(lblMapName);
            //ReadINIForControl(lblMapAuthor);
            ReadINIForControl(lblGameMode);
            ReadINIForControl(lblMapSize);
            ReadINIForControl(btnMapSortAlphabetically);

            ddGameModeMapFilter.Disable();
            lblGameModeSelect.Disable();
            lblModeText.Disable();
            lbGameModeMapList.Disable();
            tbMapSearch.Disable();
            btnPickRandomMap.Disable();
            btnAginLoadMaps.Disable();
            btnMapSortAlphabetically.Disable();
            // 禁用人数控件
            lblscreen.Disable();
            ddPeople.Disable();

            IniControlVisibleWhenChangeMap();
        }

        private void ShowMapList()
        {
            lbChatMessages.Name = "lbChatMessages_Host";
            tbChatInput.Name = "tbChatInput_Host";
            MapPreviewBox.Name = "MapPreviewBox";
            lblMapName.Name = "lblMapName";
            //lblMapAuthor.Name = "lblMapAuthor";
            lblGameMode.Name = "lblGameMode";
            lblMapSize.Name = "lblMapSize";

            ddGameModeMapFilter.Enable();
            lblGameModeSelect.Enable();
            lbGameModeMapList.Enable();
            tbMapSearch.Enable();
            btnPickRandomMap.Enable();
            btnMapSortAlphabetically.Enable();

            ReadINIForControl(btnPickRandomMap);
            ReadINIForControl(lbChatMessages);
            ReadINIForControl(tbChatInput);
            ReadINIForControl(lbGameModeMapList);
            ReadINIForControl(lblMapName);
            //ReadINIForControl(lblMapAuthor);
            ReadINIForControl(lblGameMode);
            ReadINIForControl(lblMapSize);
            ReadINIForControl(btnMapSortAlphabetically);

            // 显示人数控件
            lblscreen.Enable();
            ddPeople.Enable();
        }

        private void MapPreviewBox_LocalStartingLocationSelected(object sender, LocalStartingLocationEventArgs e)
        {
            int mTopIndex = Players.FindIndex(p => p.Name == ProgramConstants.PLAYERNAME);

            if (mTopIndex == -1 || Players[mTopIndex].SideId == ddPlayerSides[0].Items.Count - 1)
                return;

            ddPlayerStarts[mTopIndex].SelectedIndex = e.StartingLocationIndex;
        }

        private void MapPreviewBox_StartingLocationApplied(object sender, EventArgs e)
        {
            ClearReadyStatuses();
            CopyPlayerDataToUI();
            BroadcastPlayerOptions();
        }

        /// <summary>
        /// 处理用户点击"启动游戏"/"我准备好了"按钮。
        /// 如果本地玩家是游戏主机，检查游戏是否可以启动，如果允许则启动游戏。
        /// 如果本地玩家不是游戏主机，则发送准备请求。
        /// </summary>
        protected override void BtnLaunchGame_LeftClick(object sender, EventArgs e)
        {
            if (!IsHost)
            {
                RequestReadyStatus();
                return;
            }

            if (!Locked)
            {
                LockGameNotification();
                return;
            }

            var teamMappingsError = GetTeamMappingsError();
            if (!string.IsNullOrEmpty(teamMappingsError))
            {
                AddNotice(teamMappingsError);
                return;
            }

            List<int> occupiedColorIds = new List<int>();
            foreach (PlayerInfo player in Players)
            {
                if (occupiedColorIds.Contains(player.ColorId) && player.ColorId > 0)
                {
                    SharedColorsNotification();
                    return;
                }

                occupiedColorIds.Add(player.ColorId);
            }

            if (AIPlayers.Count(pInfo => pInfo.SideId == ddPlayerSides[0].Items.Count - 1) > 0)
            {
                AISpectatorsNotification();
                return;
            }

            if (Map.EnforceMaxPlayers)
            {
                foreach (PlayerInfo pInfo in Players)
                {
                    if (pInfo.StartingLocation == 0)
                        continue;

                    if (Players.Concat(AIPlayers).ToList().Find(
                        p => p.StartingLocation == pInfo.StartingLocation &&
                        p.Name != pInfo.Name) != null)
                    {
                        SharedStartingLocationNotification();
                        return;
                    }
                }

                for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
                {
                    int startingLocation = AIPlayers[aiId].StartingLocation;

                    if (startingLocation == 0)
                        continue;

                    int index = AIPlayers.FindIndex(aip => aip.StartingLocation == startingLocation);

                    if (index > -1 && index != aiId)
                    {
                        SharedStartingLocationNotification();
                        return;
                    }
                }

                int totalPlayerCount = Players.Count(p => p.SideId < ddPlayerSides[0].Items.Count - 1)
                    + AIPlayers.Count;

                int minPlayers = GameMode.MinPlayersOverride > -1 ? GameMode.MinPlayersOverride : Map.MinPlayers;
                if (totalPlayerCount < minPlayers)
                {
                    InsufficientPlayersNotification();
                    return;
                }

                if (Map.EnforceMaxPlayers && totalPlayerCount > Map.MaxPlayers)
                {
                    TooManyPlayersNotification();
                    return;
                }
            }

            int iId = 0;
            foreach (PlayerInfo player in Players)
            {
                iId++;

                if (player.Name == ProgramConstants.PLAYERNAME)
                    continue;

                if (!player.Verified)
                {
                    NotVerifiedNotification(iId - 1);
                    return;
                }


                if (player.IsInGame)
                {
                    StillInGameNotification(iId - 1);
                    return;
                }
                /*
                if (DisableSpectatorReadyChecking)
                {
                    // Only account ready status if player is not a spectator
                    if (!player.Ready && !IsPlayerSpectator(player))
                    {
                        GetReadyNotification();
                        return;
                    }
                }
                else
                {
                    if (!player.Ready)
                    {
                        GetReadyNotification();
                        return;
                    }
                }
                */

                if (!player.Ready)
                {
                    GetReadyNotification();
                    return;
                }

            }

            HostLaunchGame();
        }

        protected virtual void LockGameNotification() =>
            AddNotice("启动游戏前需要先锁定游戏房间.");

        protected virtual void SharedColorsNotification() =>
            AddNotice("多个人类玩家不能使用相同的颜色.");

        protected virtual void AISpectatorsNotification() =>
            AddNotice("AI不喜欢观战.它们想操作!");

        protected virtual void SharedStartingLocationNotification() =>
            AddNotice("多个人类玩家不能使用相同的位置.");

        protected virtual void NotVerifiedNotification(int playerIndex)
        {
            if (playerIndex > -1 && playerIndex < Players.Count)
                AddNotice(string.Format("无法启动游戏;玩家{0}未准备.", Players[playerIndex].Name));
        }

        protected virtual void StillInGameNotification(int playerIndex)
        {
            if (playerIndex > -1 && playerIndex < Players.Count)
            {
                AddNotice(String.Format("无法启动游戏;玩家{0}还在您先前启动的游戏中.",
                    Players[playerIndex].Name));
            }
        }

        protected virtual void GetReadyNotification()
        {
            AddNotice("游戏主持想要启动游戏但有玩家未准备!");
            if (!IsHost && !Players.Find(p => p.Name == ProgramConstants.PLAYERNAME).Ready)
                sndGetReadySound.Play();
        }

        protected virtual void InsufficientPlayersNotification()
        {
            if (GameMode != null && GameMode.MinPlayersOverride > -1)
                AddNotice(String.Format("无法启动游戏:{0}不能低于{1}名玩家",
                    GameMode.UIName, GameMode.MinPlayersOverride));
            else if (Map != null)
                AddNotice(String.Format("无法启动游戏:此地图不能低于{0}名玩家.",
                    Map.MinPlayers));
        }

        protected virtual void TooManyPlayersNotification()
        {
            if (Map != null)
                AddNotice(String.Format("无法启动游戏:不能高于{0}名玩家.",
                    Map.MaxPlayers));
        }

        public virtual void Clear()
        {
            if (!IsHost)
                AIPlayers.Clear();

            Players.Clear();
        }

        protected override void OnGameOptionChanged()
        {
            base.OnGameOptionChanged();

            ClearReadyStatuses();
            CopyPlayerDataToUI();
        }

        protected abstract void HostLaunchGame();

        protected override void CopyPlayerDataFromUI(object sender, EventArgs e)
        {
            if (PlayerUpdatingInProgress)
                return;

            if (IsHost)
            {
                base.CopyPlayerDataFromUI(sender, e);
                BroadcastPlayerOptions();
                return;
            }

            int mTopIndex = Players.FindIndex(p => p.Name == ProgramConstants.PLAYERNAME);

            if (mTopIndex == -1)
                return;

            int requestedSide = ddPlayerSides[mTopIndex].SelectedIndex;
            int requestedColor = ddPlayerColors[mTopIndex].SelectedIndex;
            int requestedStart = ddPlayerStarts[mTopIndex].SelectedIndex;
            int requestedTeam = ddPlayerTeams[mTopIndex].SelectedIndex;

            RequestPlayerOptions(requestedSide, requestedColor, requestedStart, requestedTeam);
        }

        protected override void CopyPlayerDataToUI()
        {
            if (Players.Count + AIPlayers.Count > MAX_PLAYER_COUNT)
                return;

            base.CopyPlayerDataToUI();

            ClearPingIndicators();

            if (IsHost)
            {
                for (int pId = 1; pId < Players.Count; pId++)
                    ddPlayerNames[pId].AllowDropDown = true;
            }

            // 玩家状态
            for (int pId = 0; pId < Players.Count; pId++)
            {
                /* if (pId != 0 && !Players[pId].Verified) // If player is not verified (not counting the host)
                {
                    StatusIndicators[pId].SwitchTexture("error");
                }
                else */
                if (Players[pId].IsInGame) // 如果玩家在游戏中
                {
                    StatusIndicators[pId].SwitchTexture(PlayerSlotState.InGame);
                }
                else if (pId == 0) // 如果玩家是主机
                {
                    StatusIndicators[pId].SwitchTexture(Locked ? PlayerSlotState.Ready : PlayerSlotState.NotReady); // 显示房间锁定状态
                }
                else
                {
                    // StatusIndicators[pId].SwitchTexture(
                    //     (IsPlayerSpectator(Players[pId]) && DisableSpectatorReadyChecking) 
                    //     ? "okDisabled" : "ok");
                    StatusIndicators[pId].SwitchTexture(Players[pId].Ready ? PlayerSlotState.Ready : PlayerSlotState.NotReady);
                }
                /*
                else
                {
                    // StatusIndicators[pId].SwitchTexture(
                    //     (IsPlayerSpectator(Players[pId]) && DisableSpectatorReadyChecking) 
                    //     ? "offDisabled" : "off");

                }
                */

                UpdatePlayerPingIndicator(Players[pId]);
            }

            // AI状态
            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                StatusIndicators[aiId + Players.Count].SwitchTexture(
                    IsPlayerSpectator(AIPlayers[aiId]) ? PlayerSlotState.Error : PlayerSlotState.AI);

                if (IsPlayerSpectator(AIPlayers[aiId]))
                    StatusIndicators[aiId + Players.Count].ToolTip.Text += Environment.NewLine + "AI players can't be spectators.";
            }

            // 空槽位状态
            for (int i = AIPlayers.Count + Players.Count; i < MAX_PLAYER_COUNT; i++)
            {
                StatusIndicators[i].SwitchTexture(PlayerSlotState.Empty);
            }
        }

        protected virtual void ClearPingIndicators()
        {
            foreach (XNAClientDropDown dd in ddPlayerNames)
            {
                dd.Items[0].Texture = null;
                dd.ToolTip.Text = string.Empty;
            }
        }

        protected virtual void UpdatePlayerPingIndicator(PlayerInfo pInfo)
        {
            XNAClientDropDown ddPlayerName = ddPlayerNames[pInfo.Index];
            ddPlayerName.Items[0].Texture = GetTextureForPing(pInfo.Ping);
            if (pInfo.Ping < 0)
                ddPlayerName.ToolTip.Text = "延迟:" + " ? ms";
            else
                ddPlayerName.ToolTip.Text = "延迟:" + $" {pInfo.Ping} ms";
        }

        private Texture2D GetTextureForPing(int ping)
        {
            switch (ping)
            {
                case int p when (p > 350):
                    return PingTextures[4];
                case int p when (p > 250):
                    return PingTextures[3];
                case int p when (p > 100):
                    return PingTextures[2];
                case int p when (p >= 0):
                    return PingTextures[1];
                default:
                    return PingTextures[0];
            }
        }

        protected abstract void BroadcastPlayerOptions();

        protected abstract void BroadcastPlayerExtraOptions();

        protected abstract void RequestPlayerOptions(int side, int color, int start, int team);

        protected abstract void RequestReadyStatus();

        // 此方法为public，因为主大厅使用它来通知用户邀请失败
        public void AddWarning(string message)
        {
            AddNotice(message, Color.Yellow);
        }

        protected override bool AllowPlayerOptionsChange() => IsHost;

        protected override void ChangeMap(GameModeMap gameModeMap)
        {
            base.ChangeMap(gameModeMap);

            bool resetAutoReady = gameModeMap?.GameMode == null || gameModeMap?.Map == null;

            ClearReadyStatuses(resetAutoReady);

            if ((lastMapChangeWasInvalid || resetAutoReady) && chkAutoReady.Checked)
                RequestReadyStatus();

            lastMapChangeWasInvalid = resetAutoReady;

            //if (IsHost)
            //    OnGameOptionChanged();

            IniControlVisibleWhenChangeMap();
        }

        /// <summary>
        /// 初始化切换地图时控件可见性的逻辑方法
        /// </summary>
        public void IniControlVisibleWhenChangeMap()
        {
            if (!IsHost)
            {
                // 根据逻辑判断是否显示地图玩法
                if (string.IsNullOrEmpty(GameModeMap.Map?.PlayDescription))
                    lblPlayDescription.Visible = false;
                else
                    lblPlayDescription.Visible = true;

                // 根据逻辑判断当前地图是否为防守地图并确定chkDefenceAiTrigger是否显示
                bool isShow = DefenceAiHelper.IsShowCKH(GameModeMap.Map.BaseFilePath);
                var chkDefenceAiTrigger = CheckBoxes.FirstOrDefault(p => p.Name == "chkDefenceAiTrigger");
                chkDefenceAiTrigger.Visible = isShow;
            }

        }

        protected override void ToggleFavoriteMap()
        {
            base.ToggleFavoriteMap();

            if (GameModeMap.IsFavorite || !IsHost)
                return;

            RefreshForFavoriteMapRemoved();
        }

        protected override void WriteSpawnIniAdditions(IniFile iniFile)
        {
            base.WriteSpawnIniAdditions(iniFile);
            iniFile.SetIntValue("Settings", "FrameSendRate", FrameSendRate);
            if (MaxAhead > 0)
                iniFile.SetIntValue("Settings", "MaxAhead", MaxAhead);
            iniFile.SetIntValue("Settings", "Protocol", ProtocolVersion);
        }

        protected override int GetDefaultMapRankIndex(GameModeMap gameModeMap)
        {
            if (gameModeMap.Map.MaxPlayers > 3)
                return StatisticsManager.Instance.GetCoopRankForDefaultMap(gameModeMap.Map.Name, gameModeMap.Map.MaxPlayers);

            if (StatisticsManager.Instance.HasWonMapInPvP(gameModeMap.Map.Name, gameModeMap.GameMode.UIName, gameModeMap.Map.MaxPlayers))
                return 2;

            return -1;
        }

        public void SwitchOn() => Enable();

        public void SwitchOff() => Disable();

        public abstract string GetSwitchName();

        protected override void UpdateMapPreviewBoxEnabledStatus()
        {
            if (Map != null && GameMode != null)
            {
                bool disablestartlocs = (Map.ForceRandomStartLocations || GameMode.ForceRandomStartLocations || GetPlayerExtraOptions().IsForceRandomStarts);
                MapPreviewBox.EnableContextMenu = disablestartlocs ? false : IsHost;
                MapPreviewBox.EnableStartLocationSelection = !disablestartlocs;
            }
            else
            {
                MapPreviewBox.EnableContextMenu = IsHost;
                MapPreviewBox.EnableStartLocationSelection = true;
            }
        }
    }
}
