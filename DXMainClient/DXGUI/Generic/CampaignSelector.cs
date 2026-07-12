using ClientCore;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using DTAClient.Domain;
using System.IO;
using ClientGUI;
using Rampastring.XNAUI.XNAControls;
using Rampastring.XNAUI;
using Rampastring.Tools;
using System.Linq;
using DTAClient.Domain.AI;

namespace DTAClient.DXGUI.Generic
{
    public class CampaignSelector : XNAWindow
    {
        private const int DEFAULT_WIDTH = 650;
        private const int DEFAULT_HEIGHT = 600;

        private static string[] DifficultyNames = new string[] { "简单", "中等", "困难" };

        private static string[] DifficultyIniPaths = new string[]
        {
            "INI/Map Code/Difficulty Easy.ini",
            "INI/Map Code/Difficulty Medium.ini",
            "INI/Map Code/Difficulty Hard.ini"
        };

        public CampaignSelector(WindowManager windowManager, DiscordHandler discordHandler) : base(windowManager)
        {
            this.discordHandler = discordHandler;
        }

        private DiscordHandler discordHandler;

        private List<Mission> Missions = new List<Mission>();
        private XNAListBox lbCampaignList;
        private XNALabel lblScreen;
        private XNADropDown dddifficulty;
        private XNADropDown ddside;
        private XNAClientButton btnLaunch;
        private XNATextBlock tbMissionDescription;
        private XNATrackbar trbDifficultySelector;

        private XNALabel lbGameSpeed;
        private XNADropDown ddGameSpeed;



        private CheaterWindow cheaterWindow;

        List<string> difficultyList = new List<string>();
        List<string> sideList = new List<string>();

        private string[] filesToCheck = new string[]
        {
            "INI/AI.ini",
            "INI/AIE.ini",
            "INI/Art.ini",
            "INI/ArtE.ini",
            "INI/Enhance.ini",
            "INI/Rules.ini",
            "INI/Map Code/Difficulty Hard.ini",
            "INI/Map Code/Difficulty Medium.ini",
            "INI/Map Code/Difficulty Easy.ini"
        };

        private Mission missionToLaunch;

        public override void Initialize()
        {
            BackgroundTexture = AssetLoader.LoadTexture("missionselectorbg.png");
            ClientRectangle = new Rectangle(0, 0, DEFAULT_WIDTH, DEFAULT_HEIGHT);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            Name = "CampaignSelector";

            var lblSelectCampaign = new XNALabel(WindowManager);
            lblSelectCampaign.Name = "lblSelectCampaign";
            lblSelectCampaign.FontIndex = 1;
            lblSelectCampaign.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblSelectCampaign.Text = "战役:";

            lbCampaignList = new XNAListBox(WindowManager);
            lbCampaignList.Name = "lbCampaignList";
            lbCampaignList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 2, 2);
            lbCampaignList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbCampaignList.ClientRectangle = new Rectangle(12,
                lblSelectCampaign.Bottom + 36, 300, 480);
            lbCampaignList.LineHeight = 20;
            lbCampaignList.SelectedIndexChanged += LbCampaignList_SelectedIndexChanged;

            lblScreen = new XNALabel(WindowManager);
            lblScreen.Name = "lblScreen";
            lblScreen.Text = "筛选";
            lblScreen.ClientRectangle = new Rectangle(10, 35, 0, 0);

            dddifficulty = new XNADropDown(WindowManager);
            dddifficulty.Name = nameof(dddifficulty);
            dddifficulty.ClientRectangle = new Rectangle(10, 55, 100, 25);


            ddside = new XNADropDown(WindowManager);
            ddside.Name = nameof(ddside);
            ddside.ClientRectangle = new Rectangle(dddifficulty.X + dddifficulty.Width + 5, dddifficulty.Y, dddifficulty.Width, dddifficulty.Height);

            var lblMissionDescriptionHeader = new XNALabel(WindowManager);
            lblMissionDescriptionHeader.Name = "lblMissionDescriptionHeader";
            lblMissionDescriptionHeader.FontIndex = 1;
            lblMissionDescriptionHeader.ClientRectangle = new Rectangle(
                lbCampaignList.Right + 12,
                lblSelectCampaign.Y, 0, 0);
            lblMissionDescriptionHeader.Text = "战役描述:";

