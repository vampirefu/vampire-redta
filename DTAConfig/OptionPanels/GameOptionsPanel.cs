using ClientCore;
using ClientCore.CnCNet5;
using ClientGUI;
using DTAConfig.Settings;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.Menu;

namespace DTAConfig.OptionPanels
{
    class GameOptionsPanel : XNAOptionsPanel
    {

        private const int MAX_SCROLL_RATE = 6;

        public GameOptionsPanel(WindowManager windowManager, UserINISettings iniSettings, XNAControl topBar)
            : base(windowManager, iniSettings)
        {
            this.topBar = topBar;
        }

        private XNALabel lblScrollRateValue;

        private XNATrackbar trbScrollRate;
        private XNAClientCheckBox chkTargetLines;
        private XNAClientCheckBox chkScrollCoasting;
        private XNAClientCheckBox chkTooltips;
        private XNAClientCheckBox chkShowHiddenObjects;

        private XNADropDown ddGameMod;

        private XNAControl topBar;

        private XNATextBox tbPlayerName;

        private HotkeyConfigurationWindow hotkeyConfigWindow;

        public override void Initialize()
        {
            base.Initialize();

            Name = "GameOptionsPanel";

            var lblScrollRate = new XNALabel(WindowManager);
            lblScrollRate.Name = "lblScrollRate";
            lblScrollRate.ClientRectangle = new Rectangle(12,
                14, 0, 0);
            lblScrollRate.Text = "屏幕滚动速度:";

            lblScrollRateValue = new XNALabel(WindowManager);
            lblScrollRateValue.Name = "lblScrollRateValue";
            lblScrollRateValue.FontIndex = 1;
            lblScrollRateValue.Text = "3";
            lblScrollRateValue.ClientRectangle = new Rectangle(
                Width - lblScrollRateValue.Width - 12,
                lblScrollRate.Y, 0, 0);

            trbScrollRate = new XNATrackbar(WindowManager);
            trbScrollRate.Name = "trbClientVolume";
            trbScrollRate.ClientRectangle = new Rectangle(
                lblScrollRate.Right + 32,
                lblScrollRate.Y - 2,
                lblScrollRateValue.X - lblScrollRate.Right - 47,
                22);
            trbScrollRate.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 2, 2);
            trbScrollRate.MinValue = 0;
            trbScrollRate.MaxValue = MAX_SCROLL_RATE;
            trbScrollRate.ValueChanged += TrbScrollRate_ValueChanged;

            chkScrollCoasting = new SettingCheckBox(WindowManager, true, UserINISettings.OPTIONS, "ScrollMethod", true, "0", "1");
            chkScrollCoasting.Name = "chkScrollCoasting";
            chkScrollCoasting.ClientRectangle = new Rectangle(
                lblScrollRate.X,
                trbScrollRate.Bottom + 20, 0, 0);
            chkScrollCoasting.Text = "惯性滚动";

            //选择游戏
            var lblGameMod = new XNALabel(WindowManager);
            lblGameMod.Name = "lblGameMod";
            lblGameMod.ClientRectangle = new Rectangle(400, chkScrollCoasting.Y, 0, 0);
            lblGameMod.Text = "模组:";

            ddGameMod = new XNAClientDropDown(WindowManager);
            ddGameMod.Name = "ddGameMod";
            ddGameMod.ClientRectangle = new Rectangle(lblGameMod.X + 60, chkScrollCoasting.Y, 150, 20);

            foreach (string s in UserINISettings.Instance.GameModName.Value.Split(','))
                ddGameMod.AddItem(s);

            chkTargetLines = new SettingCheckBox(WindowManager, true, UserINISettings.OPTIONS, "UnitActionLines");
            chkTargetLines.Name = "chkTargetLines";
            chkTargetLines.ClientRectangle = new Rectangle(
                lblScrollRate.X,
                chkScrollCoasting.Bottom + 24, 0, 0);
            chkTargetLines.Text = "目标线";

            chkTooltips = new SettingCheckBox(WindowManager, true, UserINISettings.OPTIONS, "ToolTips");
            chkTooltips.Name = "chkTooltips";
            chkTooltips.Text = "工具提示";

            var lblPlayerName = new XNALabel(WindowManager);
            lblPlayerName.Name = "lblPlayerName";
            lblPlayerName.Text = "玩家名称*:";

            chkShowHiddenObjects = new SettingCheckBox(WindowManager, true, UserINISettings.OPTIONS, "ShowHidden");
            chkShowHiddenObjects.Name = "chkShowHiddenObjects";
            chkShowHiddenObjects.ClientRectangle = new Rectangle(
                lblScrollRate.X,
                chkTargetLines.Bottom + 24, 0, 0);
            chkShowHiddenObjects.Text = "显示隐藏在建筑后的物体";

            chkTooltips.ClientRectangle = new Rectangle(
                lblScrollRate.X,
                chkShowHiddenObjects.Bottom + 24, 0, 0);

            lblPlayerName.ClientRectangle = new Rectangle(
                lblScrollRate.X,
                chkTooltips.Bottom + 30, 0, 0);

            AddChild(chkShowHiddenObjects);

            tbPlayerName = new XNATextBox(WindowManager);
            tbPlayerName.Name = "tbPlayerName";
            tbPlayerName.MaximumTextLength = ClientConfiguration.Instance.MaxNameLength;
            tbPlayerName.ClientRectangle = new Rectangle(trbScrollRate.X,
                lblPlayerName.Y - 2, 200, 19);
            tbPlayerName.Text = ProgramConstants.PLAYERNAME;

