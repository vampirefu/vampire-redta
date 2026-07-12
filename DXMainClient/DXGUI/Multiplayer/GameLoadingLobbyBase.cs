using ClientCore;
using ClientCore.Statistics;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;

namespace DTAClient.DXGUI.Multiplayer
{
    /// <summary>
    /// 多人游戏加载大厅的抽象基类。
    /// </summary>
    public abstract class GameLoadingLobbyBase : XNAWindow, ISwitchable
    {
        public GameLoadingLobbyBase(WindowManager windowManager, DiscordHandler discordHandler) : base(windowManager)
        {
            this.discordHandler = discordHandler;
        }

        public event EventHandler GameLeft;

        /// <summary>
        /// 当前存档中的玩家列表。
        /// </summary>
        protected List<SavedGamePlayer> SGPlayers = new List<SavedGamePlayer>();

        /// <summary>
        /// 游戏大厅中的玩家列表。
        /// </summary>
        protected List<PlayerInfo> Players = new List<PlayerInfo>();

        protected bool IsHost = false;

        protected DiscordHandler discordHandler;

        protected XNAClientDropDown ddSavedGame;

        protected ChatListBox lbChatMessages;
        protected XNATextBox tbChatInput;

        protected EnhancedSoundEffect sndGetReadySound;
        protected EnhancedSoundEffect sndJoinSound;
        protected EnhancedSoundEffect sndLeaveSound;
        protected EnhancedSoundEffect sndMessageSound;

        protected XNALabel lblDescription;
        protected XNAPanel panelPlayers;
        protected XNALabel[] lblPlayerNames;

        private XNALabel lblMapName;
        protected XNALabel lblMapNameValue;
        private XNALabel lblGameMode;
        protected XNALabel lblGameModeValue;
        private XNALabel lblSavedGameTime;

        protected XNAClientButton btnLoadGame;
        protected XNAClientButton btnLeaveGame;

        private List<MultiplayerColor> MPColors = new List<MultiplayerColor>();

        private string loadedGameID;

        private bool isSettingUp = false;
        private FileSystemWatcher fsw;

        private int uniqueGameId = 0;
        private DateTime gameLoadTime;

