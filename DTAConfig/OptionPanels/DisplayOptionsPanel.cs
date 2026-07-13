using ClientCore;
using ClientGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace DTAConfig.OptionPanels
{
    class DisplayOptionsPanel : XNAOptionsPanel
    {
        private const int DRAG_DISTANCE_DEFAULT = 4;
        private const int ORIGINAL_RESOLUTION_WIDTH = 640;
        private const string FIXED_RENDERER_CONFIG_FILE_NAME = "ddraw.ini";
        private const string FIXED_RENDERER_DEFAULT_CONFIG_PATH = @"Compatibility\Configs\cnc-ddraw.ini";
        private const string FIXED_RENDERER_SECTION = "ddraw";
        private const string FIXED_RENDERER_WINDOWED_KEY = "windowed";
        private const string FIXED_RENDERER_BORDER_KEY = "border";

        public DisplayOptionsPanel(WindowManager windowManager, UserINISettings iniSettings)
            : base(windowManager, iniSettings)
        {
        }

        private XNAClientDropDown ddIngameResolution;
        private XNAClientDropDown ddDetailLevel;
        private XNAClientCheckBox chkWindowedMode;
        private XNAClientCheckBox chkBorderlessWindowedMode;
        private XNAClientCheckBox chkBackBufferInVRAM;
        private XNAClientPreferredItemDropDown ddClientResolution;
        private XNAClientCheckBox chkBorderlessClient;
        private XNAClientDropDown ddClientTheme;
        private XNAClientDropDown ddLanguage;
        private XNAClientDropDown ddVoice;

        public override void Initialize()
        {
            base.Initialize();

            Name = "DisplayOptionsPanel";

            var lblIngameResolution = new XNALabel(WindowManager);
            lblIngameResolution.Name = "lblIngameResolution";
            lblIngameResolution.ClientRectangle = new Rectangle(12, 14, 0, 0);
            lblIngameResolution.Text = "游戏分辨率:";

            ddIngameResolution = new XNAClientDropDown(WindowManager);
            ddIngameResolution.Name = "ddIngameResolution";
            ddIngameResolution.ClientRectangle = new Rectangle(
                lblIngameResolution.Right + 12,
                lblIngameResolution.Y - 2,
                120,
                19);

            var clientConfig = ClientConfiguration.Instance;
            var resolutions = GetResolutions(
                clientConfig.MinimumIngameWidth,
                clientConfig.MinimumIngameHeight,
                clientConfig.MaximumIngameWidth,
                clientConfig.MaximumIngameHeight);

            resolutions.Sort();

            foreach (var res in resolutions)
            {
                ddIngameResolution.AddItem(res.ToString());
            }

            var lblDetailLevel = new XNALabel(WindowManager);
            lblDetailLevel.Name = "lblDetailLevel";
            lblDetailLevel.ClientRectangle = new Rectangle(
                lblIngameResolution.X,
                ddIngameResolution.Bottom + 16,
                0,
                0);
            lblDetailLevel.Text = "画面细节度:";

            ddDetailLevel = new XNAClientDropDown(WindowManager);
            ddDetailLevel.Name = "ddDetailLevel";
            ddDetailLevel.ClientRectangle = new Rectangle(
                ddIngameResolution.X,
                lblDetailLevel.Y - 2,
                ddIngameResolution.Width,
                ddIngameResolution.Height);
            ddDetailLevel.AddItem("低");
            ddDetailLevel.AddItem("中");
            ddDetailLevel.AddItem("高");

            chkWindowedMode = new XNAClientCheckBox(WindowManager);
            chkWindowedMode.Name = "chkWindowedMode";
            chkWindowedMode.ClientRectangle = new Rectangle(
                lblDetailLevel.X,
                ddDetailLevel.Bottom + 16,
                0,
                0);
            chkWindowedMode.Text = "窗口模式";
            chkWindowedMode.CheckedChanged += ChkWindowedMode_CheckedChanged;

            chkBorderlessWindowedMode = new XNAClientCheckBox(WindowManager);
            chkBorderlessWindowedMode.Name = "chkBorderlessWindowedMode";
            chkBorderlessWindowedMode.ClientRectangle = new Rectangle(
                chkWindowedMode.X + 50,
                chkWindowedMode.Bottom + 24,
                0,
                0);
            chkBorderlessWindowedMode.Text = "无边框窗口模式";
            chkBorderlessWindowedMode.AllowChecking = false;

            chkBackBufferInVRAM = new XNAClientCheckBox(WindowManager);
            chkBackBufferInVRAM.Name = "chkBackBufferInVRAM";
            chkBackBufferInVRAM.ClientRectangle = new Rectangle(
                lblDetailLevel.X,
                chkBorderlessWindowedMode.Bottom + 28,
                0,
                0);
            chkBackBufferInVRAM.Text = "开启双重显存缓冲" + Environment.NewLine + "(会降低性能,但在某些系统上是必须的)";

            var lblClientResolution = new XNALabel(WindowManager);
            lblClientResolution.Name = "lblClientResolution";
            lblClientResolution.ClientRectangle = new Rectangle(285, 14, 0, 0);
            lblClientResolution.Text = "客户端分辨率:";

            ddClientResolution = new XNAClientPreferredItemDropDown(WindowManager);
            ddClientResolution.Name = "ddClientResolution";
            ddClientResolution.ClientRectangle = new Rectangle(
                lblClientResolution.Right + 12,
                lblClientResolution.Y - 2,
                160,
                ddIngameResolution.Height);
            ddClientResolution.AllowDropDown = false;
            ddClientResolution.PreferredItemLabel = "(推荐)";

            int width = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            int height = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            resolutions = GetResolutions(1280, 768, width, height);

            AddResolutionIfFitting(1024, 600, resolutions);
            AddResolutionIfFitting(1024, 720, resolutions);
            AddResolutionIfFitting(1280, 600, resolutions);
            AddResolutionIfFitting(1280, 720, resolutions);
            AddResolutionIfFitting(1280, 768, resolutions);

            resolutions.Sort();

            foreach (var res in resolutions)
            {
                var item = new XNADropDownItem
                {
                    Text = res.ToString(),
                    Tag = res.ToString()
                };
                ddClientResolution.AddItem(item);
            }

            foreach (string resolution in clientConfig.RecommendedResolutions)
            {
                string trimmedResolution = resolution.Trim();
                int index = resolutions.FindIndex(res => res.ToString() == trimmedResolution);
                if (index > -1)
                {
                    ddClientResolution.PreferredItemIndexes.Add(index);
                }
            }

            chkBorderlessClient = new XNAClientCheckBox(WindowManager);
            chkBorderlessClient.Name = "chkBorderlessClient";
            chkBorderlessClient.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblDetailLevel.Y,
                0,
                0);
            chkBorderlessClient.Text = "客户端全屏";
            chkBorderlessClient.CheckedChanged += ChkBorderlessMenu_CheckedChanged;
            chkBorderlessClient.Checked = true;

            var lblClientTheme = new XNALabel(WindowManager);
            lblClientTheme.Name = "lblClientTheme";
            lblClientTheme.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                ddDetailLevel.Bottom + 16,
                0,
                0);
            lblClientTheme.Text = "客户端主题";

            ddClientTheme = new XNAClientDropDown(WindowManager);
            ddClientTheme.Name = "ddClientTheme";
            ddClientTheme.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                lblClientTheme.Y - 2,
                ddClientResolution.Width,
                ddDetailLevel.Height);

            var lblLanguage = new XNALabel(WindowManager);
            lblLanguage.Name = "lblLanguage";
            lblLanguage.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblClientTheme.Y + 60,
                0,
                0);
            lblLanguage.Text = "语言";

            ddLanguage = new XNAClientDropDown(WindowManager);
            ddLanguage.Name = "ddLanguage";
            ddLanguage.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                lblLanguage.Y - 2,
                160,
                ddDetailLevel.Height);

            var lblVoice = new XNALabel(WindowManager);
            lblVoice.Name = "lblVoice";
            lblVoice.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblLanguage.Y + 60,
                0,
                0);
            lblVoice.Text = "语音";

            ddVoice = new XNAClientDropDown(WindowManager);
            ddVoice.Name = "ddVoice";
            ddVoice.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                ddLanguage.Y + 60,
                160,
                ddDetailLevel.Height);

            int languageCount = ClientConfiguration.Instance.LanguageCount;
            for (int i = 0; i < languageCount; i++)
            {
                var item = new XNADropDownItem
                {
                    Text = ClientConfiguration.Instance.GetLanguageInfoFromIndex(i)[0],
                    Tag = ClientConfiguration.Instance.GetLanguageInfoFromIndex(i)[0]
                };
                ddLanguage.AddItem(item);
            }

            if (languageCount == 0)
            {
                lblLanguage.Visible = false;
                ddLanguage.Visible = false;
            }

            int voiceCount = ClientConfiguration.Instance.VoiceCount;
            for (int i = 0; i < voiceCount; i++)
            {
                var item = new XNADropDownItem
                {
                    Text = ClientConfiguration.Instance.GetVoiceInfoFromIndex(i)[0],
                    Tag = ClientConfiguration.Instance.GetVoiceInfoFromIndex(i)[0]
                };
                ddVoice.AddItem(item);
            }

            int themeCount = ClientConfiguration.Instance.ThemeCount;
            for (int i = 0; i < themeCount; i++)
            {
                var item = new XNADropDownItem
                {
                    Text = ClientConfiguration.Instance.GetThemeInfoFromIndex(i)[0],
                    Tag = ClientConfiguration.Instance.GetThemeInfoFromIndex(i)[0]
                };
                ddClientTheme.AddItem(item);
            }

            AddChild(chkWindowedMode);
            AddChild(chkBorderlessWindowedMode);
            AddChild(chkBackBufferInVRAM);
            AddChild(chkBorderlessClient);
            AddChild(lblClientTheme);
            AddChild(ddClientTheme);
            AddChild(lblClientResolution);
            AddChild(ddClientResolution);
            AddChild(lblDetailLevel);
            AddChild(ddDetailLevel);
            AddChild(lblIngameResolution);
            AddChild(ddIngameResolution);
            AddChild(lblLanguage);
            AddChild(ddLanguage);
            AddChild(lblVoice);
            AddChild(ddVoice);
        }

        private void AddResolutionIfFitting(int width, int height, List<ScreenResolution> resolutions)
        {
            if (resolutions.Find(res => res.Width == width && res.Height == height) != null)
            {
                return;
            }

            int currentWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            int currentHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            if (currentWidth >= width && currentHeight >= height)
            {
                resolutions.Add(new ScreenResolution(width, height));
            }
        }

        private IniFile GetFixedRendererSettingsIni()
        {
            string settingsPath = SafePath.CombineFilePath(ProgramConstants.GamePath, FIXED_RENDERER_CONFIG_FILE_NAME);
            if (!File.Exists(settingsPath))
            {
                string defaultSettingsPath = SafePath.CombineFilePath(
                    ProgramConstants.GetBaseResourcePath(),
                    FIXED_RENDERER_DEFAULT_CONFIG_PATH);

                if (File.Exists(defaultSettingsPath))
                {
                    File.Copy(defaultSettingsPath, settingsPath, false);
                }
            }

            return new IniFile(settingsPath);
        }

        private void ChkBorderlessMenu_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBorderlessClient.Checked)
            {
                ddClientResolution.AllowDropDown = false;
                string nativeRes = Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height;

                int nativeResIndex = ddClientResolution.Items.FindIndex(i => (string)i.Tag == nativeRes);
                if (nativeResIndex > -1)
                {
                    ddClientResolution.SelectedIndex = nativeResIndex;
                }
            }
            else
            {
                ddClientResolution.AllowDropDown = true;

                if (ddClientResolution.PreferredItemIndexes.Count > 0)
                {
                    int optimalWindowedResIndex = ddClientResolution.PreferredItemIndexes[0];
                    ddClientResolution.SelectedIndex = optimalWindowedResIndex;
                }
            }
        }

        private void ChkWindowedMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkWindowedMode.Checked)
            {
                chkBorderlessWindowedMode.AllowChecking = true;
                return;
            }

            chkBorderlessWindowedMode.AllowChecking = false;
            chkBorderlessWindowedMode.Checked = false;
        }

        public override void Load()
        {
            base.Load();

            ddDetailLevel.SelectedIndex = UserINISettings.Instance.DetailLevel;

            string currentRes = UserINISettings.Instance.IngameScreenWidth.Value +
                "x" + UserINISettings.Instance.IngameScreenHeight.Value;

            int index = ddIngameResolution.Items.FindIndex(i => i.Text == currentRes);
            ddIngameResolution.SelectedIndex = index > -1 ? index : 0;

            IniSettings.Win8CompatMode.Value = "No";

            IniFile rendererSettingsIni = GetFixedRendererSettingsIni();
            chkWindowedMode.Checked = rendererSettingsIni.GetBooleanValue(
                FIXED_RENDERER_SECTION,
                FIXED_RENDERER_WINDOWED_KEY,
                UserINISettings.Instance.WindowedMode);

            bool borderValue = rendererSettingsIni.GetBooleanValue(
                FIXED_RENDERER_SECTION,
                FIXED_RENDERER_BORDER_KEY,
                !UserINISettings.Instance.BorderlessWindowedMode);
            chkBorderlessWindowedMode.Checked = !borderValue;

            int selectedLanguageIndex = ddLanguage.Items.FindIndex(
                ddi => (string)ddi.Tag == UserINISettings.Instance.Language);
            ddLanguage.SelectedIndex = selectedLanguageIndex > -1 ? selectedLanguageIndex : 0;

            int selectedVoiceIndex = ddVoice.Items.FindIndex(
                ddi => (string)ddi.Tag == UserINISettings.Instance.Voice);
            ddVoice.SelectedIndex = selectedVoiceIndex > -1 ? selectedVoiceIndex : 0;

            string currentClientRes = IniSettings.ClientResolutionX.Value + "x" + IniSettings.ClientResolutionY.Value;
            int clientResIndex = ddClientResolution.Items.FindIndex(i => (string)i.Tag == currentClientRes);
            ddClientResolution.SelectedIndex = clientResIndex > -1 ? clientResIndex : 0;

            chkBorderlessClient.Checked = UserINISettings.Instance.BorderlessWindowedClient;

            int selectedThemeIndex = ddClientTheme.Items.FindIndex(
                ddi => (string)ddi.Tag == UserINISettings.Instance.ClientTheme);
            ddClientTheme.SelectedIndex = selectedThemeIndex > -1 ? selectedThemeIndex : 0;

            chkBackBufferInVRAM.Checked = UserINISettings.Instance.BackBufferInVRAM;
        }

        private void CopyDirectory(string sourceDirPath, string saveDirPath)
        {
            if (sourceDirPath == null)
            {
                return;
            }

            if (!Directory.Exists(saveDirPath))
            {
                Directory.CreateDirectory(saveDirPath);
            }

            string[] files = Directory.GetFiles(sourceDirPath);
            foreach (string file in files)
            {
                string filePath = saveDirPath + "\\" + Path.GetFileName(file);
                File.Copy(file, filePath, true);
            }

            string[] folders = Directory.GetDirectories(sourceDirPath);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(saveDirPath, name);
                CopyDirectory(folder, dest);
            }
        }

        public override bool Save()
        {
            bool restartRequired = base.Save();

            IniSettings.DetailLevel.Value = ddDetailLevel.SelectedIndex;

            string[] resolution = ddIngameResolution.SelectedItem.Text.Split('x');
            int[] ingameRes = new int[2] { int.Parse(resolution[0]), int.Parse(resolution[1]) };

            IniSettings.IngameScreenWidth.Value = ingameRes[0];
            IniSettings.IngameScreenHeight.Value = ingameRes[1];

            int dragDistance = ingameRes[0] / ORIGINAL_RESOLUTION_WIDTH * DRAG_DISTANCE_DEFAULT;
            IniSettings.DragDistance.Value = dragDistance;

            IniSettings.WindowedMode.Value = chkWindowedMode.Checked;
            IniSettings.BorderlessWindowedMode.Value = chkBorderlessWindowedMode.Checked;

            string[] clientResolution = ((string)ddClientResolution.SelectedItem.Tag).Split('x');
            int[] clientRes = new int[2] { int.Parse(clientResolution[0]), int.Parse(clientResolution[1]) };

            if (clientRes[0] != IniSettings.ClientResolutionX.Value ||
                clientRes[1] != IniSettings.ClientResolutionY.Value)
            {
                restartRequired = true;
            }

            IniSettings.ClientResolutionX.Value = clientRes[0];
            IniSettings.ClientResolutionY.Value = clientRes[1];

            if (IniSettings.BorderlessWindowedClient.Value != chkBorderlessClient.Checked)
            {
                restartRequired = true;
            }

            IniSettings.BorderlessWindowedClient.Value = chkBorderlessClient.Checked;

            if (UserINISettings.Instance.Language != "")
            {
                string language = ClientConfiguration.Instance.GetLanguagePath(UserINISettings.Instance.Language);
                if (language == null)
                {
                    language = ClientConfiguration.Instance.GetLanguageInfoFromIndex(0)[1];
                }
                else
                {
                    language = ClientConfiguration.Instance.GetLanguageInfoFromIndex(ddLanguage.SelectedIndex)[1];
                }

                if (IniSettings.Language != (string)ddLanguage.SelectedItem.Tag)
                {
                    File.Delete(ProgramConstants.GamePath + "cameo..mix");
                    File.Delete(ProgramConstants.GamePath + "cameomd.mix");
                    File.Delete(ProgramConstants.GamePath + "ra2md.csf");
                    CopyDirectory(language, "./");
                    restartRequired = true;
                    Logger.Log("123");
                }

                IniSettings.Language.Value = (string)ddLanguage.SelectedItem.Tag;
            }

            string voice = ClientConfiguration.Instance.GetVoicePath(UserINISettings.Instance.Voice);
            if (voice == null)
            {
                voice = ClientConfiguration.Instance.GetVoiceInfoFromIndex(0)[1];
            }
            else
            {
                voice = ClientConfiguration.Instance.GetVoiceInfoFromIndex(ddVoice.SelectedIndex)[1];
            }

            if (IniSettings.Voice != (string)ddVoice.SelectedItem.Tag)
            {
                File.Delete(ProgramConstants.GamePath + "audiomd.mix");
                File.Delete(ProgramConstants.GamePath + "audio.mix");
                File.Delete(ProgramConstants.GamePath + "expandmd51.mix");
                File.Delete(ProgramConstants.GamePath + "expandmd50.mix");
                CopyDirectory(voice, "./");
            }

            if (IniSettings.ClientTheme != (string)ddClientTheme.SelectedItem.Tag)
            {
                restartRequired = true;
            }

            IniSettings.Voice.Value = (string)ddVoice.SelectedItem.Tag;
            IniSettings.ClientTheme.Value = (string)ddClientTheme.SelectedItem.Tag;
            IniSettings.BackBufferInVRAM.Value = chkBackBufferInVRAM.Checked;

            IniFile rendererSettingsIni = GetFixedRendererSettingsIni();
            rendererSettingsIni.SetBooleanValue(FIXED_RENDERER_SECTION, FIXED_RENDERER_WINDOWED_KEY, chkWindowedMode.Checked);
            rendererSettingsIni.SetBooleanValue(FIXED_RENDERER_SECTION, FIXED_RENDERER_BORDER_KEY, !chkBorderlessWindowedMode.Checked);
            rendererSettingsIni.WriteIniFile();

            return restartRequired;
        }

        private List<ScreenResolution> GetResolutions(int minWidth, int minHeight, int maxWidth, int maxHeight)
        {
            var screenResolutions = new List<ScreenResolution>();

            foreach (DisplayMode dm in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
            {
                if (dm.Width < minWidth || dm.Height < minHeight || dm.Width > maxWidth || dm.Height > maxHeight)
                {
                    continue;
                }

                var resolution = new ScreenResolution(dm.Width, dm.Height);
                if (screenResolutions.Find(res => res.Equals(resolution)) != null)
                {
                    continue;
                }

                screenResolutions.Add(resolution);
            }

            var subResolutions = new List<ScreenResolution>
            {
                new ScreenResolution(1000, 600),
                new ScreenResolution(1100, 600)
            };

            foreach (var subResolution in subResolutions)
            {
                if (screenResolutions.Find(res => res.Equals(subResolution)) == null)
                {
                    screenResolutions.Add(subResolution);
                }
            }

            return screenResolutions;
        }

        sealed class ScreenResolution : IComparable<ScreenResolution>
        {
            public ScreenResolution(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; set; }

            public int Height { get; set; }

            public override string ToString()
            {
                return Width + "x" + Height;
            }

            public int CompareTo(ScreenResolution res2)
            {
                if (Width < res2.Width)
                {
                    return -1;
                }

                if (Width > res2.Width)
                {
                    return 1;
                }

                if (Height < res2.Height)
                {
                    return -1;
                }

                if (Height > res2.Height)
                {
                    return 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                var resolution = obj as ScreenResolution;
                if (resolution == null)
                {
                    return false;
                }

                return CompareTo(resolution) == 0;
            }

            public override int GetHashCode()
            {
                return new { Width, Height }.GetHashCode();
            }
        }
    }
}
