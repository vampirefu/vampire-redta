using ClientCore;
using ClientCore.Statistics;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClientCore.Enums;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.Online.EventArguments;
using DTAConfig;
using System.Security.Cryptography;
using DTAClient.DXGUI.Helpers;
using DTAClient.Domain.AI;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    /// <summary>
    /// 所有游戏大厅(遭遇战、局域网和CnCNet)的通用基类。
    /// 包含解析游戏选项和处理玩家信息的通用逻辑。
    /// </summary>
    public abstract class GameLobbyBase : INItializableWindow
    {
        protected const int MAX_PLAYER_COUNT = 8;
        protected const int PLAYER_OPTION_VERTICAL_MARGIN = 12;
        protected const int PLAYER_OPTION_HORIZONTAL_MARGIN = 3;
        protected const int PLAYER_OPTION_CAPTION_Y = 6;
        private const int DROP_DOWN_HEIGHT = 21;
        protected readonly string BTN_LAUNCH_GAME = "启动游戏";
        protected readonly string BTN_LAUNCH_READY = "准备";
        protected readonly string BTN_LAUNCH_NOT_READY = "取消准备";

        private readonly string FavoriteMapsLabel = "地图收藏夹";

        private const int RANK_NONE = 0;
        private const int RANK_EASY = 1;
        private const int RANK_MEDIUM = 2;
        private const int RANK_HARD = 3;

        /// <summary>
        /// 创建游戏大厅基类的新实例。
        /// </summary>
        /// <param name="windowManager"></param>
        /// <param name="iniName">GameOptions.ini中的大厅名称。</param>
        /// <param name="mapLoader"></param>
        /// <param name="isMultiplayer"></param>
        /// <param name="discordHandler"></param>
        public GameLobbyBase(
            WindowManager windowManager,
            string iniName,
            MapLoader mapLoader,
            bool isMultiplayer,
            DiscordHandler discordHandler
        ) : base(windowManager)
        {
            _iniSectionName = iniName;
            MapLoader = mapLoader;
            this.isMultiplayer = isMultiplayer;
            this.discordHandler = discordHandler;
        }

        private string _iniSectionName;

        protected XNAPanel PlayerOptionsPanel;

        protected List<MultiplayerColor> MPColors;

        public List<GameLobbyCheckBox> CheckBoxes { get; set; } = new List<GameLobbyCheckBox>();
        public List<GameLobbyDropDown> DropDowns { get; set; } = new List<GameLobbyDropDown>();

        protected DiscordHandler discordHandler;

        protected MapLoader MapLoader;
        /// <summary>
        /// 多人游戏模式地图列表。
        /// 每个都是特定游戏模式的地图实例。
        /// </summary>
        protected GameModeMapCollection GameModeMaps => MapLoader.GameModeMaps;

        protected GameModeMapFilter gameModeMapFilter;

        private GameModeMap _gameModeMap;

        /// <summary>
        /// 当前选中的游戏模式。
        /// </summary>
        protected GameModeMap GameModeMap
        {
            get => _gameModeMap;
            set
            {
                var oldGameModeMap = _gameModeMap;
                _gameModeMap = value;
                if (value != null && oldGameModeMap != value)
                    UpdateDiscordPresence();
            }
        }

        protected Map Map => GameModeMap?.Map;
        protected GameMode GameMode => GameModeMap?.GameMode;

        protected XNAClientDropDown[] ddPlayerNames;
        protected XNAClientDropDown[] ddPlayerSides;
        protected XNAClientDropDown[] ddPlayerColors;
        protected XNAClientDropDown[] ddPlayerStarts;
        protected XNAClientDropDown[] ddPlayerTeams;

        protected XNAClientButton btnPlayerExtraOptionsOpen;
        protected PlayerExtraOptionsPanel PlayerExtraOptionsPanel;

        protected XNAClientButton btnLeaveGame;
        protected GameLaunchButton btnLaunchGame;

        protected XNAClientButton btnPickRandomMap;
        protected XNAClientButton btnAginLoadMaps;
        protected XNAClientButton btnRandomMap;

        protected XNALabel lblMapName;
        protected XNALabel lblMapAuthor;
        protected XNALinkLabel lblPlayDescription;
        protected XNALabel lblGameMode;
        protected XNALabel lblMapSize;

        protected XNALabel lblscreen;
        protected XNADropDown ddPeople;

        protected GameLobbyDropDown cmbAI;

        protected MapPreviewBox MapPreviewBox;

        protected int count = 0;

        protected Rectangle MapPreviewBoxPosition;

        protected XNAMultiColumnListBox lbGameModeMapList;
        protected XNAClientDropDown ddGameModeMapFilter;
        protected XNALabel lblGameModeSelect;

        /// <summary>
        /// 游戏模式介绍
        /// 已隐藏
        /// </summary>
        protected XNALabel lblModeText;

        private GetRandomMap randomMap;

        protected XNAContextMenu mapContextMenu;
        private XNAContextMenuItem toggleFavoriteItem;

        protected XNAClientStateButton<SortDirection> btnMapSortAlphabetically;

        protected XNASuggestionTextBox tbMapSearch;

        protected List<PlayerInfo> Players = new List<PlayerInfo>();
        protected List<PlayerInfo> AIPlayers = new List<PlayerInfo>();

        protected virtual PlayerInfo FindLocalPlayer() => Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);

        protected bool PlayerUpdatingInProgress { get; set; }

        protected Texture2D[] RankTextures;

        /// <summary>
        /// 用于随机化玩家选项的种子。
        /// </summary>
        protected int RandomSeed { get; set; }

        /// <summary>
        /// 此游戏的唯一标识符。
        /// </summary>
        protected int UniqueGameID { get; set; }
        protected int SideCount { get; private set; }
        protected int RandomSelectorCount { get; private set; } = 1;

        protected List<int[]> RandomSelectors = new List<int[]>();

        private readonly bool isMultiplayer = false;

        private MatchStatistics matchStatistics;

        private bool disableGameOptionUpdateBroadcast = false;

        protected EventHandler<MultiplayerNameRightClickedEventArgs> MultiplayerNameRightClicked;

        /// <summary>
        /// 如果设置，客户端将在启动地图之前移除所有起始航点。
        /// </summary>
        protected bool RemoveStartingLocations { get; set; } = false;
        protected IniFile GameOptionsIni { get; private set; }

        protected XNAClientButton BtnSaveLoadGameOptions { get; set; }

        private XNAContextMenu loadSaveGameOptionsMenu { get; set; }

        private LoadOrSaveGameOptionPresetWindow loadOrSaveGameOptionPresetWindow;

        public override void Initialize()
        {
            Name = _iniSectionName;
            //if (WindowManager.RenderResolutionY < 800)
            //    ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);
            //else
            ClientRectangle = new Rectangle(0, 0, 1280, 768);
            // ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX - 60, WindowManager.RenderResolutionY - 32);
            WindowManager.CenterControlOnScreen(this);
            BackgroundTexture = AssetLoader.LoadTexture("gamelobbybg.png");

            RankTextures = new Texture2D[4]
            {
                AssetLoader.LoadTexture("rankNone.png"),
                AssetLoader.LoadTexture("rankEasy.png"),
                AssetLoader.LoadTexture("rankNormal.png"),
                AssetLoader.LoadTexture("rankHard.png")
            };

            MPColors = MultiplayerColor.LoadColors();

            GameOptionsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "GameOptions.ini"));

            base.Initialize();

            PlayerOptionsPanel = FindChild<XNAPanel>(nameof(PlayerOptionsPanel));

            btnLeaveGame = FindChild<XNAClientButton>(nameof(btnLeaveGame));

            btnLeaveGame.Text = "离开游戏";

            btnLeaveGame.LeftClick += BtnLeaveGame_LeftClick;

            MapPreviewBox = FindChild<MapPreviewBox>("MapPreviewBox");

            MapPreviewBoxPosition = MapPreviewBox.ClientRectangle;

            MapPreviewBox.SetFields(Players, AIPlayers, MPColors, GameOptionsIni.GetStringValue("General", "Sides", String.Empty).Split(','), GameOptionsIni);
            // MapPreviewBox.UpdateMap();
            MapPreviewBox.ToggleFavorite += MapPreviewBox_ToggleFavorite;

            MapPreviewBox.LeftClick += MapPreviewBox_LeftClick;

            btnLaunchGame = FindChild<GameLaunchButton>(nameof(btnLaunchGame));
            btnLaunchGame.LeftClick += BtnLaunchGame_LeftClick;
            btnLaunchGame.InitStarDisplay(RankTextures);

            lblMapName = FindChild<XNALabel>(nameof(lblMapName));
            lblMapAuthor = FindChild<XNALabel>(nameof(lblMapAuthor));

            //新增label:玩法介绍
            lblPlayDescription = new XNALinkLabel(WindowManager);
            AddChild(lblPlayDescription);
            lblPlayDescription.ClientRectangle = new Rectangle(lblMapName.ClientRectangle.X + MapPreviewBox.ClientRectangle.Width / 2, lblMapName.ClientRectangle.Y, lblMapAuthor.ClientRectangle.Width, lblMapAuthor.ClientRectangle.Height);
            lblPlayDescription.Text = "玩法介绍";

            //新增逻辑：增加玩法介绍交互界面
            PlayDescriptionWindow playDescriptionwin = new PlayDescriptionWindow(WindowManager);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, playDescriptionwin);
            playDescriptionwin.Disable();
            lblPlayDescription.LeftClick += (s, e) =>
            {
                playDescriptionwin.AddDescription(Map?.PlayDescription);
                playDescriptionwin.CenterOnParent();
                playDescriptionwin.Enable();
            };

            lblGameMode = FindChild<XNALabel>(nameof(lblGameMode));
            lblMapSize = FindChild<XNALabel>(nameof(lblMapSize));

            lbGameModeMapList = FindChild<XNAMultiColumnListBox>("lbMapList"); //lbMapList用于向后兼容
            lbGameModeMapList.SelectedIndexChanged += LbGameModeMapList_SelectedIndexChanged;
            lbGameModeMapList.RightClick += LbGameModeMapList_RightClick;
            lbGameModeMapList.AllowKeyboardInput = true; //!isMultiplayer
            lbGameModeMapList.LineHeight = 25; //行间距扩大
            lbGameModeMapList.FontIndex = 1;

            cmbAI = FindChild<GameLobbyDropDown>(nameof(cmbAI));

            mapContextMenu = new XNAContextMenu(WindowManager);
            mapContextMenu.Name = nameof(mapContextMenu);
            mapContextMenu.Width = 100;
            mapContextMenu.AddItem("删除地图", DeleteMapConfirmation, null, CanDeleteMap);
            toggleFavoriteItem = new XNAContextMenuItem
            {
                Text = "最爱",
                SelectAction = ToggleFavoriteMap
            };
            mapContextMenu.AddItem(toggleFavoriteItem);
            AddChild(mapContextMenu);

            XNAPanel rankHeader = new XNAPanel(WindowManager);
            rankHeader.BackgroundTexture = AssetLoader.LoadTexture("rank.png");
            rankHeader.ClientRectangle = new Rectangle(0, 0, rankHeader.BackgroundTexture.Width,
                19);

            XNAListBox rankListBox = new XNAListBox(WindowManager);
            rankListBox.TextBorderDistance = 2;

            lbGameModeMapList.AddColumn(rankHeader, rankListBox);
            lbGameModeMapList.AddColumn("地图名称", lbGameModeMapList.Width - RankTextures[1].Width - 3);

            ddGameModeMapFilter = FindChild<XNAClientDropDown>("ddGameMode"); // ddGameMode用于向后兼容
            ddGameModeMapFilter.SelectedIndexChanged += DdGameModeMapFilter_SelectedIndexChanged;

            ddGameModeMapFilter.AddItem(CreateGameFilterItem(FavoriteMapsLabel, new GameModeMapFilter(GetFavoriteGameModeMaps)));
            foreach (GameMode gm in GameModeMaps.GameModes)
                ddGameModeMapFilter.AddItem(CreateGameFilterItem(gm.UIName, new GameModeMapFilter(GetGameModeMaps(gm))));

            lblGameModeSelect = FindChild<XNALabel>(nameof(lblGameModeSelect));
            // lblModeText = FindChild<XNALabel>(nameof(lblModeText));

            InitBtnMapSort();

            tbMapSearch = FindChild<XNASuggestionTextBox>(nameof(tbMapSearch));
            tbMapSearch.InputReceived += TbMapSearch_InputReceived;
            tbMapSearch.Suggestion = "搜索地图...";

            btnPickRandomMap = FindChild<XNAClientButton>(nameof(btnPickRandomMap));
            btnPickRandomMap.LeftClick += BtnPickRandomMap_LeftClick;

            btnAginLoadMaps = new XNAClientButton(WindowManager);
            //   btnAginLoadMaps = FindChild<XNAClientButton>(nameof(btnAginLoadMaps));
            btnAginLoadMaps.IdleTexture = AssetLoader.LoadTexture("133pxtab.png");
            btnAginLoadMaps.HoverTexture = AssetLoader.LoadTexture("133pxtab_c.png");
            btnAginLoadMaps.Text = "刷新列表";
            btnAginLoadMaps.ClientRectangle = new Rectangle(btnLaunchGame.X, lbGameModeMapList.Y - 35, btnLaunchGame.Width - 20, btnLaunchGame.Height);
            btnAginLoadMaps.LeftClick += btnAginLoadMaps_LeftClick;
            AddChild(btnAginLoadMaps);

            lblscreen = new XNALabel(WindowManager);
            lblscreen.Name = nameof(lblscreen);
            lblscreen.Text = "人数";
            lblscreen.ClientRectangle = new Rectangle(btnAginLoadMaps.X + btnAginLoadMaps.Width + 10, btnAginLoadMaps.Y + 5, 0, 0);
            AddChild(lblscreen);

            ddPeople = new XNADropDown(WindowManager);
            ddPeople.Name = nameof(ddPeople);
            ddPeople.ClientRectangle = new Rectangle(lblscreen.X + 100, lblscreen.Y, 60, 25);
            AddChild(ddPeople);

            ddPeople.AddItem("-");

            for (int i = 2; i <= 8; i++)
            {
                ddPeople.AddItem(i.ToString());
            }

            ddPeople.SelectedIndex = 0;
            ddPeople.SelectedIndexChanged += DdPeople_SelectedIndexChanged;

            lblModeText = new XNALabel(WindowManager);
            lblModeText.Name = nameof(lblModeText);
            lblModeText.ClientRectangle = new Rectangle(btnAginLoadMaps.X + 130, btnAginLoadMaps.Y - 20, 0, 0);
            AddChild(lblModeText);

            randomMap = new GetRandomMap(WindowManager, MapLoader);
            AddAndInitializeWithControl(WindowManager, randomMap);
            randomMap.Disable();
            randomMap.EnabledChanged += randomMap_EnabledChanged;


            btnRandomMap = new XNAClientButton(WindowManager);
            // btnRandomMap = FindChild<XNAClientButton>(nameof(btnRandomMap));
            btnRandomMap.IdleTexture = AssetLoader.LoadTexture("133pxtab.png");
            btnRandomMap.HoverTexture = AssetLoader.LoadTexture("133pxtab_c.png");
            btnRandomMap.Text = "生成地图";
            btnRandomMap.Disable();
            btnRandomMap.ClientRectangle = new Rectangle(btnLaunchGame.X + 150, btnLaunchGame.Y, btnLaunchGame.Width, btnLaunchGame.Height);
            btnRandomMap.LeftClick += (sender, s) => randomMap.Enable();
            AddChild(btnRandomMap);
            CheckBoxes.ForEach(chk => chk.CheckedChanged += ChkBox_CheckedChanged);
            DropDowns.ForEach(dd => dd.SelectedIndexChanged += Dropdown_SelectedIndexChanged);

            RemoveChild(MapPreviewBox);

            AddChildWithoutInitialize(MapPreviewBox);
            InitializeGameOptionPresetUI();

            //屏蔽游戏说明
            lblModeText.Visible = false;
        }


        private void MapPreviewBox_LeftClick(object sender, EventArgs e)
        {

            if (count % 2 == 0)

                MapPreviewBox.ClientRectangle = new Rectangle(0, 0, 1280, 768);
            else

                MapPreviewBox.ClientRectangle = MapPreviewBoxPosition;

            count++;

            base.OnLeftClick();

        }
        private void DdPeople_SelectedIndexChanged(object sender, EventArgs e)
        {
            gameModeMapFilter = ddPeople.SelectedIndex != 0
                ? new GameModeMapFilter(GetPeopleGameModeMaps(ddGameModeMapFilter.SelectedItem.Text, ddPeople.SelectedIndex + 1))
                : ddGameModeMapFilter.SelectedItem.Tag as GameModeMapFilter;

            ListMaps();

        }

        /// <summary>
        /// 在GUICreator能够处理类型化类之前，这必须保持手动完成。
        /// </summary>
        private void InitBtnMapSort()
        {
            btnMapSortAlphabetically = new XNAClientStateButton<SortDirection>(WindowManager, new Dictionary<SortDirection, Texture2D>()
            {
                { SortDirection.None, AssetLoader.LoadTexture("sortAlphaNone.png") },
                { SortDirection.Asc, AssetLoader.LoadTexture("sortAlphaAsc.png") },
                { SortDirection.Desc, AssetLoader.LoadTexture("sortAlphaDesc.png") },
            });
            btnMapSortAlphabetically.Name = nameof(btnMapSortAlphabetically);
            btnMapSortAlphabetically.ClientRectangle = new Rectangle(
                ddGameModeMapFilter.X + -ddGameModeMapFilter.Height - 4, ddGameModeMapFilter.Y,
                ddGameModeMapFilter.Height, ddGameModeMapFilter.Height
            );
            btnMapSortAlphabetically.LeftClick += BtnMapSortAlphabetically_LeftClick;
            btnMapSortAlphabetically.SetToolTipText("按名称排列");
            RefreshMapSortAlphabeticallyBtn();
            AddChild(btnMapSortAlphabetically);

            // 允许在INI中重新定位/禁用。
            ReadINIForControl(btnMapSortAlphabetically);
        }

        private void InitializeGameOptionPresetUI()
        {
            BtnSaveLoadGameOptions = FindChild<XNAClientButton>(nameof(BtnSaveLoadGameOptions), true);

            if (BtnSaveLoadGameOptions != null)
            {
                loadOrSaveGameOptionPresetWindow = new LoadOrSaveGameOptionPresetWindow(WindowManager);
                loadOrSaveGameOptionPresetWindow.Name = nameof(loadOrSaveGameOptionPresetWindow);
                loadOrSaveGameOptionPresetWindow.PresetLoaded += (sender, s) => HandleGameOptionPresetLoadCommand(s);
                loadOrSaveGameOptionPresetWindow.PresetSaved += (sender, s) => HandleGameOptionPresetSaveCommand(s);
                loadOrSaveGameOptionPresetWindow.Disable();
                var loadConfigMenuItem = new XNAContextMenuItem()
                {
                    Text = "加载",
                    SelectAction = () => loadOrSaveGameOptionPresetWindow.Show(true)
                };
                var saveConfigMenuItem = new XNAContextMenuItem()
                {
                    Text = "保存",
                    SelectAction = () => loadOrSaveGameOptionPresetWindow.Show(false)
                };

                loadSaveGameOptionsMenu = new XNAContextMenu(WindowManager);
                loadSaveGameOptionsMenu.Name = nameof(loadSaveGameOptionsMenu);
                loadSaveGameOptionsMenu.ClientRectangle = new Rectangle(0, 0, 75, 0);
                loadSaveGameOptionsMenu.Items.Add(loadConfigMenuItem);
                loadSaveGameOptionsMenu.Items.Add(saveConfigMenuItem);

                BtnSaveLoadGameOptions.LeftClick += (sender, args) =>
                    loadSaveGameOptionsMenu.Open(GetCursorPoint());

                AddChild(loadSaveGameOptionsMenu);
                AddChild(loadOrSaveGameOptionPresetWindow);
            }
        }

        public static void AddAndInitializeWithControl(WindowManager wm, XNAControl control)
        {
            var dp = new DarkeningPanel(wm);
            wm.AddAndInitializeControl(dp);
            dp.AddChild(control);
        }

        private void BtnMapSortAlphabetically_LeftClick(object sender, EventArgs e)
        {
            UserINISettings.Instance.MapSortState.Value = (int)btnMapSortAlphabetically.GetState();

            RefreshMapSortAlphabeticallyBtn();
            UserINISettings.Instance.SaveSettings();
            ListMaps();
        }

        private void RefreshMapSortAlphabeticallyBtn()
        {
            if (Enum.IsDefined(typeof(SortDirection), UserINISettings.Instance.MapSortState.Value))
                btnMapSortAlphabetically.SetState((SortDirection)UserINISettings.Instance.MapSortState.Value);
        }

        private static XNADropDownItem CreateGameFilterItem(string text, GameModeMapFilter filter)
        {
            return new XNADropDownItem
            {
                Text = text,
                Tag = filter
            };
        }

        protected bool IsFavoriteMapsSelected() => ddGameModeMapFilter.SelectedItem?.Text == FavoriteMapsLabel;

        private List<GameModeMap> GetFavoriteGameModeMaps() =>
            GameModeMaps.Where(gmm => gmm.IsFavorite).ToList();

        private Func<List<GameModeMap>> GetGameModeMaps(GameMode gm) => () =>
            GameModeMaps.Where(gmm => gmm.GameMode == gm).ToList();

        private Func<List<GameModeMap>> GetPeopleGameModeMaps(string gm, int i) => () =>
            GameModeMaps.Where(gmm => gmm.Map.MaxPlayers == i && gmm.GameMode.UIName == gm).ToList();

        private void RefreshBtnPlayerExtraOptionsOpenTexture()
        {
            if (btnPlayerExtraOptionsOpen != null)
            {
                var textureName = GetPlayerExtraOptions().IsDefault() ? "optionsButton.png" : "optionsButtonActive.png";
                var hoverTextureName = GetPlayerExtraOptions().IsDefault() ? "optionsButton_c.png" : "optionsButtonActive_c.png";
                var hoverTexture = AssetLoader.AssetExists(hoverTextureName) ? AssetLoader.LoadTexture(hoverTextureName) : null;
                btnPlayerExtraOptionsOpen.IdleTexture = AssetLoader.LoadTexture(textureName);
                btnPlayerExtraOptionsOpen.HoverTexture = hoverTexture;
            }
        }

        protected void HandleGameOptionPresetSaveCommand(GameOptionPresetEventArgs e) => HandleGameOptionPresetSaveCommand(e.PresetName);

        protected void HandleGameOptionPresetSaveCommand(string presetName)
        {
            string error = AddGameOptionPreset(presetName);
            if (!string.IsNullOrEmpty(error))
                AddNotice(error);
        }

        protected void HandleGameOptionPresetLoadCommand(GameOptionPresetEventArgs e) => HandleGameOptionPresetLoadCommand(e.PresetName);

        protected void HandleGameOptionPresetLoadCommand(string presetName)
        {
            if (LoadGameOptionPreset(presetName))
                AddNotice("游戏预设加载成功.");
            else
                AddNotice(string.Format("预设{0}未找到!", presetName));
        }

        protected void AddNotice(string message) => AddNotice(message, Color.White);

        protected abstract void AddNotice(string message, Color color);

        private void BtnPickRandomMap_LeftClick(object sender, EventArgs e) => PickRandomMap();

        private void btnAginLoadMaps_LeftClick(object sender, EventArgs e)
        {
            MapLoader.AgainLoadMaps();

            ddGameModeMapFilter.Items.Clear();

            ddGameModeMapFilter.AddItem(CreateGameFilterItem(FavoriteMapsLabel, new GameModeMapFilter(GetFavoriteGameModeMaps)));
            foreach (GameMode gm in GameModeMaps.GameModes)
            {
                ddGameModeMapFilter.AddItem(CreateGameFilterItem(gm.UIName, new GameModeMapFilter(GetGameModeMaps(gm))));

            }

            MapPreviewBox.UpdateMap();
            int i = ddGameModeMapFilter.SelectedIndex;
            ddGameModeMapFilter.SelectedIndex = 0;
            ddGameModeMapFilter.SelectedIndex = i;

        }

        private void randomMap_EnabledChanged(object sender, EventArgs e)
        {
            if (randomMap.Enabled == false && randomMap.GetIsSave())
            {

                btnAginLoadMaps.OnLeftClick();

                ddGameModeMapFilter.SelectedIndex = ddGameModeMapFilter.Items.FindIndex(d => d.Text == "常规作战" || d.Text == "Standard");

                for (int i = 0; i < lbGameModeMapList.ItemCount; i++)
                {
                    if (lbGameModeMapList.GetItem(1, i).Text == "随机地图")
                    {
                        lbGameModeMapList.SelectedIndex = i;
                        break;
                    }
                }
            }

        }

        private void TbMapSearch_InputReceived(object sender, EventArgs e) => ListMaps();

        private void Dropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (disableGameOptionUpdateBroadcast)
                return;

            var dd = (GameLobbyDropDown)sender;
            dd.HostSelectedIndex = dd.SelectedIndex;
            OnGameOptionChanged();

            if (dd.ControlName != null && dd.ControlIndex != null && dd.ControlIndex.Count == dd.ControlIndex.Count)
            {

                List<string> ControlName = dd.ControlName[dd.SelectedIndex].Split('|').ToList();
                List<string> ControlIndex = dd.ControlIndex[dd.SelectedIndex].Split('|').ToList();

                for (int i = 0; i < ControlName.Count; i++)
                {

                    if (!string.IsNullOrEmpty(dd.ControlIndex[dd.SelectedIndex]))
                    {
                        GameLobbyCheckBox otherChk = CheckBoxes.Find(chk => chk.Name == ControlName[i]);
                        if (otherChk != null)
                        {
                            otherChk.Checked = Convert.ToInt32(ControlIndex[i]) != 0;
                        }
                        else
                        {
                            GameLobbyDropDown otherDd = DropDowns.Find(dd => dd.Name == ControlName[i]);
                            otherDd.SelectedIndex = Convert.ToInt32(ControlIndex[i]);
                        }
                    }
                }

            }
        }


        private void ChkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (disableGameOptionUpdateBroadcast)
                return;

            var checkBox = (GameLobbyCheckBox)sender;


            if (checkBox.ControlName != null && checkBox.ControlIndex != null && checkBox.ControlIndex.Count == checkBox.ControlIndex.Count)
            {
                for (int i = 0; i < checkBox.ControlName.Count; i++)
                {

                    if (checkBox.Checked)
                    {
                        GameLobbyCheckBox otherChk = CheckBoxes.Find(chk => chk.Name == checkBox.ControlName[i]);
                        if (otherChk != null)
                            otherChk.Checked = Convert.ToInt32(checkBox.ControlIndex[i]) == 0 ? false : true;
                        else
                        {
                            GameLobbyDropDown otherDd = DropDowns.Find(dd => dd.Name == checkBox.ControlName[i]);
                            otherDd.SelectedIndex = Convert.ToInt32(checkBox.ControlIndex[i]);
                        }
                    }
                }
            }
            checkBox.HostChecked = checkBox.Checked;
            OnGameOptionChanged();

        }

        protected virtual void OnGameOptionChanged()
        {
            CheckDisallowedSides();

            btnLaunchGame.SetRank(GetRank());
        }

        protected void DdGameModeMapFilter_SelectedIndexChanged(object sender, EventArgs e)
        {

            //gameModeMapFilter = ddGameModeMapFilter.SelectedItem.Tag as GameModeMapFilter;

            gameModeMapFilter = ddPeople.SelectedIndex != 0
                ? new GameModeMapFilter(GetPeopleGameModeMaps(ddGameModeMapFilter.SelectedItem.Text, ddPeople.SelectedIndex + 1))
                : ddGameModeMapFilter.SelectedItem.Tag as GameModeMapFilter;

            tbMapSearch.Text = string.Empty;
            tbMapSearch.OnSelectedChanged();

            ListMaps();

            if (lbGameModeMapList.SelectedIndex == -1)
                lbGameModeMapList.SelectedIndex = 0; // 选择默认GameModeMap
            else
                ChangeMap(GameModeMap);

            if (GameModeMap != null)
            {
                lblModeText.Text = GameModeMap.GameMode.modeText;
            }
            else
            {
                lblModeText.Text = string.Empty;
            }

        }

        protected void BtnPlayerExtraOptions_LeftClick(object sender, EventArgs e)
        {
            if (PlayerExtraOptionsPanel.Enabled)
                PlayerExtraOptionsPanel.Disable();
            else
                PlayerExtraOptionsPanel.Enable();
        }

        protected void ApplyPlayerExtraOptions(string sender, string message)
        {
            var playerExtraOptions = PlayerExtraOptions.FromMessage(message);

            if (playerExtraOptions.IsForceRandomSides != PlayerExtraOptionsPanel.IsForcedRandomSides())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomSides, "阵营选择");

            if (playerExtraOptions.IsForceRandomColors != PlayerExtraOptionsPanel.IsForcedRandomColors())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomColors, "颜色选择");

            if (playerExtraOptions.IsForceRandomStarts != PlayerExtraOptionsPanel.IsForcedRandomStarts())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomStarts, "起始位置选择");

            if (playerExtraOptions.IsForceRandomTeams != PlayerExtraOptionsPanel.IsForcedRandomTeams())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomTeams, "队伍选择");

            if (playerExtraOptions.IsUseTeamStartMappings != PlayerExtraOptionsPanel.IsUseTeamStartMappings())
                AddPlayerExtraOptionForcedNotice(!playerExtraOptions.IsUseTeamStartMappings, "自动结盟");

            SetPlayerExtraOptions(playerExtraOptions);
            UpdateMapPreviewBoxEnabledStatus();
        }

        private void AddPlayerExtraOptionForcedNotice(bool disabled, string type)
            => AddNotice(disabled ?
                string.Format("游戏主持禁用了{0}选项", type) :
                string.Format("游戏主持启用了{0}选项", type));

        private List<GameModeMap> GetSortedGameModeMaps()
        {
            var gameModeMaps = gameModeMapFilter.GetGameModeMaps();

            // 仅当地图列表排序按钮可用时才应用排序。
            if (btnMapSortAlphabetically.Enabled && btnMapSortAlphabetically.Visible)
            {
                switch ((SortDirection)UserINISettings.Instance.MapSortState.Value)
                {
                    case SortDirection.Asc:
                        gameModeMaps = gameModeMaps.OrderBy(gmm => gmm.Map.Name).ToList();
                        break;
                    case SortDirection.Desc:
                        gameModeMaps = gameModeMaps.OrderByDescending(gmm => gmm.Map.Name).ToList();
                        break;
                }
            }

            return gameModeMaps;
        }

        protected void ListMaps()
        {
            lbGameModeMapList.SelectedIndexChanged -= LbGameModeMapList_SelectedIndexChanged;

            lbGameModeMapList.ClearItems();
            lbGameModeMapList.SetTopIndex(0);

            lbGameModeMapList.SelectedIndex = -1;

            int mapIndex = -1;
            int skippedMapsCount = 0;

            var isFavoriteMapsSelected = IsFavoriteMapsSelected();
            var maps = GetSortedGameModeMaps();

            maps = maps.OrderBy(o => o.Map.MaxPlayers).ToList();

            for (int i = 0; i < maps.Count; i++)
            {


                var gameModeMap = maps[i];

                if (tbMapSearch.Text != tbMapSearch.Suggestion)
                {
                    if (!gameModeMap.Map.Name.ToUpper().Contains(tbMapSearch.Text.ToUpper()))
                    {
                        skippedMapsCount++;
                        continue;
                    }
                }

                XNAListBoxItem rankItem = new XNAListBoxItem();
                if (gameModeMap.Map.IsCoop)
                {
                    if (StatisticsManager.Instance.HasBeatCoOpMap(gameModeMap.Map.Name, gameModeMap.GameMode.UIName))
                        rankItem.Texture = RankTextures[Math.Abs(2 - gameModeMap.GameMode.CoopDifficultyLevel) + 1];
                    else
                        rankItem.Texture = RankTextures[0];
                }
                else
                {

                    rankItem.Texture = RankTextures[GetDefaultMapRankIndex(gameModeMap) + 1];
                }

                XNAListBoxItem mapNameItem = new XNAListBoxItem();
                var mapNameText = gameModeMap.Map.Name;
                if (isFavoriteMapsSelected)
                    mapNameText += $" - {gameModeMap.GameMode.UIName}";

                mapNameItem.Text = Renderer.GetSafeString(mapNameText, lbGameModeMapList.FontIndex);

                if ((gameModeMap.Map.MultiplayerOnly || gameModeMap.GameMode.MultiplayerOnly) && !isMultiplayer)
                    mapNameItem.TextColor = UISettings.ActiveSettings.DisabledItemColor;
                mapNameItem.Tag = gameModeMap;

                XNAListBoxItem[] mapInfoArray = {
                    rankItem,
                    mapNameItem,
                };

                lbGameModeMapList.AddItem(mapInfoArray);

                if (gameModeMap == GameModeMap)
                    mapIndex = i - skippedMapsCount;
            }


            //    foreach (XNAListBoxItem[] mapInfoArray in maplist)
            //    {
            //       lbGameModeMapList.AddItem(mapInfoArray);
            //  }

            if (mapIndex > -1)
            {
                lbGameModeMapList.SelectedIndex = mapIndex;
                while (mapIndex > lbGameModeMapList.LastIndex)
                    lbGameModeMapList.TopIndex++;
            }

            lbGameModeMapList.SelectedIndexChanged += LbGameModeMapList_SelectedIndexChanged;
        }

        protected abstract int GetDefaultMapRankIndex(GameModeMap gameModeMap);

        private void LbGameModeMapList_RightClick(object sender, EventArgs e)
        {
            if (lbGameModeMapList.HoveredIndex < 0 || lbGameModeMapList.HoveredIndex >= lbGameModeMapList.ItemCount)
                return;

            lbGameModeMapList.SelectedIndex = lbGameModeMapList.HoveredIndex;

            if (!mapContextMenu.Items.Any(i => i.VisibilityChecker == null || i.VisibilityChecker()))
                return;

            toggleFavoriteItem.Text = GameModeMap.IsFavorite ? "取消收藏" : "添加收藏";

            mapContextMenu.Open(GetCursorPoint());
        }

        private bool CanDeleteMap()
        {
            return Map != null && !Map.Official && !isMultiplayer;
        }

        private void DeleteMapConfirmation()
        {
            if (Map == null)
                return;

            var messageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "删除确认",
                string.Format("您确定要删除自定义地图{0}?", Map.Name));
            messageBox.YesClickedAction = DeleteSelectedMap;
        }

        private void MapPreviewBox_ToggleFavorite(object sender, EventArgs e) =>
            ToggleFavoriteMap();

        protected virtual void ToggleFavoriteMap()
        {
            GameModeMap.IsFavorite = UserINISettings.Instance.ToggleFavoriteMap(Map.Name, GameMode.Name, GameModeMap.IsFavorite);
            MapPreviewBox.RefreshFavoriteBtn();
        }

        protected void RefreshForFavoriteMapRemoved()
        {
            if (!gameModeMapFilter.GetGameModeMaps().Any())
            {
                LoadDefaultGameModeMap();
                return;
            }

            ListMaps();
            if (IsFavoriteMapsSelected())
                lbGameModeMapList.SelectedIndex = 0; // 在查看收藏夹时地图已被移除
        }

        private void DeleteSelectedMap(XNAMessageBox messageBox)
        {
            try
            {
                MapLoader.DeleteCustomMap(GameModeMap);

                tbMapSearch.Text = string.Empty;
                if (GameMode.Maps.Count == 0)
                {
                    // 这将触发选择另一个游戏模式
                    GameModeMap = GameModeMaps.Find(gm => gm.GameMode.Maps.Count > 0);
                }
                else
                {
                    // 这将触发选择另一个地图
                    lbGameModeMapList.SelectedIndex = lbGameModeMapList.SelectedIndex == 0 ? 1 : lbGameModeMapList.SelectedIndex - 1;
                }

                ListMaps();
                ChangeMap(GameModeMap);
            }
            catch (IOException ex)
            {
                Logger.Log($"Deleting map {Map.BaseFilePath} failed! Message: {ex.Message}");
                XNAMessageBox.Show(WindowManager, "删除失败",
                    "删除地图失败!原因:" + " " + ex.Message);
            }
        }

        private bool _lastchkDefenceAiTriggerChecked = false;
        private void LbGameModeMapList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbGameModeMapList.SelectedIndex < 0 || lbGameModeMapList.SelectedIndex >= lbGameModeMapList.ItemCount)
            {
                ChangeMap(null);
                return;
            }

            XNAListBoxItem item = lbGameModeMapList.GetItem(1, lbGameModeMapList.SelectedIndex);

            GameModeMap = (GameModeMap)item.Tag;

            ChangeMap(GameModeMap);

            //补充逻辑：判断当前地图是否为防守地图来确定chkDefenceAiTrigger是否显示
            bool isShow = DefenceAiHelper.IsShowCKH(GameModeMap.Map.BaseFilePath);
            var chkDefenceAiTrigger = CheckBoxes.FirstOrDefault(p => p.Name == "chkDefenceAiTrigger");
            if (chkDefenceAiTrigger != null)
            {
                if (isShow)
                {
                    chkDefenceAiTrigger.Visible = true;
                    chkDefenceAiTrigger.Checked = _lastchkDefenceAiTriggerChecked;
                }
                else
                {
                    chkDefenceAiTrigger.Visible = false;
                    _lastchkDefenceAiTriggerChecked = chkDefenceAiTrigger.Checked;
                    chkDefenceAiTrigger.Checked = false;
                }
            }

            //补充逻辑：判断是否显示地图玩法
            if (string.IsNullOrEmpty(GameModeMap.Map?.PlayDescription))
                lblPlayDescription.Visible = false;
            else
                lblPlayDescription.Visible = true;
        }

        private void PickRandomMap()
        {
            int totalPlayerCount = Players.Count(p => p.SideId < ddPlayerSides[0].Items.Count - 1)
                   + AIPlayers.Count;
            List<Map> maps = GetMapList(totalPlayerCount);
            if (maps.Count < 1)
                return;

            int random = new Random().Next(0, maps.Count);
            GameModeMap = GameModeMaps.Find(gmm => gmm.GameMode == GameMode && gmm.Map == maps[random]);

            Logger.Log("PickRandomMap: Rolled " + random + " out of " + maps.Count + ". Picked map: " + Map.Name);

            ChangeMap(GameModeMap);
            tbMapSearch.Text = string.Empty;
            tbMapSearch.OnSelectedChanged();
            ListMaps();
        }

        private List<Map> GetMapList(int playerCount)
        {
            List<Map> mapList = (GameMode?.Maps.Where(x => x.MaxPlayers == playerCount) ?? Array.Empty<Map>()).ToList();
            if (mapList.Count < 1 && playerCount <= MAX_PLAYER_COUNT)
                return GetMapList(playerCount + 1);
            else
                return mapList;
        }

        /// <summary>
        /// 刷新地图选择UI以匹配当前选中的地图和游戏模式。
        /// </summary>
        protected void RefreshMapSelectionUI()
        {
            if (GameMode == null)
                return;

            int gameModeMapFilterIndex = ddGameModeMapFilter.Items.FindIndex(i => i.Text == GameMode.UIName);

            if (gameModeMapFilterIndex == -1)
                return;

            if (ddGameModeMapFilter.SelectedIndex == gameModeMapFilterIndex)
                DdGameModeMapFilter_SelectedIndexChanged(this, EventArgs.Empty);

            ddGameModeMapFilter.SelectedIndex = gameModeMapFilterIndex;
        }

        bool SelectedIndexChangedFlag = false;
        /// <summary>
        /// 初始化玩家选项下拉控件。
        /// </summary>
        protected void InitPlayerOptionDropdowns()
        {
            ddPlayerNames = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerSides = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerColors = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerStarts = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerTeams = new XNAClientDropDown[MAX_PLAYER_COUNT];

            int playerOptionVecticalMargin = ConfigIni.GetIntValue(Name, "PlayerOptionVerticalMargin", PLAYER_OPTION_VERTICAL_MARGIN);
            int playerOptionHorizontalMargin = ConfigIni.GetIntValue(Name, "PlayerOptionHorizontalMargin", PLAYER_OPTION_HORIZONTAL_MARGIN);
            int playerOptionCaptionLocationY = ConfigIni.GetIntValue(Name, "PlayerOptionCaptionLocationY", PLAYER_OPTION_CAPTION_Y);
            int playerNameWidth = ConfigIni.GetIntValue(Name, "PlayerNameWidth", 136);
            int sideWidth = ConfigIni.GetIntValue(Name, "SideWidth", 91);
            int colorWidth = ConfigIni.GetIntValue(Name, "ColorWidth", 79);
            int startWidth = ConfigIni.GetIntValue(Name, "StartWidth", 49);
            int teamWidth = ConfigIni.GetIntValue(Name, "TeamWidth", 46);
            int locationX = ConfigIni.GetIntValue(Name, "PlayerOptionLocationX", 25);
            int locationY = ConfigIni.GetIntValue(Name, "PlayerOptionLocationY", 24);

            // InitPlayerOptionDropdowns(136, 91, 79, 49, 46, new Point(25, 24));

            string[] sides = ClientConfiguration.Instance.Sides.Split(',');
            SideCount = sides.Length;

            List<string> selectorNames = new List<string>();
            GetRandomSelectors(selectorNames, RandomSelectors);
            RandomSelectorCount = RandomSelectors.Count + 1;
            MapPreviewBox.RandomSelectorCount = RandomSelectorCount;

            string randomColor = GameOptionsIni.GetStringValue("General", "RandomColor", "255,255,255");

            for (int i = MAX_PLAYER_COUNT - 1; i > -1; i--)
            {
                var ddPlayerName = new XNAClientDropDown(WindowManager);
                ddPlayerName.Name = "ddPlayerName" + i;
                ddPlayerName.ClientRectangle = new Rectangle(locationX,
                    locationY + (DROP_DOWN_HEIGHT + playerOptionVecticalMargin) * i,
                    playerNameWidth, DROP_DOWN_HEIGHT);
                ddPlayerName.AddItem(String.Empty);
                ProgramConstants.AI_PLAYER_NAMES.ForEach(ddPlayerName.AddItem);
                ddPlayerName.AllowDropDown = true;
                ddPlayerName.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerName.RightClick += MultiplayerName_RightClick;
                ddPlayerName.Tag = true;

                var ddPlayerSide = new XNAClientDropDown(WindowManager);
                ddPlayerSide.Name = "ddPlayerSide" + i;
                ddPlayerSide.ClientRectangle = new Rectangle(
                    ddPlayerName.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, sideWidth, DROP_DOWN_HEIGHT);
                ddPlayerSide.AddItem("随机国家与阵营", LoadTextureOrNull("randomicon.png"));
                foreach (string randomSelector in selectorNames)
                    ddPlayerSide.AddItem(randomSelector, LoadTextureOrNull(randomSelector + "icon.png"));
                foreach (string sideName in sides)
                    ddPlayerSide.AddItem(sideName, LoadTextureOrNull(sideName + "icon.png"));
                ddPlayerSide.AllowDropDown = false;
                ddPlayerSide.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerSide.Tag = true;

                var ddPlayerColor = new XNAClientDropDown(WindowManager);
                ddPlayerColor.Name = "ddPlayerColor" + i;
                ddPlayerColor.ClientRectangle = new Rectangle(
                    ddPlayerSide.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, colorWidth, DROP_DOWN_HEIGHT);
                ddPlayerColor.AddItem("随机", AssetLoader.GetColorFromString(randomColor));
                foreach (MultiplayerColor mpColor in MPColors)
                    ddPlayerColor.AddItem(mpColor.Name, mpColor.XnaColor);
                ddPlayerColor.AllowDropDown = false;
                ddPlayerColor.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerColor.Tag = false;

                var ddPlayerTeam = new XNAClientDropDown(WindowManager);
                ddPlayerTeam.Name = "ddPlayerTeam" + i;
                ddPlayerTeam.ClientRectangle = new Rectangle(
                    ddPlayerColor.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, teamWidth, DROP_DOWN_HEIGHT);
                ddPlayerTeam.AddItem("-");
                ProgramConstants.TEAMS.ForEach(ddPlayerTeam.AddItem);
                ddPlayerTeam.AllowDropDown = false;
                ddPlayerTeam.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerTeam.Tag = true;

                var ddPlayerStart = new XNAClientDropDown(WindowManager);
                ddPlayerStart.Name = "ddPlayerStart" + i;
                ddPlayerStart.ClientRectangle = new Rectangle(
                    ddPlayerTeam.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, startWidth, DROP_DOWN_HEIGHT);
                for (int j = 1; j < 9; j++)
                    ddPlayerStart.AddItem(j.ToString());
                ddPlayerStart.AllowDropDown = false;
                ddPlayerStart.SelectedIndexChanged += CopyPlayerDataFromUI;
                //新逻辑：避免重复选择位置
                ddPlayerStart.SelectedIndexChanged += (sender, e) =>
                {
                    if (PlayerUpdatingInProgress)
                        return;

                    if (SelectedIndexChangedFlag)
                        return;

                    //Dictionary<string, (int index, int value)> records = new Dictionary<string, (int index, int value)>();
                    Dictionary<string, int> records = new Dictionary<string, int>();

                    foreach (var subddPlayerStart in ddPlayerStarts)
                    {
                        int subddPlayerStartValue;
                        if (int.TryParse(subddPlayerStart.SelectedItem?.Text, out subddPlayerStartValue))
                        {
                            records.Add(subddPlayerStart.Name, subddPlayerStartValue);
                        }
                    }

                    XNAClientDropDown curddPlayerStart = (XNAClientDropDown)sender;
                    string curddPlayerStartValue = curddPlayerStart.SelectedItem?.Text;

                    foreach (var subddPlayerStart in ddPlayerStarts)
                    {
                        if (curddPlayerStart.Name == subddPlayerStart.Name)
                        {
                            for (int j = curddPlayerStart.Items.Count - 1; j >= 0; j--)
                            {
                                if (curddPlayerStart.Items[j].Text == curddPlayerStartValue)
                                    continue;

                                if (records.Values.Any(p => p.ToString() == curddPlayerStart.Items[j].Text))
                                    curddPlayerStart.Items.RemoveAt(j);
                            }
                            continue;
                        }

                        subddPlayerStart.Items.Clear();
                        subddPlayerStart.AddItem("???");
                        for (int j = 1; j <= Map.MaxPlayers; j++)
                        {

                            if (records.Values.Any(p => p == j))
                            {
                                if (records.ContainsKey(subddPlayerStart.Name) && records[subddPlayerStart.Name] == j)
                                    ;
                                else
                                    continue;
                            }

                            subddPlayerStart.AddItem(j.ToString());
                        }

                        if (records.ContainsKey(subddPlayerStart.Name))
                        {
                            //此处需要用个Flag来避免SelectedIndex赋值时再次进入该事件从而造成死循环
                            SelectedIndexChangedFlag = true;
                            subddPlayerStart.SelectedIndex = subddPlayerStart.Items.Select(p => p.Text).ToList().IndexOf(records[subddPlayerStart.Name].ToString());
                            SelectedIndexChangedFlag = false;
                        }
                    }
                };

                ddPlayerStart.Visible = false;
                ddPlayerStart.Enabled = false;
                ddPlayerStart.Tag = true;

                ddPlayerNames[i] = ddPlayerName;
                ddPlayerSides[i] = ddPlayerSide;
                ddPlayerColors[i] = ddPlayerColor;
                ddPlayerStarts[i] = ddPlayerStart;
                ddPlayerTeams[i] = ddPlayerTeam;

                PlayerOptionsPanel.AddChild(ddPlayerName);
                PlayerOptionsPanel.AddChild(ddPlayerSide);
                PlayerOptionsPanel.AddChild(ddPlayerColor);
                PlayerOptionsPanel.AddChild(ddPlayerStart);
                PlayerOptionsPanel.AddChild(ddPlayerTeam);

                ReadINIForControl(ddPlayerName);
                ReadINIForControl(ddPlayerSide);
                ReadINIForControl(ddPlayerColor);
                ReadINIForControl(ddPlayerStart);
                ReadINIForControl(ddPlayerTeam);
            }

            var lblName = GeneratePlayerOptionCaption("lblName", "PLAYER", ddPlayerNames[0].X, playerOptionCaptionLocationY);
            var lblSide = GeneratePlayerOptionCaption("lblSide", "SIDE", ddPlayerSides[0].X, playerOptionCaptionLocationY);
            var lblColor = GeneratePlayerOptionCaption("lblColor", "COLOR", ddPlayerColors[0].X, playerOptionCaptionLocationY);

            var lblStart = GeneratePlayerOptionCaption("lblStart", "START", ddPlayerStarts[0].X, playerOptionCaptionLocationY);
            lblStart.Visible = false;

            var lblTeam = GeneratePlayerOptionCaption("lblTeam", "TEAM", ddPlayerTeams[0].X, playerOptionCaptionLocationY);

            ReadINIForControl(lblName);
            ReadINIForControl(lblSide);
            ReadINIForControl(lblColor);
            ReadINIForControl(lblStart);
            ReadINIForControl(lblTeam);

            btnPlayerExtraOptionsOpen = FindChild<XNAClientButton>(nameof(btnPlayerExtraOptionsOpen), true);
            if (btnPlayerExtraOptionsOpen != null)
            {
                PlayerExtraOptionsPanel = FindChild<PlayerExtraOptionsPanel>(nameof(PlayerExtraOptionsPanel));
                PlayerExtraOptionsPanel.Disable();
                PlayerExtraOptionsPanel.OptionsChanged += PlayerExtraOptions_OptionsChanged;
                btnPlayerExtraOptionsOpen.LeftClick += BtnPlayerExtraOptions_LeftClick;
            }

            CheckDisallowedSides();
        }

        private XNALabel GeneratePlayerOptionCaption(string name, string text, int x, int y)
        {
            var label = new XNALabel(WindowManager);
            label.Name = name;
            label.Text = text;
            label.FontIndex = 1;
            label.ClientRectangle = new Rectangle(x, y, 0, 0);
            PlayerOptionsPanel.AddChild(label);

            return label;
        }

        protected virtual void PlayerExtraOptions_OptionsChanged(object sender, EventArgs e)
        {
            var playerExtraOptions = GetPlayerExtraOptions();

            for (int i = 0; i < ddPlayerSides.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerSides[i], i, !playerExtraOptions.IsForceRandomSides);

            for (int i = 0; i < ddPlayerTeams.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerTeams[i], i, !playerExtraOptions.IsForceRandomTeams);

            for (int i = 0; i < ddPlayerColors.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerColors[i], i, !playerExtraOptions.IsForceRandomColors);

            for (int i = 0; i < ddPlayerStarts.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerStarts[i], i, !playerExtraOptions.IsForceRandomStarts);

            UpdateMapPreviewBoxEnabledStatus();
            RefreshBtnPlayerExtraOptionsOpenTexture();
        }

        private void EnablePlayerOptionDropDown(XNAClientDropDown clientDropDown, int playerIndex, bool enable)
        {
            var pInfo = GetPlayerInfoForIndex(playerIndex);
            var allowOtherPlayerOptionsChange = AllowPlayerOptionsChange() && pInfo != null;
            clientDropDown.AllowDropDown = enable && (allowOtherPlayerOptionsChange || pInfo?.Name == ProgramConstants.PLAYERNAME);
            if (!clientDropDown.AllowDropDown)
                clientDropDown.SelectedIndex = clientDropDown.SelectedIndex > 0 ? 0 : clientDropDown.SelectedIndex;
        }

        protected PlayerInfo GetPlayerInfoForIndex(int playerIndex)
        {
            if (playerIndex < Players.Count)
                return Players[playerIndex];

            if (playerIndex < Players.Count + AIPlayers.Count)
                return AIPlayers[playerIndex - Players.Count];

            return null;
        }

        protected PlayerExtraOptions GetPlayerExtraOptions() =>
            PlayerExtraOptionsPanel == null ? new PlayerExtraOptions() : PlayerExtraOptionsPanel.GetPlayerExtraOptions();

        protected void SetPlayerExtraOptions(PlayerExtraOptions playerExtraOptions) => PlayerExtraOptionsPanel?.SetPlayerExtraOptions(playerExtraOptions);

        protected string GetTeamMappingsError() => GetPlayerExtraOptions()?.GetTeamMappingsError();

        private Texture2D LoadTextureOrNull(string name) =>
            AssetLoader.AssetExists(name) ? AssetLoader.LoadTexture(name) : null;

        /// <summary>
        /// 从GameOptions.ini加载随机阵营选择器
        /// </summary>
        /// <param name="selectorNames">选择器名称列表</param>
        /// <param name="selectorSides">选择器阵营列表</param>
        private void GetRandomSelectors(List<string> selectorNames, List<int[]> selectorSides)
        {
            List<string> keys = GameOptionsIni.GetSectionKeys("RandomSelectors");

            if (keys == null)
                return;

            foreach (string randomSelector in keys)
            {
                List<int> randomSides = new List<int>();
                try
                {
                    string[] tmp = GameOptionsIni.GetStringValue("RandomSelectors", randomSelector, string.Empty).Split(',');
                    randomSides = Array.ConvertAll(tmp, int.Parse).Distinct().ToList();
                    randomSides.RemoveAll(x => (x >= SideCount || x < 0));
                }
                catch (FormatException) { }

                if (randomSides.Count > 1)
                {
                    selectorNames.Add(randomSelector);
                    selectorSides.Add(randomSides.ToArray());
                }
            }
        }

        protected abstract void BtnLaunchGame_LeftClick(object sender, EventArgs e);

        protected abstract void BtnLeaveGame_LeftClick(object sender, EventArgs e);

        /// <summary>
        /// 使用实际信息更新Discord Rich Presence。
        /// </summary>
        /// <param name="resetTimer">是否重新启动"已用时间"计时器</param>
        protected abstract void UpdateDiscordPresence(bool resetTimer = false);

        /// <summary>
        /// 将Discord Rich Presence重置为默认状态。
        /// </summary>
        protected void ResetDiscordPresence() => discordHandler.UpdatePresence();

        protected void LoadDefaultGameModeMap()
        {
            if (ddGameModeMapFilter.Items.Count > 0)
            {
                ddGameModeMapFilter.SelectedIndex = GetDefaultGameModeMapFilterIndex();

                lbGameModeMapList.SelectedIndex = 0;
            }
        }

        protected int GetDefaultGameModeMapFilterIndex()
        {
            return ddGameModeMapFilter.Items.FindIndex(i => (i.Tag as GameModeMapFilter)?.Any() ?? false);
        }

        protected GameModeMapFilter GetDefaultGameModeMapFilter()
        {
            return ddGameModeMapFilter.Items[GetDefaultGameModeMapFilterIndex()].Tag as GameModeMapFilter;
        }

        private int GetSpectatorSideIndex() => SideCount + RandomSelectorCount;

        /// <summary>
        /// 将不允许的阵营索引应用到阵营选项下拉框和玩家选项中。
        /// </summary>
        protected void CheckDisallowedSides()
        {
            var disallowedSideArray = GetDisallowedSides();
            int defaultSide = 0;
            int allowedSideCount = disallowedSideArray.Count(b => b == false);

            if (allowedSideCount == 1)
            {
                // 禁止随机

                for (int i = 0; i < disallowedSideArray.Length; i++)
                {
                    if (!disallowedSideArray[i])
                        defaultSide = i + RandomSelectorCount;
                }

                foreach (XNADropDown dd in ddPlayerSides)
                {
                    //dd.Items[0].Selectable = false;
                    for (int i = 0; i < RandomSelectorCount; i++)
                        dd.Items[i].Selectable = false;
                }
            }
            else
            {
                foreach (XNADropDown dd in ddPlayerSides)
                {
                    //dd.Items[0].Selectable = true;
                    for (int i = 0; i < RandomSelectorCount; i++)
                        dd.Items[i].Selectable = true;
                }
            }

            var concatPlayerList = Players.Concat(AIPlayers);

            // 如果所有或除一个包含的阵营被禁用，则禁用自定义随机组。
            int c = 0;
            var playerInfos = concatPlayerList.ToList();
            foreach (int[] randomSides in RandomSelectors)
            {
                int disableCount = 0;

                foreach (int side in randomSides)
                {
                    if (disallowedSideArray[side])
                        disableCount++;
                }

                bool disabled = disableCount >= randomSides.Length - 1;

                foreach (XNADropDown dd in ddPlayerSides)
                    dd.Items[1 + c].Selectable = !disabled;

                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == 1 + c && disabled)
                        pInfo.SideId = defaultSide;
                }

                c++;
            }

            // 遍历阵营数组，根据阵营是否可用
            // 来禁用或启用阵营下拉选项
            for (int i = 0; i < disallowedSideArray.Length; i++)
            {
                bool disabled = disallowedSideArray[i];

                if (disabled)
                {
                    foreach (XNADropDown dd in ddPlayerSides)
                        dd.Items[i + RandomSelectorCount].Selectable = false;

                    // 将使用禁用阵营的玩家的阵营更改为默认阵营
                    foreach (PlayerInfo pInfo in playerInfos)
                    {
                        if (pInfo.SideId == i + RandomSelectorCount)
                            pInfo.SideId = defaultSide;
                    }
                }
                else
                {
                    foreach (XNADropDown dd in ddPlayerSides)
                        dd.Items[i + RandomSelectorCount].Selectable = true;
                }
            }

            // 如果只允许1个阵营，将所有玩家的阵营更改为该阵营
            if (allowedSideCount == 1)
            {
                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == 0)
                        pInfo.SideId = defaultSide;
                }
            }

            if (Map != null && Map.CoopInfo != null)
            {
                // 禁止观战

                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == GetSpectatorSideIndex())
                        pInfo.SideId = defaultSide;
                }

                foreach (XNADropDown dd in ddPlayerSides)
                {
                    if (dd.Items.Count > GetSpectatorSideIndex())
                        dd.Items[SideCount + RandomSelectorCount].Selectable = false;
                }
            }
            else
            {
                foreach (XNADropDown dd in ddPlayerSides)
                {
                    if (dd.Items.Count > SideCount + RandomSelectorCount)
                        dd.Items[SideCount + RandomSelectorCount].Selectable = true;
                }
            }
        }

        public List<string> GetDeleteFile(string oldGame)
        {
            if (oldGame == null || oldGame == "")
                return null;
            List<string> deleteFile = new List<string>();

            foreach (string file in Directory.GetFiles(oldGame))
            {
                deleteFile.Add(Path.GetFileName(file));
            }

            return deleteFile;
        }

        /// <summary>
        /// 获取不允许的阵营索引列表。
        /// </summary>
        /// <returns>不允许的阵营索引列表。</returns>
        protected bool[] GetDisallowedSides()
        {
            string[] sides = null;
            List<string> selectorNames = new List<string>();
            RandomSelectors.Clear();
            string[,] Randomside = null;
            int count = 0;
            foreach (var dropDown in DropDowns)
            {

                if (dropDown.SetSides() != null)
                    sides = dropDown.SetSides();

            }

            if (sides != null)
            {

                foreach (var dropDown in DropDowns)
                {
                    if (dropDown.SetRandomSelectors() != null)
                        Randomside = dropDown.SetRandomSelectors();
                }
                if (Randomside != null)
                {
                    count = Randomside.GetLength(1);
                    MapPreviewBox.RandomSelectorCount = Randomside.GetLength(1);
                }
                RandomSelectorCount = count + 1;
                SideCount = sides.Length;
            }
            else
            {

                sides = GameOptionsIni.GetStringValue("General", "Sides", String.Empty).Split(',');
                GetRandomSelectors(selectorNames, RandomSelectors);
                RandomSelectorCount = RandomSelectors.Count + 1;
                count = RandomSelectors.Count;
                Randomside = new string[selectorNames.Count, 2];
                for (int i = 0; i < selectorNames.Count; i++)
                {
                    Randomside[i, 0] = selectorNames[i];

                    Randomside[i, 1] = string.Join(",", RandomSelectors[i]);

                }
                SideCount = sides.Length;
            }



            foreach (var ddSide in ddPlayerSides)
            {
                ddSide.Items.Clear();
                ddSide.AddItem("随机", LoadTextureOrNull("Randomicon.png"));
                RandomSelectors.Clear();
                for (int i = 0; i < count; i++)
                {
                    RandomSelectors.Add(Array.ConvertAll(Randomside[i, 1].Split(','), int.Parse));
                    ddSide.AddItem(Randomside[i, 0], LoadTextureOrNull(Randomside[i, 0] + "icon.png"));
                }

                for (int i = count; i < sides.Length + count; i++)
                {
                    ddSide.AddItem(sides[i - count], LoadTextureOrNull(sides[i - count] + "icon.png"));

                }
                ddSide.AddItem("观战", LoadTextureOrNull("spectatoricon.png"));
            }

            var returnValue = new bool[SideCount];

            foreach (var dropDown in DropDowns)
            {
                dropDown.ApplyDisallowedSideIndex(returnValue);
            }

            if (Map != null && Map.CoopInfo != null)
            {
                // 合作地图禁用阵营逻辑

                foreach (int disallowedSideIndex in Map.CoopInfo.DisallowedPlayerSides)
                    returnValue[disallowedSideIndex] = true;
            }

            if (GameMode != null)
            {
                foreach (int disallowedSideIndex in GameMode.DisallowedPlayerSides)
                    returnValue[disallowedSideIndex] = true;
            }

            foreach (var checkBox in CheckBoxes)
                checkBox.ApplyDisallowedSideIndex(returnValue);

            return returnValue;
        }

        /// <summary>
        /// 随机化人类和AI玩家的选项，并以PlayerHouseInfo数组的形式返回选项。
        /// </summary>
        /// <returns>PlayerHouseInfo数组。</returns>
        protected virtual PlayerHouseInfo[] Randomize(List<TeamStartMapping> teamStartMappings)
        {
            int totalPlayerCount = Players.Count + AIPlayers.Count;
            PlayerHouseInfo[] houseInfos = new PlayerHouseInfo[totalPlayerCount];

            for (int i = 0; i < totalPlayerCount; i++)
                houseInfos[i] = new PlayerHouseInfo();

            // 收集观战者列表
            for (int i = 0; i < Players.Count; i++)
                houseInfos[i].IsSpectator = Players[i].SideId == GetSpectatorSideIndex();

            // 收集可用颜色列表

            List<int> freeColors = new List<int>();

            for (int cId = 0; cId < MPColors.Count; cId++)
                freeColors.Add(cId);

            if (Map.CoopInfo != null)
            {
                foreach (int colorIndex in Map.CoopInfo.DisallowedPlayerColors)
                    freeColors.Remove(colorIndex);
            }

            foreach (PlayerInfo player in Players)
                freeColors.Remove(player.ColorId - 1); // 第一个颜色是随机

            foreach (PlayerInfo aiPlayer in AIPlayers)
                freeColors.Remove(aiPlayer.ColorId - 1);

            // 收集可用起始位置列表

            List<int> freeStartingLocations = new List<int>();
            List<int> takenStartingLocations = new List<int>();

            for (int i = 0; i < Map.MaxPlayers; i++)
                freeStartingLocations.Add(i);

            for (int i = 0; i < Players.Count; i++)
            {
                if (!houseInfos[i].IsSpectator)
                {
                    freeStartingLocations.Remove(Players[i].StartingLocation - 1);
                    //takenStartingLocations.Add(Players[i].StartingLocation - 1);
                    // ^ 这会让每个选择了位置的玩家在游戏中获得一个完全随机的
                    // 位置，因为PlayerHouseInfo.RandomizeStart已经
                    // 自己填充了列表
                }
            }

            for (int i = 0; i < AIPlayers.Count; i++)
                freeStartingLocations.Remove(AIPlayers[i].StartingLocation - 1);

            foreach (var teamStartMapping in teamStartMappings.Where(mapping => mapping.IsBlock))
                freeStartingLocations.Remove(teamStartMapping.StartingWaypoint);

            // 随机化选项

            Random random = new Random(RandomSeed);

            for (int i = 0; i < totalPlayerCount; i++)
            {
                PlayerInfo pInfo;
                PlayerHouseInfo pHouseInfo = houseInfos[i];

                if (i < Players.Count)
                    pInfo = Players[i];
                else
                    pInfo = AIPlayers[i - Players.Count];

                pHouseInfo.RandomizeSide(pInfo, SideCount, random, GetDisallowedSides(), RandomSelectors, RandomSelectorCount);

                pHouseInfo.RandomizeColor(pInfo, freeColors, MPColors, random);
                pHouseInfo.RandomizeStart(pInfo, random, freeStartingLocations, takenStartingLocations, teamStartMappings.Any());
            }

            return houseInfos;
        }

        /// <summary>
        /// 写入spawn.ini。返回随机化器返回的玩家房屋信息。
        /// </summary>
        private PlayerHouseInfo[] WriteSpawnIni()
        {
            Logger.Log("Writing spawn.ini");

            FileInfo spawnerSettingsFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS);

            IniFile spawnReader = new IniFile(spawnerSettingsFile.FullName);

            spawnerSettingsFile.Delete();

            if (Map.IsCoop)
            {
                foreach (PlayerInfo pInfo in Players)
                    pInfo.TeamId = 1;

                foreach (PlayerInfo pInfo in AIPlayers)
                    pInfo.TeamId = 1;
            }

            var teamStartMappings = new List<TeamStartMapping>(0);
            if (PlayerExtraOptionsPanel != null)
            {
                teamStartMappings = PlayerExtraOptionsPanel.GetTeamStartMappings();
            }

            PlayerHouseInfo[] houseInfos = Randomize(teamStartMappings);

            IniFile spawnIni = new IniFile(spawnerSettingsFile.FullName);

            IniSection settings = new IniSection("Settings");

            string newAi = (cmbAI.SelectedItem.Tag as AI).DisplayName;
            //写入新AI
            settings.SetStringValue("AI", newAi);

            settings.SetStringValue("Name", ProgramConstants.PLAYERNAME);
            settings.SetStringValue("Scenario", ProgramConstants.SPAWNMAP_INI);
            settings.SetStringValue("UIGameMode", GameMode.UIName);
            settings.SetStringValue("UIMapName", Map.Name);
            settings.SetIntValue("PlayerCount", Players.Count);
            int myIndex = Players.FindIndex(c => c.Name == ProgramConstants.PLAYERNAME);
            settings.SetIntValue("Side", houseInfos[myIndex].InternalSideIndex);
            settings.SetBooleanValue("IsSpectator", houseInfos[myIndex].IsSpectator);
            settings.SetIntValue("Color", houseInfos[myIndex].ColorIndex);
            settings.SetStringValue("CustomLoadScreen", LoadingScreenController.GetLoadScreenName(houseInfos[myIndex].InternalSideIndex.ToString()));
            settings.SetIntValue("AIPlayers", AIPlayers.Count);
            settings.SetIntValue("Seed", RandomSeed);
            if (GetPvPTeamCount() > 1)
                settings.SetBooleanValue("CoachMode", true);
            if (GetGameType() == GameType.Coop)
                settings.SetBooleanValue("AutoSurrender", false);
            spawnIni.AddSection(settings);
            WriteSpawnIniAdditions(spawnIni);

            foreach (GameLobbyCheckBox chkBox in CheckBoxes)
                chkBox.ApplySpawnINICode(spawnIni);

            foreach (GameLobbyDropDown dd in DropDowns)
                dd.ApplySpawnIniCode(spawnIni);

            // 从GameOptions.ini应用强制选项

            List<string> forcedKeys = GameOptionsIni.GetSectionKeys("ForcedSpawnIniOptions");

            if (forcedKeys != null)
            {
                foreach (string key in forcedKeys)
                {
                    spawnIni.SetStringValue("Settings", key,
                        GameOptionsIni.GetStringValue("ForcedSpawnIniOptions", key, string.Empty));
                }
            }

            GameMode.ApplySpawnIniCode(spawnIni); // 来自游戏模式的强制选项
            Map.ApplySpawnIniCode(spawnIni, Players.Count + AIPlayers.Count,
                AIPlayers.Count, GameMode.CoopDifficultyLevel); // 来自地图的强制选项

            // 玩家选项

            int otherId = 1;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];
                PlayerHouseInfo pHouseInfo = houseInfos[pId];

                if (pInfo.Name == ProgramConstants.PLAYERNAME)
                    continue;

                string sectionName = "Other" + otherId;

                spawnIni.SetStringValue(sectionName, "Name", pInfo.Name);
                spawnIni.SetIntValue(sectionName, "Side", pHouseInfo.InternalSideIndex);
                spawnIni.SetBooleanValue(sectionName, "IsSpectator", pHouseInfo.IsSpectator);
                spawnIni.SetIntValue(sectionName, "Color", pHouseInfo.ColorIndex);
                spawnIni.SetStringValue(sectionName, "Ip", GetIPAddressForPlayer(pInfo));
                spawnIni.SetIntValue(sectionName, "Port", pInfo.Port);

                otherId++;
            }

            // 生成器根据玩家的游戏内颜色索引将玩家分配到SpawnX房屋
            List<int> multiCmbIndexes = new List<int>();
            var sortedColorList = MPColors.OrderBy(mpc => mpc.GameColorIndex).ToList();

            for (int cId = 0; cId < sortedColorList.Count; cId++)
            {
                for (int pId = 0; pId < Players.Count; pId++)
                {
                    if (houseInfos[pId].ColorIndex == sortedColorList[cId].GameColorIndex)
                        multiCmbIndexes.Add(pId);
                }
            }

            if (AIPlayers.Count > 0)
            {
                for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
                {
                    int multiId = multiCmbIndexes.Count + aiId + 1;

                    string keyName = "Multi" + multiId;

                    spawnIni.SetIntValue("HouseHandicaps", keyName, AIPlayers[aiId].HouseHandicapAILevel);
                    spawnIni.SetIntValue("HouseCountries", keyName, houseInfos[Players.Count + aiId].InternalSideIndex);
                    spawnIni.SetIntValue("HouseColors", keyName, houseInfos[Players.Count + aiId].ColorIndex);
                }
            }

            for (int multiId = 0; multiId < multiCmbIndexes.Count; multiId++)
            {
                int pIndex = multiCmbIndexes[multiId];
                if (houseInfos[pIndex].IsSpectator)
                    spawnIni.SetBooleanValue("IsSpectator", "Multi" + (multiId + 1), true);
            }

            // 写入同盟，代码量较大所以放到另一个类中
            AllianceHolder.WriteInfoToSpawnIni(Players, AIPlayers, multiCmbIndexes, houseInfos.ToList(), teamStartMappings, spawnIni);

            for (int pId = 0; pId < Players.Count; pId++)
            {
                int startingWaypoint = houseInfos[multiCmbIndexes[pId]].StartingWaypoint;

                // -1表示完全没有起始位置 - 让游戏本身使用其自己的逻辑选择起始位置
                if (startingWaypoint > -1)
                {
                    int multiIndex = pId + 1;
                    spawnIni.SetIntValue("SpawnLocations", "Multi" + multiIndex,
                        startingWaypoint);
                }
            }

            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                int startingWaypoint = houseInfos[Players.Count + aiId].StartingWaypoint;

                if (startingWaypoint > -1)
                {
                    int multiIndex = Players.Count + aiId + 1;
                    spawnIni.SetIntValue("SpawnLocations", "Multi" + multiIndex,
                        startingWaypoint);
                }
            }

            spawnIni.WriteIniFile();

            return houseInfos;
        }

        /// <summary>
        /// 返回包含人类玩家的队伍数量。
        /// 不计算观战者和未设置队伍的人类玩家。
        /// </summary>
        /// <returns>游戏中的人类玩家队伍数量。</returns>
        private int GetPvPTeamCount()
        {
            int[] teamPlayerCounts = new int[4];
            int playerTeamCount = 0;

            foreach (PlayerInfo pInfo in Players)
            {
                if (pInfo.IsAI || IsPlayerSpectator(pInfo))
                    continue;

                if (pInfo.TeamId > 0)
                {
                    teamPlayerCounts[pInfo.TeamId - 1]++;
                    if (teamPlayerCounts[pInfo.TeamId - 1] == 2)
                        playerTeamCount++;
                }
            }

            return playerTeamCount;
        }

        /// <summary>
        /// 检查指定玩家是否选择了观战者作为其阵营。
        /// </summary>
        /// <param name="pInfo">玩家。</param>
        /// <returns>如果玩家是观战者则为true，否则为false。</returns>
        protected bool IsPlayerSpectator(PlayerInfo pInfo)
        {
            if (pInfo.SideId == GetSpectatorSideIndex())
                return true;

            return false;
        }

        protected virtual string GetIPAddressForPlayer(PlayerInfo player) => "0.0.0.0";

        /// <summary>
        /// 在派生类中重写此方法以向spawn.ini写入游戏大厅特定的代码。
        /// 例如，CnCNet游戏大厅应在此方法中写入隧道信息。
        /// </summary>
        /// <param name="iniFile">spawn INI文件。</param>
        protected virtual void WriteSpawnIniAdditions(IniFile iniFile)
        {
            // 默认不做任何操作
        }

        private void InitializeMatchStatistics(PlayerHouseInfo[] houseInfos)
        {
            matchStatistics = new MatchStatistics(ProgramConstants.GAME_VERSION, UniqueGameID,
                Map.Name, GameMode.UIName, Players.Count, Map.IsCoop);

            bool isValidForStar = true;
            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                if ((checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenChecked && checkBox.Checked) ||
                    (checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenUnchecked && !checkBox.Checked))
                {
                    isValidForStar = false;
                    break;
                }
            }

            matchStatistics.IsValidForStar = isValidForStar;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];
                matchStatistics.AddPlayer(pInfo.Name, pInfo.Name == ProgramConstants.PLAYERNAME,
                    false, pInfo.SideId == SideCount + RandomSelectorCount, houseInfos[pId].SideIndex + 1, pInfo.TeamId,
                    MPColors.FindIndex(c => c.GameColorIndex == houseInfos[pId].ColorIndex), 10);
            }

            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                var pHouseInfo = houseInfos[Players.Count + aiId];
                PlayerInfo aiInfo = AIPlayers[aiId];
                matchStatistics.AddPlayer("Computer", false, true, false,
                    pHouseInfo.SideIndex + 1, aiInfo.TeamId,
                    MPColors.FindIndex(c => c.GameColorIndex == pHouseInfo.ColorIndex),
                    aiInfo.AILevel);
            }
        }

        /// <summary>
        /// 写入spawnmap.ini。
        /// </summary>
        private void WriteMap(PlayerHouseInfo[] houseInfos)
        {
            FileInfo spawnMapIniFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.SPAWNMAP_INI);

            DeleteSupplementalMapFiles();
            spawnMapIniFile.Delete();

            Logger.Log("Writing map.");

            Logger.Log("Loading map INI from " + Map.CompleteFilePath);

            IniFile mapIni = Map.GetMapIni();

            IniFile globalCodeIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Map Code", "GlobalCode.ini"));

            MapCodeHelper.ApplyMapCode(mapIni, GameMode.GetMapRulesIniFile());
            MapCodeHelper.ApplyMapCode(mapIni, globalCodeIni);

            if (isMultiplayer)
            {
                IniFile mpGlobalCodeIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Map Code", "MultiplayerGlobalCode.ini"));
                MapCodeHelper.ApplyMapCode(mapIni, mpGlobalCodeIni);
            }

            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
                checkBox.ApplyMapCode(mapIni, GameMode);

            foreach (GameLobbyDropDown dropDown in DropDowns)
                dropDown.ApplyMapCode(mapIni, GameMode);

            mapIni.MoveSectionToFirst("MultiplayerDialogSettings"); // YR要求

            string mapIniFileName = Path.GetFileName(mapIni.FileName);
            mapIni.SetStringValue("Basic", "OriginalFilename", mapIniFileName);
            CopySupplementalMapFiles(mapIni);

            ManipulateStartingLocations(mapIni, houseInfos);

            mapIni.WriteIniFile(spawnMapIniFile.FullName);
        }

        /// <summary>
        /// 某些模组要求.map文件在复制spawnmap.ini时也同时复制补充文件。
        ///
        /// 此函数扫描包含地图文件的目录，查找与地图文件具有相同基本文件名
        /// 且被客户端配置允许的其他文件。这些文件随后以"spawnmap.EXT"的
        /// 基本文件名复制到游戏基础路径。
        /// </summary>
        /// <param name="mapIni"></param>
        private void CopySupplementalMapFiles(IniFile mapIni)
        {
            var mapFileInfo = new FileInfo(mapIni.FileName);
            string mapFileBaseName = Path.GetFileNameWithoutExtension(mapFileInfo.Name);

            IEnumerable<string> supplementalMapFiles = GetSupplementalMapFiles(mapFileInfo.DirectoryName, mapFileBaseName).ToList();
            if (!supplementalMapFiles.Any())
                return;

            List<string> supplementalFileNames = new();
            foreach (string file in supplementalMapFiles)
            {
                try
                {
                    // 复制每个补充文件
                    string supplementalFileName = $"spawnmap{Path.GetExtension(file)}";
                    File.Copy(file, SafePath.CombineFilePath(ProgramConstants.GamePath, supplementalFileName), true);
                    supplementalFileNames.Add(supplementalFileName);
                }
                catch (Exception e)
                {
                    string errorMessage = "无法复制补充地图文件" + $" {file}";
                    Logger.Log(errorMessage);
                    Logger.Log(e.Message);
                    XNAMessageBox.Show(WindowManager, "错误", errorMessage);

                }
            }

            // 将补充地图文件写入INI（最终的spawnmap.ini）
            mapIni.SetStringValue("Basic", "SupplementalFiles", string.Join(',', supplementalFileNames));
        }

        /// <summary>
        /// 删除上次生成时的所有补充地图文件
        /// </summary>
        private void DeleteSupplementalMapFiles()
        {
            IEnumerable<string> supplementalMapFilePaths = GetSupplementalMapFiles(ProgramConstants.GamePath, "spawnmap").ToList();
            if (!supplementalMapFilePaths.Any())
                return;

            foreach (string supplementalMapFilename in supplementalMapFilePaths)
            {
                try
                {
                    File.Delete(supplementalMapFilename);
                }
                catch (Exception e)
                {
                    string errorMessage = "无法删除补充地图文件" + $" {supplementalMapFilename}";
                    Logger.Log(errorMessage);
                    Logger.Log(e.Message);
                    XNAMessageBox.Show(WindowManager, "错误", errorMessage);
                }
            }
        }

        private static IEnumerable<string> GetSupplementalMapFiles(string basePath, string baseFileName)
        {
            // 获取允许扩展名的补充文件名
            var supplementalMapFileNames = ClientConfiguration.Instance.SupplementalMapFileExtensions
                .Select(ext => $"{baseFileName}.{ext}".ToUpperInvariant())
                .ToList();

            if (!supplementalMapFileNames.Any())
                return new List<string>();

            // 获取所有可能补充文件的完整路径
            return Directory.GetFiles(basePath, $"{baseFileName}.*")
                .Where(f => supplementalMapFileNames.Contains(Path.GetFileName(f).ToUpperInvariant()));
        }

        private void ManipulateStartingLocations(IniFile mapIni, PlayerHouseInfo[] houseInfos)
        {
            if (RemoveStartingLocations)
            {
                if (Map.EnforceMaxPlayers)
                    return;

                // 游戏给出的所有随机起始位置
                IniSection waypointSection = mapIni.GetSection("Waypoints");
                if (waypointSection == null)
                    return;

                // TODO 在Rampastring.Tools中实现IniSection.RemoveKey，然后
                // 移除依赖IniSection内部实现的代码
                for (int i = 0; i <= 7; i++)
                {
                    int index = waypointSection.Keys.FindIndex(k => !string.IsNullOrEmpty(k.Key) && k.Key == i.ToString());
                    if (index > -1)
                        waypointSection.Keys.RemoveAt(index);
                }
            }

            // 多个玩家不能正确共享同一个起始位置
            // 否则会破坏预放置对象所依赖的SpawnX房屋逻辑

            // 为了解决这个问题，我们添加新的起始位置，指向与现有堆叠起始位置
            // 相同的单元格坐标，并让同一起始位置的额外玩家从新的起始位置开始。

            // 作为一个额外的限制，玩家只能从航点0到7开始。
            // 这意味着如果地图已经有太多起始航点，
            // 我们需要移动现有（但未占用）的起始航点指向堆叠位置，
            // 以便我们可以在那里生成玩家。


            // 检查堆叠的起始位置（有超过1个玩家的位置）
            bool[] startingLocationUsed = new bool[MAX_PLAYER_COUNT];
            bool stackedStartingLocations = false;
            foreach (PlayerHouseInfo houseInfo in houseInfos)
            {
                if (houseInfo.RealStartingWaypoint > -1)
                {
                    startingLocationUsed[houseInfo.RealStartingWaypoint] = true;

                    // 如果分配的起始航点未知而实际起始位置已知，
                    // 则表示该位置与另一个玩家共享
                    if (houseInfo.StartingWaypoint == -1)
                    {
                        stackedStartingLocations = true;
                    }
                }
            }

            // 如果有任何起始位置是堆叠的，重新排列所有起始位置
            // 使得未使用的起始位置被移除并指向已使用的起始位置
            if (!stackedStartingLocations)
                return;

            // 我们还需要修改spawn.ini，因为WriteSpawnIni
            // 不处理堆叠位置。
            // 我们可以将此代码移到那里，但那样我们就必须在两个地方
            // （这里和WriteSpawnIni）处理堆叠位置，
            // 因为无论如何我们都需要修改地图。
            // 不确定放在这里还是WriteSpawnIni中更好，
            // 但这种实现方式写起来更快。
            IniFile spawnIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS));

            // 对于每个玩家，检查他们是否与其他玩家共享起始位置
            // 如果是，找到一个未使用的航点并将他们的起始位置分配到那里
            for (int pId = 0; pId < houseInfos.Length; pId++)
            {
                PlayerHouseInfo houseInfo = houseInfos[pId];

                if (houseInfo.RealStartingWaypoint > -1 &&
                    houseInfo.StartingWaypoint == -1)
                {
                    // 找到第一个未使用的起始位置索引
                    int unusedLocation = -1;
                    for (int i = 0; i < startingLocationUsed.Length; i++)
                    {
                        if (!startingLocationUsed[i])
                        {
                            unusedLocation = i;
                            startingLocationUsed[i] = true;
                            break;
                        }
                    }

                    houseInfo.StartingWaypoint = unusedLocation;
                    mapIni.SetIntValue("Waypoints", unusedLocation.ToString(),
                        mapIni.GetIntValue("Waypoints", houseInfo.RealStartingWaypoint.ToString(), 0));
                    spawnIni.SetIntValue("SpawnLocations", $"Multi{pId + 1}", unusedLocation);
                }
            }

            spawnIni.WriteIniFile();
        }

        /// <summary>
        /// 写入spawn.ini，写入地图文件，初始化统计信息并启动游戏进程。
        /// </summary>
        protected virtual void StartGame()
        {
            PlayerHouseInfo[] houseInfos = WriteSpawnIni();
            InitializeMatchStatistics(houseInfos);
            WriteMap(houseInfos);

            //补充逻辑：血量显示是否应用
            var chkBloodDisplay = CheckBoxes.FirstOrDefault(p => p.Name == "chkBloodDisplay");
            if (chkBloodDisplay != null)
                ShowKratosHelper.ApplyKratosDisplay(chkBloodDisplay);
            //补充逻辑：原生AI逻辑是否触发(已弃用，改为使用ini文件替换的逻辑)
            //var chkDefenceAiTrigger = CheckBoxes.FirstOrDefault(p => p.Name == "chkDefenceAiTrigger");
            //if (chkDefenceAiTrigger != null && chkDefenceAiTrigger.Visible && chkDefenceAiTrigger.Checked)
            //{
            //    DefenceAiHelper.SetAITriggerEnable(Map?.BaseFilePath, chkDefenceAiTrigger);
            //}

            //应用新AI
            var cmbAI = DropDowns.FirstOrDefault(p => p.Name == "cmbAI");
            if (cmbAI != null)
            {
                AI ai = cmbAI.SelectedItem.Tag as AI;
                ai.Backup();
            }

            GameProcessLogic.GameProcessExited += GameProcessExited_Callback;

            GameProcessLogic.StartGameProcess(WindowManager);
            UpdateDiscordPresence(true);
        }

        private void GameProcessExited_Callback() => AddCallback(new Action(GameProcessExited), null);

        protected virtual void GameProcessExited()
        {
            GameProcessLogic.GameProcessExited -= GameProcessExited_Callback;

            Logger.Log("GameProcessExited: Parsing statistics.");

            matchStatistics.ParseStatistics(ProgramConstants.GamePath, ClientConfiguration.Instance.LocalGame, false);

            Logger.Log("GameProcessExited: Adding match to statistics.");

            StatisticsManager.Instance.AddMatchAndSaveDatabase(true, matchStatistics);

            ClearReadyStatuses();

            CopyPlayerDataToUI();

            UpdateDiscordPresence(true);

            var needDeleteList = new string[] { "0.png", "1.png", "2.png", "3.png", "4.png" };
            foreach (var item in needDeleteList)
            {
                try
                {
                    if (File.Exists(item))
                        File.Delete(item);
                }
                catch (Exception ex)
                {

                }
            }
            //还原老AI
            var cmbAI = DropDowns.FirstOrDefault(p => p.Name == "cmbAI");
            if (cmbAI != null)
            {
                AI ai = (cmbAI.SelectedItem).Tag as AI;
                ai.Recovery();
            }
        }

        /// <summary>
        /// 将玩家信息从UI"复制"到内部内存，应用用户的玩家选项更改。
        /// </summary>
        protected virtual void CopyPlayerDataFromUI(object sender, EventArgs e)
        {
            if (PlayerUpdatingInProgress)
                return;

            var senderDropDown = (XNADropDown)sender;
            if ((bool)senderDropDown.Tag)
                ClearReadyStatuses();

            var oldSideId = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME)?.SideId;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];

                pInfo.ColorId = ddPlayerColors[pId].SelectedIndex;
                pInfo.SideId = ddPlayerSides[pId].SelectedIndex;
                int startingLocation;
                bool isInt = int.TryParse(ddPlayerStarts[pId].SelectedItem.Text, out startingLocation);
                pInfo.StartingLocation = isInt ? startingLocation : 0;
                pInfo.TeamId = ddPlayerTeams[pId].SelectedIndex;

                if (pInfo.SideId == SideCount + RandomSelectorCount)
                    pInfo.StartingLocation = 0;

                XNADropDown ddName = ddPlayerNames[pId];

                switch (ddName.SelectedIndex)
                {
                    case 0:
                        break;
                    case 1:
                        ddName.SelectedIndex = 0;
                        break;
                    case 2:
                        KickPlayer(pId);
                        break;
                    case 3:
                        BanPlayer(pId);
                        break;
                }
            }

            AIPlayers.Clear();
            for (int cmbId = Players.Count; cmbId < 8; cmbId++)
            {
                XNADropDown dd = ddPlayerNames[cmbId];
                dd.Items[0].Text = "-";

                if (dd.SelectedIndex < 1)
                    continue;

                PlayerInfo aiPlayer = new PlayerInfo
                {
                    Name = dd.Items[dd.SelectedIndex].Text,
                    AILevel = dd.SelectedIndex - 1,
                    SideId = Math.Max(ddPlayerSides[cmbId].SelectedIndex, 0),
                    ColorId = Math.Max(ddPlayerColors[cmbId].SelectedIndex, 0),
                    TeamId = Map != null && Map.IsCoop ? 1 : Math.Max(ddPlayerTeams[cmbId].SelectedIndex, 0),
                    IsAI = true
                };

                int startingLocation;
                bool isInt = int.TryParse(ddPlayerStarts[cmbId].SelectedItem?.Text, out startingLocation);
                aiPlayer.StartingLocation = isInt ? startingLocation : 0;

                AIPlayers.Add(aiPlayer);
            }

            CopyPlayerDataToUI();
            btnLaunchGame.SetRank(GetRank());

            if (oldSideId != Players.Find(p => p.Name == ProgramConstants.PLAYERNAME)?.SideId)
                UpdateDiscordPresence();
        }

        /// <summary>
        /// 将所有非主机人类玩家的准备状态设为false。
        /// </summary>
        /// <param name="resetAutoReady">如果设置，则同时重置启用了自动准备的玩家。</param>
        protected void ClearReadyStatuses(bool resetAutoReady = false)
        {
            for (int i = 1; i < Players.Count; i++)
            {
                if (resetAutoReady || !Players[i].AutoReady || Players[i].IsInGame)
                    Players[i].Ready = false;
            }
        }

        private bool CanRightClickMultiplayer(XNADropDownItem selectedPlayer)
        {
            return selectedPlayer != null &&
                   selectedPlayer.Text != ProgramConstants.PLAYERNAME &&
                   !ProgramConstants.AI_PLAYER_NAMES.Contains(selectedPlayer.Text);
        }

        private void MultiplayerName_RightClick(object sender, EventArgs e)
        {
            var selectedPlayer = ((XNADropDown)sender).SelectedItem;
            if (!CanRightClickMultiplayer(selectedPlayer))
                return;

            if (selectedPlayer == null ||
                selectedPlayer.Text == ProgramConstants.PLAYERNAME)
            {
                return;
            }

            MultiplayerNameRightClicked?.Invoke(this, new MultiplayerNameRightClickedEventArgs(selectedPlayer.Text));
        }

        /// <summary>
        /// 将内存中的玩家信息更改应用到UI。
        /// </summary>
        protected virtual void CopyPlayerDataToUI()
        {
            PlayerUpdatingInProgress = true;

            bool allowOptionsChange = AllowPlayerOptionsChange();
            var playerExtraOptions = GetPlayerExtraOptions();

            // 人类玩家
            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];

                pInfo.Index = pId;

                XNADropDown ddPlayerName = ddPlayerNames[pId];
                ddPlayerName.Items[0].Text = pInfo.Name;
                ddPlayerName.Items[1].Text = string.Empty;
                ddPlayerName.Items[2].Text = "踢出";
                ddPlayerName.Items[3].Text = "封禁";
                ddPlayerName.SelectedIndex = 0;
                ddPlayerName.AllowDropDown = false;

                bool allowPlayerOptionsChange = allowOptionsChange || pInfo.Name == ProgramConstants.PLAYERNAME;

                ddPlayerSides[pId].SelectedIndex = pInfo.SideId;
                ddPlayerSides[pId].AllowDropDown = !playerExtraOptions.IsForceRandomSides && allowPlayerOptionsChange;

                ddPlayerColors[pId].SelectedIndex = pInfo.ColorId;
                ddPlayerColors[pId].AllowDropDown = !playerExtraOptions.IsForceRandomColors && allowPlayerOptionsChange;
                //由于集合改变了，index可能也改变了，所以需要重新找
                if (pInfo.StartingLocation == 0)
                    ddPlayerStarts[pId].SelectedIndex = 0;
                else
                    ddPlayerStarts[pId].SelectedIndex = ddPlayerStarts[pId].Items.FindIndex(p => p.Text == pInfo.StartingLocation.ToString());

                ddPlayerTeams[pId].SelectedIndex = pInfo.TeamId;
                if (GameModeMap != null)
                {
                    ddPlayerTeams[pId].AllowDropDown = !playerExtraOptions.IsForceRandomTeams && allowPlayerOptionsChange && !Map.IsCoop && !Map.ForceNoTeams && !GameMode.ForceNoTeams;
                    ddPlayerStarts[pId].AllowDropDown = !playerExtraOptions.IsForceRandomStarts && allowPlayerOptionsChange && (Map.IsCoop || !Map.ForceRandomStartLocations && !GameMode.ForceRandomStartLocations);
                }
            }

            // AI玩家
            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                PlayerInfo aiInfo = AIPlayers[aiId];

                int index = Players.Count + aiId;

                aiInfo.Index = index;

                XNADropDown ddPlayerName = ddPlayerNames[index];
                ddPlayerName.Items[0].Text = "-";
                ddPlayerName.Items[1].Text = ProgramConstants.AI_PLAYER_NAMES[0];
                ddPlayerName.Items[2].Text = ProgramConstants.AI_PLAYER_NAMES[1];
                ddPlayerName.Items[3].Text = ProgramConstants.AI_PLAYER_NAMES[2];
                ddPlayerName.SelectedIndex = 1 + aiInfo.AILevel;
                ddPlayerName.AllowDropDown = allowOptionsChange;

                ddPlayerSides[index].SelectedIndex = aiInfo.SideId;
                ddPlayerSides[index].AllowDropDown = !playerExtraOptions.IsForceRandomSides && allowOptionsChange;

                ddPlayerColors[index].SelectedIndex = aiInfo.ColorId;
                ddPlayerColors[index].AllowDropDown = !playerExtraOptions.IsForceRandomColors && allowOptionsChange;
                //由于集合改变了，index可能也改变了，所以需要重新找
                if (aiInfo.StartingLocation == 0)
                    ddPlayerStarts[index].SelectedIndex = 0;
                else
                    ddPlayerStarts[index].SelectedIndex = ddPlayerStarts[index].Items.FindIndex(p => p.Text == aiInfo.StartingLocation.ToString());

                ddPlayerTeams[index].SelectedIndex = aiInfo.TeamId;

                if (GameModeMap != null)
                {
                    ddPlayerTeams[index].AllowDropDown = !playerExtraOptions.IsForceRandomTeams && allowOptionsChange && !Map.IsCoop && !Map.ForceNoTeams && !GameMode.ForceNoTeams;
                    ddPlayerStarts[index].AllowDropDown = !playerExtraOptions.IsForceRandomStarts && allowOptionsChange && (Map.IsCoop || !Map.ForceRandomStartLocations && !GameMode.ForceRandomStartLocations);
                }
            }

            // 未使用的玩家槽位
            for (int ddIndex = Players.Count + AIPlayers.Count; ddIndex < MAX_PLAYER_COUNT; ddIndex++)
            {
                XNADropDown ddPlayerName = ddPlayerNames[ddIndex];
                ddPlayerName.AllowDropDown = false;
                ddPlayerName.Items[0].Text = string.Empty;
                ddPlayerName.Items[1].Text = ProgramConstants.AI_PLAYER_NAMES[0];
                ddPlayerName.Items[2].Text = ProgramConstants.AI_PLAYER_NAMES[1];
                ddPlayerName.Items[3].Text = ProgramConstants.AI_PLAYER_NAMES[2];
                ddPlayerName.SelectedIndex = 0;

                ddPlayerSides[ddIndex].SelectedIndex = -1;
                ddPlayerSides[ddIndex].AllowDropDown = false;

                ddPlayerColors[ddIndex].SelectedIndex = -1;
                ddPlayerColors[ddIndex].AllowDropDown = false;

                ddPlayerStarts[ddIndex].SelectedIndex = -1;
                ddPlayerStarts[ddIndex].AllowDropDown = false;

                ddPlayerTeams[ddIndex].SelectedIndex = -1;
                ddPlayerTeams[ddIndex].AllowDropDown = false;
            }

            if (allowOptionsChange && Players.Count + AIPlayers.Count < MAX_PLAYER_COUNT)
                ddPlayerNames[Players.Count + AIPlayers.Count].AllowDropDown = true;

            MapPreviewBox.UpdateStartingLocationTexts();
            UpdateMapPreviewBoxEnabledStatus();

            PlayerUpdatingInProgress = false;
        }

        /// <summary>
        /// 更新地图预览框中起始位置选择器的启用状态。
        /// </summary>
        protected abstract void UpdateMapPreviewBoxEnabledStatus();

        /// <summary>
        /// 在派生类中重写此方法以踢出玩家。
        /// </summary>
        /// <param name="playerIndex">应被踢出的玩家索引。</param>
        protected virtual void KickPlayer(int playerIndex)
        {
            // 默认不做任何操作
        }

        /// <summary>
        /// 在派生类中重写此方法以封禁玩家。
        /// </summary>
        /// <param name="playerIndex">应被禁言/封禁的玩家索引。</param>
        protected virtual void BanPlayer(int playerIndex)
        {
            // 默认不做任何操作
        }

        /// <summary>
        /// 更改当前地图和游戏模式。
        /// </summary>
        /// <param name="gameModeMap">新的游戏模式地图。</param>
        protected virtual void ChangeMap(GameModeMap gameModeMap)
        {
            GameModeMap = gameModeMap;

            if (GameMode == null || Map == null)
            {
                lblMapName.Text = "地图:未知";
                lblMapAuthor.Text = "未知作者";
                lblGameMode.Text = "游戏模式:未知";
                lblMapSize.Text = "大小:未知";

                MapPreviewBox.GameModeMap = null;

                return;
            }

            lblMapName.Text = "地图:" + " " + Renderer.GetSafeString(Map.Name, lblMapName.FontIndex);
            lblMapAuthor.Text = "作者:" + " " + Renderer.GetSafeString(Map.Author, lblMapAuthor.FontIndex);
            lblGameMode.Text = "游戏模式:" + " " + GameMode.UIName;
            lblMapSize.Text = "大小:" + " " + Map.GetSizeString();

            disableGameOptionUpdateBroadcast = true;

            // 清除强制选项
            foreach (var ddGameOption in DropDowns)
                ddGameOption.AllowDropDown = true;

            foreach (var checkBox in CheckBoxes)
                checkBox.AllowChecking = true;

            // 我们可以将此类的CheckBoxes和DropDowns传递给Map和GameMode实例，
            // 让它们应用其强制选项，或者我们可以在此类中使用辅助函数来完成。
            // 第二种方法可能更清晰。

            // 我们使用这些临时列表来确定哪些选项未被地图强制。
            // 然后我们将这些选项恢复为用户定义的设置。
            // 这样可以防止一个地图的强制选项被带到其他地图。

            var checkBoxListClone = new List<GameLobbyCheckBox>(CheckBoxes);
            var dropDownListClone = new List<GameLobbyDropDown>(DropDowns);

            ApplyForcedCheckBoxOptions(checkBoxListClone, GameMode.ForcedCheckBoxValues);
            ApplyForcedCheckBoxOptions(checkBoxListClone, Map.ForcedCheckBoxValues);

            ApplyForcedDropDownOptions(dropDownListClone, GameMode.ForcedDropDownValues);
            ApplyForcedDropDownOptions(dropDownListClone, Map.ForcedDropDownValues);

            foreach (var chkBox in checkBoxListClone)
                chkBox.Checked = chkBox.HostChecked;

            foreach (var dd in dropDownListClone)
                dd.SelectedIndex = dd.HostSelectedIndex;

            // 默认启用所有阵营
            foreach (var ddSide in ddPlayerSides)
            {
                ddSide.Items.ForEach(item => item.Selectable = true);
            }

            // 默认启用所有颜色
            foreach (var ddColor in ddPlayerColors)
            {
                ddColor.Items.ForEach(item => item.Selectable = true);
            }

            // 应用起始位置
            foreach (var ddStart in ddPlayerStarts)
            {
                ddStart.Items.Clear();

                ddStart.AddItem("???");

                for (int i = 1; i <= Map.MaxPlayers; i++)
                    ddStart.AddItem(i.ToString());
            }


            // 检查是否允许AI玩家
            bool AIAllowed = !(Map.MultiplayerOnly || GameMode.MultiplayerOnly) ||
                             !(Map.HumanPlayersOnly || GameMode.HumanPlayersOnly);
            foreach (var ddName in ddPlayerNames)
            {
                if (ddName.Items.Count > 3)
                {
                    ddName.Items[1].Selectable = AIAllowed;
                    ddName.Items[2].Selectable = AIAllowed;
                    ddName.Items[3].Selectable = AIAllowed;
                }
            }

            if (!AIAllowed) AIPlayers.Clear();
            IEnumerable<PlayerInfo> concatPlayerList = Players.Concat(AIPlayers).ToList();

            foreach (PlayerInfo pInfo in concatPlayerList)
            {
                if (pInfo.StartingLocation > Map.MaxPlayers ||
                    (!Map.IsCoop && (Map.ForceRandomStartLocations || GameMode.ForceRandomStartLocations)))
                    pInfo.StartingLocation = 0;
                if (!Map.IsCoop && (Map.ForceNoTeams || GameMode.ForceNoTeams))
                    pInfo.TeamId = 0;
            }

            CheckDisallowedSides();


            if (Map.CoopInfo != null)
            {
                // 合作地图禁用颜色逻辑
                foreach (int disallowedColorIndex in Map.CoopInfo.DisallowedPlayerColors)
                {
                    if (disallowedColorIndex >= MPColors.Count)
                        continue;

                    foreach (XNADropDown ddColor in ddPlayerColors)
                        ddColor.Items[disallowedColorIndex + 1].Selectable = false;

                    foreach (PlayerInfo pInfo in concatPlayerList)
                    {
                        if (pInfo.ColorId == disallowedColorIndex + 1)
                            pInfo.ColorId = 0;
                    }
                }

                // 强制队伍
                foreach (PlayerInfo pInfo in concatPlayerList)
                    pInfo.TeamId = 1;
            }

            OnGameOptionChanged();

            MapPreviewBox.GameModeMap = GameModeMap;
            CopyPlayerDataToUI();

            disableGameOptionUpdateBroadcast = false;

            PlayerExtraOptionsPanel?.UpdateForMap(Map);
        }

        private void ApplyForcedCheckBoxOptions(List<GameLobbyCheckBox> optionList,
            List<KeyValuePair<string, bool>> forcedOptions)
        {
            foreach (KeyValuePair<string, bool> option in forcedOptions)
            {
                GameLobbyCheckBox checkBox = CheckBoxes.Find(chk => chk.Name == option.Key);

                if (checkBox != null)
                {

                    checkBox.Checked = option.Value;
                    checkBox.AllowChecking = false;
                    optionList.Remove(checkBox);
                }
            }
        }

        private void ApplyForcedDropDownOptions(List<GameLobbyDropDown> optionList,
            List<KeyValuePair<string, int>> forcedOptions)
        {
            foreach (KeyValuePair<string, int> option in forcedOptions)
            {
                GameLobbyDropDown dropDown = DropDowns.Find(dd => dd.Name == option.Key);
                if (dropDown != null)
                {
                    dropDown.SelectedIndex = option.Value;
                    dropDown.AllowDropDown = false;
                    optionList.Remove(dropDown);
                }
            }
        }

        protected string AILevelToName(int aiLevel)
        {
            return ProgramConstants.GetAILevelName(aiLevel);
        }

        protected GameType GetGameType()
        {
            int teamCount = GetPvPTeamCount();

            if (teamCount == 0)
                return GameType.FFA;

            if (teamCount == 1)
                return GameType.Coop;

            return GameType.TeamGame;
        }

        protected int GetRank()
        {
            if (GameMode == null || Map == null)
                return RANK_NONE;

            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                if ((checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenChecked && checkBox.Checked) ||
                    (checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenUnchecked && !checkBox.Checked))
                {
                    return RANK_NONE;
                }
            }

            PlayerInfo localPlayer = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);

            if (localPlayer == null)
                return RANK_NONE;

            if (IsPlayerSpectator(localPlayer))
                return RANK_NONE;

            // 这些变量被遭遇战和多人游戏代码路径共同使用
            int[] teamMemberCounts = new int[5];
            int lowestEnemyAILevel = 2;
            int highestAllyAILevel = 0;

            foreach (PlayerInfo aiPlayer in AIPlayers)
            {
                teamMemberCounts[aiPlayer.TeamId]++;

                if (aiPlayer.TeamId > 0 && aiPlayer.TeamId == localPlayer.TeamId)
                {
                    if (aiPlayer.AILevel > highestAllyAILevel)
                        highestAllyAILevel = aiPlayer.AILevel;
                }
                else
                {
                    if (aiPlayer.AILevel < lowestEnemyAILevel)
                        lowestEnemyAILevel = aiPlayer.AILevel;
                }
            }

            if (isMultiplayer)
            {
                if (Players.Count == 1)
                    return RANK_NONE;

                // 2人和3人地图的PvP星级
                if (Map.MaxPlayers <= 3)
                {
                    List<PlayerInfo> filteredPlayers = Players.Where(p => !IsPlayerSpectator(p)).ToList();

                    if (AIPlayers.Count > 0)
                        return RANK_NONE;

                    if (filteredPlayers.Count != Map.MaxPlayers)
                        return RANK_NONE;

                    int localTeamIndex = localPlayer.TeamId;
                    if (localTeamIndex > 0 && filteredPlayers.Count(p => p.TeamId == localTeamIndex) > 1)
                        return RANK_NONE;

                    return RANK_HARD;
                }

                // 4人及以上地图的合作星级
                // 条件参见StatisticsManager.GetRankForCoopMatch中的代码

                if (Players.Find(p => IsPlayerSpectator(p)) != null)
                    return RANK_NONE;

                if (AIPlayers.Count == 0)
                    return RANK_NONE;

                if (Players.Find(p => p.TeamId != localPlayer.TeamId) != null)
                    return RANK_NONE;

                if (Players.Find(p => p.TeamId == 0) != null)
                    return RANK_NONE;

                if (AIPlayers.Find(p => p.TeamId == 0) != null)
                    return RANK_NONE;

                teamMemberCounts[localPlayer.TeamId] += Players.Count;

                if (lowestEnemyAILevel < highestAllyAILevel)
                {
                    // 检查玩家的AI盟友是否更强
                    return RANK_NONE;
                }

                // 检查所有队伍是否至少有与人类玩家队伍相同数量的玩家
                int allyCount = teamMemberCounts[localPlayer.TeamId];

                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] > 0)
                    {
                        if (teamMemberCounts[i] < allyCount)
                            return RANK_NONE;
                    }
                }

                return lowestEnemyAILevel + 1;
            }

            // *********
            // 遭遇战！
            // *********

            if (AIPlayers.Count != Map.MaxPlayers - 1)
                return RANK_NONE;

            teamMemberCounts[localPlayer.TeamId]++;

            if (lowestEnemyAILevel < highestAllyAILevel)
            {
                // 检查玩家的AI盟友是否更强
                return RANK_NONE;
            }

            if (localPlayer.TeamId > 0)
            {
                // 检查所有队伍是否至少有与本地玩家队伍相同数量的玩家
                int allyCount = teamMemberCounts[localPlayer.TeamId];

                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] > 0)
                    {
                        if (teamMemberCounts[i] < allyCount)
                            return RANK_NONE;
                    }
                }

                // 检查是否存在除玩家队伍外至少一样大的队伍
                bool pass = false;
                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] >= allyCount)
                    {
                        pass = true;
                        break;
                    }
                }

                if (!pass)
                    return RANK_NONE;
            }

            return lowestEnemyAILevel + 1;
        }

        protected string AddGameOptionPreset(string name)
        {
            string error = GameOptionPreset.IsNameValid(name);
            if (!string.IsNullOrEmpty(error))
                return error;

            GameOptionPreset preset = new GameOptionPreset(name);
            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                try
                {
                    preset.AddCheckBoxValue(checkBox.Name, checkBox.Checked);
                }
                catch
                {
                    continue;
                }
            }

            foreach (GameLobbyDropDown dropDown in DropDowns)
            {
                preset.AddDropDownValue(dropDown.Name, dropDown.SelectedIndex);
            }

            GameOptionPresets.Instance.AddPreset(preset);
            return null;
        }

        public bool LoadGameOptionPreset(string name)
        {
            GameOptionPreset preset = GameOptionPresets.Instance.GetPreset(name);
            if (preset == null)
                return false;

            disableGameOptionUpdateBroadcast = true;

            var checkBoxValues = preset.GetCheckBoxValues();
            foreach (var kvp in checkBoxValues)
            {
                GameLobbyCheckBox checkBox = CheckBoxes.Find(c => c.Name == kvp.Key);
                if (checkBox != null && checkBox.AllowChanges && checkBox.AllowChecking)
                    checkBox.Checked = kvp.Value;
            }

            var dropDownValues = preset.GetDropDownValues();
            foreach (var kvp in dropDownValues)
            {
                GameLobbyDropDown dropDown = DropDowns.Find(d => d.Name == kvp.Key);
                if (dropDown != null && dropDown.AllowDropDown)
                    dropDown.SelectedIndex = kvp.Value;
            }

            disableGameOptionUpdateBroadcast = false;
            OnGameOptionChanged();
            return true;
        }

        protected abstract bool AllowPlayerOptionsChange();
    }
}
