using ClientGUI;
using System;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using Microsoft.Xna.Framework;
using ClientCore;
using System.IO;
using Rampastring.Tools;
using DTAConfig;
using DTAClient.DXGUI.Generic;

namespace DTAClient.DXGUI.Multiplayer
{
    /// <summary>
    /// 一个窗口，使主持游戏的局域网玩家可以选择主持新游戏或主持已加载的游戏。
    /// </summary>
    class LANGameCreationWindow : XNAWindow
    {
        public LANGameCreationWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        public event EventHandler NewGame;
        public event EventHandler<GameLoadEventArgs> LoadGame;

        private XNALabel lblDescription;

        private XNAButton btnNewGame;
        private XNAButton btnLoadGame;
        private XNAButton btnCancel;

        public override void Initialize()
        {
            Name = "LANGameCreationWindow";
            BackgroundTexture = AssetLoader.LoadTexture("gamecreationoptionsbg.png");
            ClientRectangle = new Rectangle(0, 0, 447, 77);
            

            //optionsWindow.tabControl.MakeUnselectable(4);
            lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = "lblDescription";
            lblDescription.FontIndex = 1;
            lblDescription.Text = "选择游戏类型";

            AddChild(lblDescription);

            lblDescription.CenterOnParent();
            lblDescription.ClientRectangle = new Rectangle(
                lblDescription.X,
                12,
                lblDescription.Width,
                lblDescription.Height);

            btnNewGame = new XNAButton(WindowManager);
            btnNewGame.Name = "btnNewGame";
            btnNewGame.ClientRectangle = new Rectangle(12, 42, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnNewGame.IdleTexture = AssetLoader.LoadTexture("133pxbtn.png");
            btnNewGame.HoverTexture = AssetLoader.LoadTexture("133pxbtn_c.png");
            btnNewGame.FontIndex = 1;
            btnNewGame.Text = "新建游戏";
            btnNewGame.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnNewGame.LeftClick += BtnNewGame_LeftClick;

            btnLoadGame = new XNAButton(WindowManager);
            btnLoadGame.Name = "btnLoadGame";
            btnLoadGame.ClientRectangle = new Rectangle(btnNewGame.Right + 12,
                btnNewGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLoadGame.IdleTexture = btnNewGame.IdleTexture;
            btnLoadGame.HoverTexture = btnNewGame.HoverTexture;
            btnLoadGame.FontIndex = 1;
            btnLoadGame.Text = "载入存档";
            btnLoadGame.HoverSoundEffect = btnNewGame.HoverSoundEffect;
            btnLoadGame.LeftClick += BtnLoadGame_LeftClick;

            btnCancel = new XNAButton(WindowManager);
            btnCancel.Name = "btnCancel";
            btnCancel.ClientRectangle = new Rectangle(btnLoadGame.Right + 12,
                btnNewGame.Y, 133, 23);
            btnCancel.IdleTexture = btnNewGame.IdleTexture;
            btnCancel.HoverTexture = btnNewGame.HoverTexture;
            btnCancel.FontIndex = 1;
            btnCancel.Text = "取消";
            btnCancel.HoverSoundEffect = btnNewGame.HoverSoundEffect;
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(btnNewGame);
            AddChild(btnLoadGame);
            AddChild(btnCancel);

            base.Initialize();

            CenterOnParent();
        }

        private void BtnNewGame_LeftClick(object sender, EventArgs e)
        {
            Disable();
            NewGame?.Invoke(this, EventArgs.Empty);
        }

        private void BtnLoadGame_LeftClick(object sender, EventArgs e)
        {
            Disable();

            IniFile iniFile = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ProgramConstants.SAVED_GAME_SPAWN_INI));

            LoadGame?.Invoke(this, new GameLoadEventArgs(iniFile.GetIntValue("Settings", "GameID", -1)));
        }

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            Disable();
        }

        public void Open()
        {
            btnLoadGame.AllowClick = AllowLoadingGame();
            Enable();
        }

        private bool AllowLoadingGame()
        {
            FileInfo savedGameSpawnIniFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.SAVED_GAME_SPAWN_INI);

            if (!savedGameSpawnIniFile.Exists)
                return false;

            IniFile iniFile = new IniFile(savedGameSpawnIniFile.FullName);
            if (iniFile.GetStringValue("Settings", "Name", string.Empty) != ProgramConstants.PLAYERNAME)
                return false;

            if (!iniFile.GetBooleanValue("Settings", "Host", false))
                return false;

            // 不允许在局域网模式下加载CnCNet游戏
            if (iniFile.SectionExists("Tunnel"))
                return false;

            return true;
        }
    }

    public class GameLoadEventArgs : EventArgs
    {
        public GameLoadEventArgs(int loadedGameId)
        {
            LoadedGameID = loadedGameId;
        }

        public int LoadedGameID { get; private set; }
    }
}