            tbMissionDescription = new XNATextBlock(WindowManager);
            tbMissionDescription.Name = "tbMissionDescription";
            tbMissionDescription.ClientRectangle = new Rectangle(
                lblMissionDescriptionHeader.X,
                lblMissionDescriptionHeader.Bottom + 6,
                467, 350);
            tbMissionDescription.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            tbMissionDescription.Alpha = 1.0f;
            tbMissionDescription.FontIndex = 1;
            tbMissionDescription.BackgroundTexture = AssetLoader.CreateTexture(AssetLoader.GetColorFromString(ClientConfiguration.Instance.AltUIBackgroundColor),
                tbMissionDescription.Width, tbMissionDescription.Height);

            var lblDifficultyLevel = new XNALabel(WindowManager);
            lblDifficultyLevel.Name = "lblDifficultyLevel";
            lblDifficultyLevel.Text = "难度等级";
            lblDifficultyLevel.FontIndex = 1;
            Vector2 textSize = Renderer.GetTextDimensions(lblDifficultyLevel.Text, lblDifficultyLevel.FontIndex);
            lblDifficultyLevel.ClientRectangle = new Rectangle(
                tbMissionDescription.X + (tbMissionDescription.Width - (int)textSize.X) / 2,
                tbMissionDescription.Bottom + 12, (int)textSize.X, (int)textSize.Y);

            trbDifficultySelector = new XNATrackbar(WindowManager);
            trbDifficultySelector.Name = "trbDifficultySelector";
            trbDifficultySelector.ClientRectangle = new Rectangle(
                tbMissionDescription.X, lblDifficultyLevel.Bottom + 6,
                tbMissionDescription.Width - 130, 30);
            trbDifficultySelector.MinValue = 0;
            trbDifficultySelector.MaxValue = 2;
            trbDifficultySelector.BackgroundTexture = AssetLoader.CreateTexture(
                new Color(0, 0, 0, 128), 2, 2);
            trbDifficultySelector.ButtonTexture = AssetLoader.LoadTextureUncached(
                "trackbarButton_difficulty.png");

            ddGameSpeed = new XNADropDown(WindowManager);
            ddGameSpeed.Name = "ddGameSpeed";
            ddGameSpeed.ClientRectangle = new Rectangle(trbDifficultySelector.X + 120, trbDifficultySelector.Y - 15, 80, 40);

            for (int i = 6; i >= 0; i--)
            {
                ddGameSpeed.AddItem(i.ToString());
            }

            ddGameSpeed.SelectedIndex = 6 - UserINISettings.Instance.CampaignDefaultGameSpeed.Value;

            lbGameSpeed = new XNALabel(WindowManager);
            lbGameSpeed.Name = "lbGameSpeed";
            lbGameSpeed.Text = "游戏速度";
            lbGameSpeed.FontIndex = 1;
            lbGameSpeed.ClientRectangle = new Rectangle(ddGameSpeed.X - 100, trbDifficultySelector.Y - 15, 0, 0);


            var lblEasy = new XNALabel(WindowManager);
            lblEasy.Name = "lblEasy";
            lblEasy.FontIndex = 1;
            lblEasy.Text = "简单";
            lblEasy.ClientRectangle = new Rectangle(trbDifficultySelector.X,
                trbDifficultySelector.Bottom + 6, 1, 1);

            var lblNormal = new XNALabel(WindowManager);
            lblNormal.Name = "lblNormal";
            lblNormal.FontIndex = 1;
            lblNormal.Text = "中等";
            textSize = Renderer.GetTextDimensions(lblNormal.Text, lblNormal.FontIndex);
            lblNormal.ClientRectangle = new Rectangle(
                tbMissionDescription.X + (tbMissionDescription.Width - (int)textSize.X) / 2,
                lblEasy.Y, (int)textSize.X, (int)textSize.Y);

            var lblHard = new XNALabel(WindowManager);
            lblHard.Name = "lblHard";
            lblHard.FontIndex = 1;
            lblHard.Text = "困难";
            lblHard.ClientRectangle = new Rectangle(
                tbMissionDescription.Right - lblHard.Width,
                lblEasy.Y, 1, 1);