        public override void Initialize()
        {

           
            Name = "GameLoadingLobby";
            ClientRectangle = new Rectangle(0, 0, 590, 510);
            BackgroundTexture = AssetLoader.LoadTexture("loadmpsavebg.png");

            lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = nameof(lblDescription);
            lblDescription.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblDescription.Text = "等待所有玩家加入并准备,然后点击载入游戏来加载已保存的多人游戏.";

            panelPlayers = new XNAPanel(WindowManager);
            panelPlayers.ClientRectangle = new Rectangle(12, 32, 373, 125);
            panelPlayers.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            panelPlayers.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            AddChild(lblDescription);
            AddChild(panelPlayers);

            lblPlayerNames = new XNALabel[8];
            for (int i = 0; i < 8; i++)
            {
                XNALabel lblPlayerName = new XNALabel(WindowManager);
                lblPlayerName.Name = nameof(lblPlayerName) + i;

                if (i < 4)
                    lblPlayerName.ClientRectangle = new Rectangle(9, 9 + 30 * i, 0, 0);
                else
                    lblPlayerName.ClientRectangle = new Rectangle(190, 9 + 30 * (i - 4), 0, 0);

                lblPlayerName.Text = string.Format("玩家{0}", i) + " ";
                panelPlayers.AddChild(lblPlayerName);
                lblPlayerNames[i] = lblPlayerName;
            }

            lblMapName = new XNALabel(WindowManager);
            lblMapName.Name = nameof(lblMapName);
            lblMapName.FontIndex = 1;
            lblMapName.ClientRectangle = new Rectangle(panelPlayers.Right + 12,
                panelPlayers.Y, 0, 0);
            lblMapName.Text = "地图:";

            lblMapNameValue = new XNALabel(WindowManager);
            lblMapNameValue.Name = nameof(lblMapNameValue);
            lblMapNameValue.ClientRectangle = new Rectangle(lblMapName.X,
                lblMapName.Y + 18, 0, 0);
            lblMapNameValue.Text = "地图名称";

            lblGameMode = new XNALabel(WindowManager);
            lblGameMode.Name = nameof(lblGameMode);
            lblGameMode.ClientRectangle = new Rectangle(lblMapName.X,
                panelPlayers.Y + 40, 0, 0);
            lblGameMode.FontIndex = 1;
            lblGameMode.Text = "游戏模式:";

            lblGameModeValue = new XNALabel(WindowManager);
            lblGameModeValue.Name = nameof(lblGameModeValue);
            lblGameModeValue.ClientRectangle = new Rectangle(lblGameMode.X,
                lblGameMode.Y + 18, 0, 0);
            lblGameModeValue.Text = "游戏模式";

            lblSavedGameTime = new XNALabel(WindowManager);
            lblSavedGameTime.Name = nameof(lblSavedGameTime);
            lblSavedGameTime.ClientRectangle = new Rectangle(lblMapName.X,
                panelPlayers.Bottom - 40, 0, 0);
            lblSavedGameTime.FontIndex = 1;
            lblSavedGameTime.Text = "保存游戏:";

            ddSavedGame = new XNAClientDropDown(WindowManager);
            ddSavedGame.Name = nameof(ddSavedGame);
            ddSavedGame.ClientRectangle = new Rectangle(lblSavedGameTime.X,
                panelPlayers.Bottom - 21,
                Width - lblSavedGameTime.X - 12, 21);
            ddSavedGame.SelectedIndexChanged += DdSavedGame_SelectedIndexChanged;

            lbChatMessages = new ChatListBox(WindowManager);
            lbChatMessages.Name = nameof(lbChatMessages);
            lbChatMessages.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbChatMessages.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbChatMessages.ClientRectangle = new Rectangle(12, panelPlayers.Bottom + 12,
                Width - 24,
                Height - panelPlayers.Bottom - 12 - 29 - 34);

            tbChatInput = new XNATextBox(WindowManager);
            tbChatInput.Name = nameof(tbChatInput);
            tbChatInput.ClientRectangle = new Rectangle(lbChatMessages.X,
                lbChatMessages.Bottom + 3, lbChatMessages.Width, 19);
            tbChatInput.MaximumTextLength = 200;
            tbChatInput.EnterPressed += TbChatInput_EnterPressed;

            btnLoadGame = new XNAClientButton(WindowManager);
            btnLoadGame.Name = nameof(btnLoadGame);
            btnLoadGame.ClientRectangle = new Rectangle(lbChatMessages.X,
                tbChatInput.Bottom + 6, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLoadGame.Text = "载入存档";
            btnLoadGame.LeftClick += BtnLoadGame_LeftClick;

            btnLeaveGame = new XNAClientButton(WindowManager);
            btnLeaveGame.Name = nameof(btnLeaveGame);
            btnLeaveGame.ClientRectangle = new Rectangle(Width - 145,
                btnLoadGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLeaveGame.Text = "离开游戏";
            btnLeaveGame.LeftClick += BtnLeaveGame_LeftClick;

            AddChild(lblMapName);
            AddChild(lblMapNameValue);
            AddChild(lblGameMode);
            AddChild(lblGameModeValue);
            AddChild(lblSavedGameTime);
            AddChild(lbChatMessages);
            AddChild(tbChatInput);
            AddChild(btnLoadGame);
            AddChild(btnLeaveGame);
            AddChild(ddSavedGame);

            base.Initialize();

            sndJoinSound = new EnhancedSoundEffect("joingame.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyJoinCooldown);
            sndLeaveSound = new EnhancedSoundEffect("leavegame.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyLeaveCooldown);
            sndMessageSound = new EnhancedSoundEffect("message.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundMessageCooldown);
            sndGetReadySound = new EnhancedSoundEffect("getready.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundGameLobbyGetReadyCooldown);

            MPColors = MultiplayerColor.LoadColors();

            WindowManager.CenterControlOnScreen(this);

            if (SavedGameManager.AreSavedGamesAvailable())
            {
                fsw = new FileSystemWatcher(SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "Saved Games"), "*.NET");
                fsw.EnableRaisingEvents = false;
                fsw.Created += fsw_Created;
                fsw.Changed += fsw_Created;
            }
        }

        /// <summary>
        /// 使用实际信息更新Discord Rich Presence。
        /// </summary>
        /// <param name="resetTimer">是否重启"已用时"计时器</param>
        protected abstract void UpdateDiscordPresence(bool resetTimer = false);

        /// <summary>
        /// 将Discord Rich Presence重置为默认状态。
        /// </summary>
        protected void ResetDiscordPresence() => discordHandler.UpdatePresence();

        private void BtnLeaveGame_LeftClick(object sender, EventArgs e) => LeaveGame();

        protected virtual void LeaveGame()
        {
            GameLeft?.Invoke(this, EventArgs.Empty);
            ResetDiscordPresence();
        }

        private void fsw_Created(object sender, FileSystemEventArgs e) =>
            AddCallback(new Action<FileSystemEventArgs>(HandleFSWEvent), e);

        private void HandleFSWEvent(FileSystemEventArgs e)
        {
            Logger.Log("FSW Event: " + e.FullPath);

            if (Path.GetFileName(e.FullPath) == "SAVEGAME.NET")
            {
                SavedGameManager.RenameSavedGame();
            }
        }

        private void BtnLoadGame_LeftClick(object sender, EventArgs e)
        {
            if (!IsHost)
            {
                RequestReadyStatus();
                return;
            }

            if (Players.Find(p => !p.Ready) != null)
            {
                GetReadyNotification();
                return;
            }

            if (Players.Count != SGPlayers.Count)
            {
                NotAllPresentNotification();
                return;
            }

            HostStartGame();
        }

        protected abstract void RequestReadyStatus();

        protected virtual void GetReadyNotification()
        {
            AddNotice("游戏主持想要加载已储存的游戏但是有玩家未准备!");

            if (!IsHost && !Players.Find(p => p.Name == ProgramConstants.PLAYERNAME).Ready)
                sndGetReadySound.Play();

            WindowManager.FlashWindow();
        }

        protected virtual void NotAllPresentNotification() =>
            AddNotice("无法在有玩家未在场时加载已存储的游戏.");

        protected abstract void HostStartGame();

        protected void LoadGame()
        {
            FileInfo spawnFileInfo = SafePath.GetFile(ProgramConstants.GamePath, "spawn.ini");

            spawnFileInfo.Delete();

            File.Copy(SafePath.CombineFilePath(ProgramConstants.GamePath, "Saved Games", "spawnSG.ini"), spawnFileInfo.FullName);

            IniFile spawnIni = new IniFile(spawnFileInfo.FullName);

            int sgIndex = (ddSavedGame.Items.Count - 1) - ddSavedGame.SelectedIndex;

            spawnIni.SetStringValue("Settings", "SaveGameName",
                string.Format("SVGM_{0}.NET", sgIndex.ToString("D3")));
            spawnIni.SetBooleanValue("Settings", "LoadSaveGame", true);

            PlayerInfo localPlayer = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);

            if (localPlayer == null)
                return;

            spawnIni.SetIntValue("Settings", "Port", localPlayer.Port);

            for (int i = 1; i < Players.Count; i++)
            {
                string otherName = spawnIni.GetStringValue("Other" + i, "Name", string.Empty);

                if (string.IsNullOrEmpty(otherName))
                    continue;

                PlayerInfo otherPlayer = Players.Find(p => p.Name == otherName);

                if (otherPlayer == null)
                    continue;

                spawnIni.SetStringValue("Other" + i, "Ip", otherPlayer.IPAddress);
                spawnIni.SetIntValue("Other" + i, "Port", otherPlayer.Port);
            }

            WriteSpawnIniAdditions(spawnIni);
            spawnIni.WriteIniFile();

            FileInfo spawnMapFileInfo = SafePath.GetFile(ProgramConstants.GamePath, "spawnmap.ini");

            spawnMapFileInfo.Delete();
            using StreamWriter spawnMapStreamWriter = new StreamWriter(spawnMapFileInfo.FullName);
            spawnMapStreamWriter.WriteLine("[Map]");
            spawnMapStreamWriter.WriteLine("Size=0,0,50,50");
            spawnMapStreamWriter.WriteLine("LocalSize=0,0,50,50");
            spawnMapStreamWriter.WriteLine();

            gameLoadTime = DateTime.Now;

            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;
            GameProcessLogic.StartGameProcess(WindowManager);

            fsw.EnableRaisingEvents = true;
            UpdateDiscordPresence(true);
        }

        private void SharedUILogic_GameProcessExited() =>
            AddCallback(new Action(HandleGameProcessExited), null);

        protected virtual void HandleGameProcessExited()
        {
            fsw.EnableRaisingEvents = false;

            GameProcessLogic.GameProcessExited -= SharedUILogic_GameProcessExited;

            var matchStatistics = StatisticsManager.Instance.GetMatchWithGameID(uniqueGameId);

            if (matchStatistics != null)
            {
                int oldLength = matchStatistics.LengthInSeconds;
                int newLength = matchStatistics.LengthInSeconds +
                    (int)(DateTime.Now - gameLoadTime).TotalSeconds;

                matchStatistics.ParseStatistics(ProgramConstants.GamePath,
                    ClientConfiguration.Instance.LocalGame, true);

                matchStatistics.LengthInSeconds = newLength;

                StatisticsManager.Instance.SaveDatabase();
            }
            UpdateDiscordPresence(true);
        }

        protected virtual void WriteSpawnIniAdditions(IniFile spawnIni)
        {
            // 默认不做任何操作
        }

        protected void AddNotice(string notice) => AddNotice(notice, Color.White);

        protected abstract void AddNotice(string message, Color color);

        /// <summary>
        /// 根据最新的存档和已保存的spawn.ini文件中的信息，
        /// 以及本地玩家是否为游戏主持的信息来刷新UI。
        /// </summary>
        public void Refresh(bool isHost)
        {
            isSettingUp = true;
            IsHost = isHost;

            SGPlayers.Clear();
            Players.Clear();
            ddSavedGame.Items.Clear();
            lbChatMessages.Clear();
            lbChatMessages.TopIndex = 0;

            ddSavedGame.AllowDropDown = isHost;
            btnLoadGame.Text = isHost ? "载入游戏" : "准备就绪";

            IniFile spawnSGIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "Saved Games", "spawnSG.ini"));

            loadedGameID = spawnSGIni.GetStringValue("Settings", "GameID", "0");
            lblMapNameValue.Text = spawnSGIni.GetStringValue("Settings", "UIMapName", string.Empty);
            lblGameModeValue.Text = spawnSGIni.GetStringValue("Settings", "UIGameMode", string.Empty);

            uniqueGameId = spawnSGIni.GetIntValue("Settings", "GameID", -1);

            int playerCount = spawnSGIni.GetIntValue("Settings", "PlayerCount", 0);

            SavedGamePlayer localPlayer = new SavedGamePlayer();
            localPlayer.Name = ProgramConstants.PLAYERNAME;
            localPlayer.ColorIndex = MPColors.FindIndex(
                c => c.GameColorIndex == spawnSGIni.GetIntValue("Settings", "Color", 0));

            SGPlayers.Add(localPlayer);

            for (int i = 1; i < playerCount; i++)
            {
                string sectionName = "Other" + i;

                SavedGamePlayer sgPlayer = new SavedGamePlayer();
                sgPlayer.Name = spawnSGIni.GetStringValue(sectionName, "Name", "未知玩家");
                sgPlayer.ColorIndex = MPColors.FindIndex(
                    c => c.GameColorIndex == spawnSGIni.GetIntValue(sectionName, "Color", 0));

                SGPlayers.Add(sgPlayer);
            }

            for (int i = 0; i < SGPlayers.Count; i++)
            {
                lblPlayerNames[i].Enabled = true;
                lblPlayerNames[i].Visible = true;
            }

            for (int i = SGPlayers.Count; i < 8; i++)
            {
                lblPlayerNames[i].Enabled = false;
                lblPlayerNames[i].Visible = false;
            }

            List<string> timestamps = SavedGameManager.GetSaveGameTimestamps();
            timestamps.Reverse(); // 最近的存档排在最前面

            timestamps.ForEach(ts => ddSavedGame.AddItem(ts));

            if (ddSavedGame.Items.Count > 0)
                ddSavedGame.SelectedIndex = 0;

            CopyPlayerDataToUI();
            isSettingUp = false;
        }

        protected void CopyPlayerDataToUI()
        {
            for (int i = 0; i < SGPlayers.Count; i++)
            {
                SavedGamePlayer sgPlayer = SGPlayers[i];

                PlayerInfo pInfo = Players.Find(p => p.Name == SGPlayers[i].Name);

                XNALabel playerLabel = lblPlayerNames[i];

                if (pInfo == null)
                {
                    playerLabel.RemapColor = Color.Gray;
                    playerLabel.Text = sgPlayer.Name + " " + "(未在场)";
                    continue;
                }

                playerLabel.RemapColor = sgPlayer.ColorIndex > -1 ? MPColors[sgPlayer.ColorIndex].XnaColor
                    : Color.White;
                playerLabel.Text = pInfo.Ready ? sgPlayer.Name : sgPlayer.Name + " " + "(未准备)";
            }
        }

        protected virtual string GetIPAddressForPlayer(PlayerInfo pInfo) => "0.0.0.0";

        private void DdSavedGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!IsHost)
                return;

            for (int i = 1; i < Players.Count; i++)
                Players[i].Ready = false;

            CopyPlayerDataToUI();

            if (!isSettingUp)
                BroadcastOptions();
            UpdateDiscordPresence();
        }

        private void TbChatInput_EnterPressed(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbChatInput.Text))
                return;

            SendChatMessage(tbChatInput.Text);
            tbChatInput.Text = string.Empty;
        }

        /// <summary>
        /// 在派生类中重写，以向玩家广播玩家准备状态和所选的存档。
        /// </summary>
        protected abstract void BroadcastOptions();

        protected abstract void SendChatMessage(string message);

        public override void Draw(GameTime gameTime)
        {
            Renderer.FillRectangle(new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY),
                new Color(0, 0, 0, 255));

            base.Draw(gameTime);
        }

        public void SwitchOn() => Enable();

        public void SwitchOff() => Disable();

        public abstract string GetSwitchName();
    }
}