            var lblNotice = new XNALabel(WindowManager);
            lblNotice.Name = "lblNotice";
            lblNotice.ClientRectangle = new Rectangle(lblPlayerName.X,
                lblPlayerName.Bottom + 30, 0, 0);
            lblNotice.Text = "*如果您已连接到CnCNet,您需要登出并重新连接以应用新的名称.";

            hotkeyConfigWindow = new HotkeyConfigurationWindow(WindowManager);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, hotkeyConfigWindow);
            hotkeyConfigWindow.Disable();

            var btnConfigureHotkeys = new XNAClientButton(WindowManager);
            btnConfigureHotkeys.Name = "btnConfigureHotkeys";
            btnConfigureHotkeys.ClientRectangle = new Rectangle(lblPlayerName.X, lblNotice.Bottom + 36, UIDesignConstants.BUTTON_WIDTH_160, UIDesignConstants.BUTTON_HEIGHT);
            btnConfigureHotkeys.Text = "热键设置";
            btnConfigureHotkeys.LeftClick += BtnConfigureHotkeys_LeftClick;

            AddChild(lblScrollRate);
            AddChild(lblScrollRateValue);
            AddChild(trbScrollRate);
            AddChild(chkScrollCoasting);
            AddChild(chkTargetLines);
            AddChild(chkTooltips);
            AddChild(lblPlayerName);
            AddChild(tbPlayerName);
            AddChild(lblNotice);
            AddChild(btnConfigureHotkeys);
            AddChild(lblGameMod);
            AddChild(ddGameMod);

            //屏蔽设置界面的mod选项
            lblGameMod.Visible = false;
            ddGameMod.Visible = false;
        }

        private void BtnConfigureHotkeys_LeftClick(object sender, EventArgs e)
        {
            hotkeyConfigWindow.Enable();

            if (topBar.Enabled)
            {
                topBar.Disable();
                hotkeyConfigWindow.EnabledChanged += HotkeyConfigWindow_EnabledChanged;
            }
        }

        private void HotkeyConfigWindow_EnabledChanged(object sender, EventArgs e)
        {
            hotkeyConfigWindow.EnabledChanged -= HotkeyConfigWindow_EnabledChanged;
            topBar.Enable();
        }

        private void TrbScrollRate_ValueChanged(object sender, EventArgs e)
        {
            lblScrollRateValue.Text = trbScrollRate.Value.ToString();
        }

        public override void Load()
        {
            base.Load();

            int scrollRate = ReverseScrollRate(IniSettings.ScrollRate);

            if (scrollRate >= trbScrollRate.MinValue && scrollRate <= trbScrollRate.MaxValue)
            {
                trbScrollRate.Value = scrollRate;
                lblScrollRateValue.Text = scrollRate.ToString();
            }

            ddGameMod.SelectedIndex = UserINISettings.Instance.GameModSelect;


            tbPlayerName.Text = UserINISettings.Instance.PlayerName;
        }

        public bool HasChinese(string str)
        {
            return Regex.IsMatch(str, @"[\u4e00-\u9fa5]");
        }

        public override bool Save()
        {
            bool restartRequired = base.Save();

            if (HasChinese(tbPlayerName.Text))
            {
                XNAMessageBox messageBox = new XNAMessageBox(WindowManager, "出错", "请不要使用中文作为游戏名。", XNAMessageBoxButtons.OK);
                messageBox.Show();
                return false;
            }

            IniSettings.ScrollRate.Value = ReverseScrollRate(trbScrollRate.Value);

            string playerName = NameValidator.GetValidOfflineName(tbPlayerName.Text);

            if (playerName.Length > 0)
                IniSettings.PlayerName.Value = playerName;

            if (ddGameMod.SelectedIndex != IniSettings.GameModSelect)
            {
                restartRequired = true;

                List<string> deleteFile = new List<string>();
                foreach (string file in Directory.GetFiles(UserINISettings.Instance.GameModPath.Value.Split(',')[UserINISettings.Instance.GameModSelect.Value]))
                    deleteFile.Add(Path.GetFileName(file));

                DelFile(deleteFile);
                CopyDirectory(UserINISettings.Instance.GameModPath.Value.Split(',')[ddGameMod.SelectedIndex], "./");

                IniSettings.GameModSelect.Value = ddGameMod.SelectedIndex;
            }
            return restartRequired;
        }

        private void CopyDirectory(string sourceDirPath, string saveDirPath)
        {

            if (sourceDirPath != null)
            {

                if (!Directory.Exists(saveDirPath))
                {
                    Directory.CreateDirectory(saveDirPath);
                }

                string[] files = Directory.GetFiles(sourceDirPath);
                foreach (string file in files)
                {
                    string pFilePath = saveDirPath + "\\" + Path.GetFileName(file);

                    File.Copy(file, pFilePath, true);
                }
                string[] folders = System.IO.Directory.GetDirectories(sourceDirPath);
                foreach (string folder in folders)
                {
                    string name = System.IO.Path.GetFileName(folder);
                    string dest = System.IO.Path.Combine(saveDirPath, name);
                    CopyDirectory(folder, dest);//构建目标路径,递归复制文件
                }
            }
        }

        public void DelFile(List<string> deleteFile)
        {
            //  string resultDirectory = Environment.CurrentDirectory;//目录

            if (deleteFile != null)
            {
                for (int i = 0; i < deleteFile.Count; i++)
                {
                    try
                    {
                        File.Delete(deleteFile[i]);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
        private int ReverseScrollRate(int scrollRate)
        {
            return Math.Abs(scrollRate - MAX_SCROLL_RATE);
        }
    }
}