            btnLaunch = new XNAClientButton(WindowManager);
            btnLaunch.Name = "btnLaunch";
            btnLaunch.ClientRectangle = new Rectangle(12, Height - 35, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLaunch.Text = "启动";
            btnLaunch.AllowClick = false;
            btnLaunch.LeftClick += BtnLaunch_LeftClick;

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = "btnCancel";
            btnCancel.ClientRectangle = new Rectangle(Width - 145,
                btnLaunch.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnCancel.Text = "取消";
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(lblSelectCampaign);
            AddChild(lblMissionDescriptionHeader);
            AddChild(lbCampaignList);
            AddChild(lblScreen);
            AddChild(dddifficulty);
            AddChild(ddside);
            AddChild(tbMissionDescription);
            AddChild(lblDifficultyLevel);
            AddChild(btnLaunch);
            AddChild(btnCancel);
            AddChild(trbDifficultySelector);
            AddChild(lblEasy);
            AddChild(lblNormal);
            AddChild(lblHard);
            AddChild(lbGameSpeed);
            AddChild(ddGameSpeed);
            // 从 INI 文件设置控件属性
            base.Initialize();

            // 居中显示
            CenterOnParent();

            trbDifficultySelector.Value = UserINISettings.Instance.Difficulty;

            XNADropDownItem allitem = new XNADropDownItem();
            allitem.Text = "所有";
            allitem.Tag = "全部";

            dddifficulty.AddItem(allitem);
            ddside.AddItem(allitem);
            ddside.SelectedIndex = 0;
            dddifficulty.SelectedIndex = 0;
            ReadMissionList();


            foreach (string diff in difficultyList)
            {
                XNADropDownItem item = new XNADropDownItem();
                item.Text = diff;
                item.Tag = diff;
                dddifficulty.AddItem(item);
            }

            foreach (string side in sideList)
            {
                XNADropDownItem item = new XNADropDownItem();
                item.Text = side;
                item.Tag = side;
                ddside.AddItem(item);
            }



            ddside.SelectedIndexChanged += Dddifficulty_SelectedIndexChanged;
            dddifficulty.SelectedIndexChanged += Dddifficulty_SelectedIndexChanged;

            cheaterWindow = new CheaterWindow(WindowManager);
            var dp = new DarkeningPanel(WindowManager);
            dp.AddChild(cheaterWindow);
            AddChild(dp);
            dp.CenterOnParent();
            cheaterWindow.CenterOnParent();
            cheaterWindow.YesClicked += CheaterWindow_YesClicked;
            cheaterWindow.Disable();

        }

        private void Dddifficulty_SelectedIndexChanged(object sender, EventArgs e)
        {
            ReadMissionList();
            //    lbCampaignList.Items.Clear();

            //foreach (Mission mission in Missions)
            //{



            //    var item = new XNAListBoxItem();
            //    item.Text = mission.GUIName;
            //    if (!mission.Enabled)
            //    {
            //        item.TextColor = UISettings.ActiveSettings.DisabledItemColor;
            //    }
            //    else if (string.IsNullOrEmpty(mission.Scenario))
            //    {
            //        item.TextColor = AssetLoader.GetColorFromString(
            //            ClientConfiguration.Instance.ListBoxHeaderColor);
            //        item.IsHeader = true;
            //        item.Selectable = false;
            //    }
            //    else
            //    {
            //        item.TextColor = lbCampaignList.DefaultItemColor;
            //    }

            //    if (!string.IsNullOrEmpty(mission.IconPath))
            //        item.Texture = AssetLoader.LoadTexture(mission.IconPath + "icon.png");

            //    lbCampaignList.AddItem(item);

            //}

        }


        private void LbCampaignList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbCampaignList.SelectedIndex == -1)
            {
                tbMissionDescription.Text = string.Empty;
                btnLaunch.AllowClick = false;
                return;
            }

            Mission mission = Missions[lbCampaignList.SelectedIndex];

            if (string.IsNullOrEmpty(mission.Scenario))
            {
                tbMissionDescription.Text = string.Empty;
                btnLaunch.AllowClick = false;
                return;
            }
            tbMissionDescription.Text = mission.GUIDescription;
            //赋值的时候会在首个字符前产生/r/n(XNATextBlock在处理中文的时候有点问题)
            //为了整齐，没加换行的都加换行处理
            if (!tbMissionDescription.Text.StartsWith(Environment.NewLine))
            {
                tbMissionDescription.Text = Environment.NewLine + mission.GUIDescription;
            }

            if (!mission.Enabled)
            {
                btnLaunch.AllowClick = false;
                return;
            }

            btnLaunch.AllowClick = true;

        }

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            Enabled = false;
        }

        private void BtnLaunch_LeftClick(object sender, EventArgs e)
        {
            int selectedMissionId = lbCampaignList.SelectedIndex;

            Mission mission = Missions[selectedMissionId];

            if (!ClientConfiguration.Instance.ModMode && AreFilesModified())
            {
                // 通过显示作弊者界面来警告用户
                missionToLaunch = mission;
                cheaterWindow.Enable();
                return;
            }

            LaunchMission(mission);
        }

        protected List<string> GetDeleteFile(string oldGame)
        {
            if (string.IsNullOrEmpty(oldGame))
                return null;

            if (!Directory.Exists(oldGame))
                return null;

            List<string> deleteFile = new List<string>();

            foreach (string file in Directory.GetFiles(oldGame))
            {
                deleteFile.Add(Path.GetFileName(file));

            }

            return deleteFile;
        }


        private bool AreFilesModified()
        {
            // 更新器已移除：无法判断文件原始性，假定未被修改
            return false;
        }

        /// <summary>
        /// 当用户在被指责作弊后仍想继续任务时调用。
        /// </summary>
        private void CheaterWindow_YesClicked(object sender, EventArgs e)
        {
            LaunchMission(missionToLaunch);
        }

        /// <summary>
        /// 启动单人任务。
        /// </summary>
        private void LaunchMission(Mission mission)
        {
            bool copyMapsToSpawnmapINI = ClientConfiguration.Instance.CopyMissionsToSpawnmapINI;

            Logger.Log("About to write spawn.ini.");

            IniFile spawnReader = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "spawn.ini"));

            using var spawnStreamWriter = new StreamWriter(SafePath.CombineFilePath(ProgramConstants.GamePath, "spawn.ini"));

            spawnStreamWriter.WriteLine("; Generated by DTA Client");
            //spawnStreamWriter.WriteLine("[Actions]");
            //spawnStreamWriter.WriteLine("01000022=1,16,0,0,0,0,0,0,A");
            //spawnStreamWriter.WriteLine("[Events]");
            //spawnStreamWriter.WriteLine("01000022 = 1,8,0,0");
            //spawnStreamWriter.WriteLine("[Tags]");
            //spawnStreamWriter.WriteLine("01000023=0,New Trigger 1,01000022");
            //spawnStreamWriter.WriteLine("[Triggers]");
            //spawnStreamWriter.WriteLine("01000022=Americans,<none>,New Trigger,0,1,1,1,0");

            spawnStreamWriter.WriteLine("[Settings]");
            if (copyMapsToSpawnmapINI)
                spawnStreamWriter.WriteLine("Scenario=spawnmap.ini");
            else
                spawnStreamWriter.WriteLine("Scenario=" + mission.Scenario);

            // 没人想在"最快"速度下玩任务，所以我们将它改为"较快"
            if (UserINISettings.Instance.GameSpeed == 0)
                UserINISettings.Instance.GameSpeed.Value = 1;

            //TODO战役AI
            //string newAi = (cmbAI.SelectedItem.Tag as AI).DisplayName;
            //spawnStreamWriter.WriteLine("AI=" + newAi);
            spawnStreamWriter.WriteLine("CampaignID=" + mission.Index);
            //spawnStreamWriter.WriteLine("GameSpeed=" + UserINISettings.Instance.GameSpeed);
            spawnStreamWriter.WriteLine("GameSpeed=" + ddGameSpeed.SelectedItem?.Text);
            spawnStreamWriter.WriteLine("Ra2Mode=" + !mission.RequiredAddon);
            string customLoadScreen = LoadingScreenController.GetLoadScreenName(mission.Side.ToString());
            spawnStreamWriter.WriteLine("CustomLoadScreen=" + customLoadScreen);
            spawnStreamWriter.WriteLine("IsSinglePlayer=Yes");
            spawnStreamWriter.WriteLine("SidebarHack=" + ClientConfiguration.Instance.SidebarHack);
            spawnStreamWriter.WriteLine("Side=" + mission.Side);
            spawnStreamWriter.WriteLine("BuildOffAlly=" + mission.BuildOffAlly);

            UserINISettings.Instance.Difficulty.Value = trbDifficultySelector.Value;

            spawnStreamWriter.WriteLine("DifficultyModeHuman=" + (mission.PlayerAlwaysOnNormalDifficulty ? "1" : trbDifficultySelector.Value.ToString()));
            spawnStreamWriter.WriteLine("DifficultyModeComputer=" + GetComputerDifficulty());

            var difficultyIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, DifficultyIniPaths[trbDifficultySelector.Value]));
            string difficultyName = DifficultyNames[trbDifficultySelector.Value];

            spawnStreamWriter.WriteLine();
            spawnStreamWriter.WriteLine();
            spawnStreamWriter.WriteLine();

            if (copyMapsToSpawnmapINI)
            {
                var mapIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, mission.Scenario));
                IniFile.ConsolidateIniFiles(mapIni, difficultyIni);
                mapIni.WriteIniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "spawnmap.ini"));
            }

            UserINISettings.Instance.CampaignDefaultGameSpeed.Value = 6 - ddGameSpeed.SelectedIndex;
            UserINISettings.Instance.Difficulty.Value = trbDifficultySelector.Value;
            UserINISettings.Instance.SaveSettings();

            ((MainMenuDarkeningPanel)Parent).Hide();

            discordHandler.UpdatePresence(mission.GUIName, difficultyName, mission.IconPath, true);
            GameProcessLogic.GameProcessExited += GameProcessExited_Callback;

            GameProcessLogic.StartGameProcess(WindowManager);
        }

        private int GetComputerDifficulty() =>
            Math.Abs(trbDifficultySelector.Value - 2);

        private void GameProcessExited_Callback()
        {
            WindowManager.AddCallback(new Action(GameProcessExited), null);
        }

        protected virtual void GameProcessExited()
        {
            GameProcessLogic.GameProcessExited -= GameProcessExited_Callback;
            // Logger.Log("GameProcessExited: Updating Discord Presence.");
            discordHandler.UpdatePresence();
        }

        private void ReadMissionList()
        {
            lbCampaignList.Clear();
            Missions.Clear();
            string path = @"INI/";

            var files = Directory.GetFiles(path, "Battle*.ini");

            foreach (var file in files)
            {
                // Logger.Log(file);
                ParseBattleIni(file);
            }

            if (Missions.Count == 0)
                ParseBattleIni("INI/" + ClientConfiguration.Instance.BattleFSFileName);

            difficultyList = difficultyList.ToArray().GroupBy(p => p).Select(p => p.Key).ToList();
            sideList = sideList.ToArray().GroupBy(p => p).Select(p => p.Key).ToList();
        }

        /// <summary>
        /// 解析 Battle(E).ini 文件。成功（找到文件）返回 true，否则返回 false。
        /// </summary>
        /// <param name="path">文件的路径，相对于游戏目录。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        private bool ParseBattleIni(string path)
        {

            Logger.Log("Attempting to parse " + path + " to populate mission list.");

            FileInfo battleIniFileInfo = SafePath.GetFile(ProgramConstants.GamePath, path);
            if (!battleIniFileInfo.Exists)
            {
                Logger.Log("File " + path + " not found. Ignoring.");
                return false;
            }

            //if (Missions.Count > 0)
            //{
            //    throw new InvalidOperationException("Loading multiple Battle*.ini files is not supported anymore.");
            //}

            var battleIni = new IniFile(battleIniFileInfo.FullName);

            List<string> battleKeys = battleIni.GetSectionKeys("Battles");

            if (battleKeys == null)
                return false; // 文件存在但没有 [Battles] 节

            for (int i = 0; i < battleKeys.Count; i++)
            {
                string battleEntry = battleKeys[i];
                string battleSection = battleIni.GetStringValue("Battles", battleEntry, "NOT FOUND");

                if (!battleIni.SectionExists(battleSection))
                    continue;

                var mission = new Mission(battleIni, battleSection, i);


                if (dddifficulty.SelectedIndex != 0 && mission.difficulty != (string)dddifficulty.SelectedItem.Tag)
                    continue;

                if (ddside.SelectedIndex != 0 && mission.IconPath != (string)ddside.SelectedItem.Tag)
                    continue;

                if (mission.difficulty != string.Empty)
                    difficultyList.Add(mission.difficulty);
                if (mission.IconPath != string.Empty)
                    sideList.Add(mission.IconPath);

                Missions.Add(mission);

                var item = new XNAListBoxItem();
                item.Text = mission.GUIName;
                item.Tag = mission.sectionName;
                if (!mission.Enabled)
                {
                    item.TextColor = UISettings.ActiveSettings.DisabledItemColor;
                }
                else if (string.IsNullOrEmpty(mission.Scenario))
                {
                    item.TextColor = AssetLoader.GetColorFromString(
                        ClientConfiguration.Instance.ListBoxHeaderColor);
                    item.IsHeader = true;
                    item.Selectable = false;
                }
                else
                {
                    item.TextColor = lbCampaignList.DefaultItemColor;
                }

                if (!string.IsNullOrEmpty(mission.IconPath))
                    item.Texture = AssetLoader.LoadTexture(mission.IconPath + "icon.png");

                lbCampaignList.AddItem(item);

            }

            Logger.Log("Finished parsing " + path + ".");
            return true;

        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
