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
using System.Diagnostics;
using System.Linq;

using System.Windows.Forms;

namespace DTAConfig.OptionPanels
{
    class DisplayOptionsPanel : XNAOptionsPanel
    {
        private const int DRAG_DISTANCE_DEFAULT = 4;
        private const int ORIGINAL_RESOLUTION_WIDTH = 640;
        private const string RENDERERS_INI = "Renderers.ini";

        public DisplayOptionsPanel(WindowManager windowManager, UserINISettings iniSettings)
            : base(windowManager, iniSettings)
        {
        }

        private XNAClientDropDown ddIngameResolution;
        private XNAClientDropDown ddDetailLevel;
        private XNAClientDropDown ddRenderer;
        private XNAClientCheckBox chkWindowedMode;
        private XNAClientCheckBox chkBorderlessWindowedMode;
        private XNAClientCheckBox chkBackBufferInVRAM;
        private XNAClientPreferredItemDropDown ddClientResolution;
        private XNAClientCheckBox chkBorderlessClient;
        private XNAClientDropDown ddClientTheme;
        private XNAClientDropDown ddLanguage;
        private XNAClientDropDown ddVoice;

        private List<DirectDrawWrapper> renderers;

        private string defaultRenderer;
        private DirectDrawWrapper selectedRenderer = null;


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
                lblIngameResolution.Y - 2, 120, 19);

            var clientConfig = ClientConfiguration.Instance;

            var resolutions = GetResolutions(clientConfig.MinimumIngameWidth,
                clientConfig.MinimumIngameHeight,
                clientConfig.MaximumIngameWidth, clientConfig.MaximumIngameHeight);

            resolutions.Sort();

            foreach (var res in resolutions)
                ddIngameResolution.AddItem(res.ToString());

            var lblDetailLevel = new XNALabel(WindowManager);
            lblDetailLevel.Name = "lblDetailLevel";
            lblDetailLevel.ClientRectangle = new Rectangle(lblIngameResolution.X,
                ddIngameResolution.Bottom + 16, 0, 0);
            lblDetailLevel.Text = "画面精细度:";

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

            var lblRenderer = new XNALabel(WindowManager);
            lblRenderer.Name = "lblRenderer";
            lblRenderer.ClientRectangle = new Rectangle(lblDetailLevel.X,
                ddDetailLevel.Bottom + 16, 0, 0);
            lblRenderer.Text = "渲染模式:";

            ddRenderer = new XNAClientDropDown(WindowManager);
            ddRenderer.Name = "ddRenderer";
            ddRenderer.ClientRectangle = new Rectangle(
                ddDetailLevel.X,
                lblRenderer.Y - 2,
                ddDetailLevel.Width,
                ddDetailLevel.Height);

            GetRenderers();

            var localOS = ClientConfiguration.Instance.GetOperatingSystemVersion();

            foreach (var renderer in renderers)
            {
                if (renderer.IsCompatibleWithOS(localOS) && !renderer.Hidden)
                {
                    ddRenderer.AddItem(new XNADropDownItem()
                    {
                        Text = renderer.UIName,
                        Tag = renderer
                    });
                }
            }

            chkWindowedMode = new XNAClientCheckBox(WindowManager);
            chkWindowedMode.Name = "chkWindowedMode";
            chkWindowedMode.ClientRectangle = new Rectangle(lblDetailLevel.X,
                ddRenderer.Bottom + 16, 0, 0);
            chkWindowedMode.Text = "窗口模式";
            chkWindowedMode.CheckedChanged += ChkWindowedMode_CheckedChanged;

            chkBorderlessWindowedMode = new XNAClientCheckBox(WindowManager);
            chkBorderlessWindowedMode.Name = "chkBorderlessWindowedMode";
            chkBorderlessWindowedMode.ClientRectangle = new Rectangle(
                chkWindowedMode.X + 50,
                chkWindowedMode.Bottom + 24, 0, 0);
            chkBorderlessWindowedMode.Text = "无边框窗口模式";
            chkBorderlessWindowedMode.AllowChecking = false;

            chkBackBufferInVRAM = new XNAClientCheckBox(WindowManager);
            chkBackBufferInVRAM.Name = "chkBackBufferInVRAM";
            chkBackBufferInVRAM.ClientRectangle = new Rectangle(
                lblDetailLevel.X,
                chkBorderlessWindowedMode.Bottom + 28, 0, 0);
            chkBackBufferInVRAM.Text = "开启双重显存缓冲" + Environment.NewLine + "(会降低性能,但在某些系统上是必须的)";

            var lblClientResolution = new XNALabel(WindowManager);
            lblClientResolution.Name = "lblClientResolution";
            lblClientResolution.ClientRectangle = new Rectangle(
                285, 14, 0, 0);
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

            // 添加窗口模式的"最佳"客户端分辨率
            // 如果它们在全屏模式下不受支持

            AddResolutionIfFitting(1024, 600, resolutions);
            AddResolutionIfFitting(1024, 720, resolutions);
            AddResolutionIfFitting(1280, 600, resolutions);
            AddResolutionIfFitting(1280, 720, resolutions);
            AddResolutionIfFitting(1280, 768, resolutions);
            //AddResolutionIfFitting(1280, 800, resolutions);

            resolutions.Sort();

            foreach (var res in resolutions)
            {
                var item = new XNADropDownItem();
                item.Text = res.ToString();
                item.Tag = res.ToString();
                ddClientResolution.AddItem(item);
            }

            // 所以我们将最佳分辨率添加到列表中，排序后找出最佳分辨率索引
            // 虽然效率不高，但能用

            string[] recommendedResolutions = clientConfig.RecommendedResolutions;

            foreach (string resolution in recommendedResolutions)
            {
                string trimmedresolution = resolution.Trim();
                int index = resolutions.FindIndex(res => res.ToString() == trimmedresolution);
                if (index > -1)
                    ddClientResolution.PreferredItemIndexes.Add(index);
            }

            chkBorderlessClient = new XNAClientCheckBox(WindowManager);
            chkBorderlessClient.Name = "chkBorderlessClient";
            chkBorderlessClient.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblDetailLevel.Y, 0, 0);
            chkBorderlessClient.Text = "客户端全屏";
            chkBorderlessClient.CheckedChanged += ChkBorderlessMenu_CheckedChanged;
            chkBorderlessClient.Checked = true;

            var lblClientTheme = new XNALabel(WindowManager);
            lblClientTheme.Name = "lblClientTheme";
            lblClientTheme.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblRenderer.Y, 0, 0);
            lblClientTheme.Text = "客户端主题:";

            ddClientTheme = new XNAClientDropDown(WindowManager);
            ddClientTheme.Name = "ddClientTheme";
            ddClientTheme.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                ddRenderer.Y,
                ddClientResolution.Width,
                ddRenderer.Height);

            var lblLanguage = new XNALabel(WindowManager);
            lblLanguage.Name = "lblLanguage";
            lblLanguage.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblRenderer.Y + 60, 0, 0);
            lblLanguage.Text = "语言";

            ddLanguage = new XNAClientDropDown(WindowManager);
            ddLanguage.Name = "ddLanguage";
            ddLanguage.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                ddRenderer.Y + 60,
                160,
                ddRenderer.Height);

            var lblVoice = new XNALabel(WindowManager);
            lblVoice.Name = "lblVoice";
            lblVoice.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                lblLanguage.Y + 60, 0, 0);
            lblVoice.Text = "语音";

            ddVoice = new XNAClientDropDown(WindowManager);
            ddVoice.Name = "ddVoice";
            ddVoice.ClientRectangle = new Rectangle(
                ddClientResolution.X,
                ddLanguage.Y + 60,
                160,
                ddRenderer.Height);

            int languageCount = ClientConfiguration.Instance.LanguageCount;


            for (int i = 0; i < languageCount; i++)
            {
                XNADropDownItem item1 = new XNADropDownItem();
                item1.Text = ClientConfiguration.Instance.GetLanguageInfoFromIndex(i)[0];
                item1.Tag = ClientConfiguration.Instance.GetLanguageInfoFromIndex(i)[0];
                ddLanguage.AddItem(item1);
            }

            if (languageCount == 0)
            {
                lblLanguage.Visible = false;
                ddLanguage.Visible = false;
            }

            int VoiceCount = ClientConfiguration.Instance.VoiceCount;
            for (int i = 0; i < VoiceCount; i++)
            {
                XNADropDownItem item1 = new XNADropDownItem();
                item1.Text = ClientConfiguration.Instance.GetVoiceInfoFromIndex(i)[0];
                item1.Tag = ClientConfiguration.Instance.GetVoiceInfoFromIndex(i)[0];
                ddVoice.AddItem(item1);
            }

            int themeCount = ClientConfiguration.Instance.ThemeCount;
            for (int i = 0; i < themeCount; i++)
            {
                XNADropDownItem item1 = new XNADropDownItem();
                item1.Text = ClientConfiguration.Instance.GetThemeInfoFromIndex(i)[0];
                item1.Tag = ClientConfiguration.Instance.GetThemeInfoFromIndex(i)[0];
                ddClientTheme.AddItem(item1);
            }
            AddChild(chkWindowedMode);
            AddChild(chkBorderlessWindowedMode);
            AddChild(chkBackBufferInVRAM);
            AddChild(chkBorderlessClient);
            AddChild(lblClientTheme);
            AddChild(ddClientTheme);
            AddChild(lblClientResolution);
            AddChild(ddClientResolution);
            AddChild(lblRenderer);
            AddChild(ddRenderer);
            AddChild(lblDetailLevel);
            AddChild(ddDetailLevel);
            AddChild(lblIngameResolution);
            AddChild(ddIngameResolution);
            AddChild(lblLanguage);
            AddChild(ddLanguage);
            AddChild(lblVoice);
            AddChild(ddVoice);
        }

        /// <summary>
        /// 如果屏幕分辨率适合屏幕则将其添加到分辨率列表中。
        /// 添加前检查该分辨率是否已存在。
        /// </summary>
        /// <param name="width">新分辨率的宽度。</param>
        /// <param name="height">新分辨率的高度。</param>
        /// <param name="resolutions">屏幕分辨率列表。</param>
        private void AddResolutionIfFitting(int width, int height, List<ScreenResolution> resolutions)
        {
            if (resolutions.Find(res => res.Width == width && res.Height == height) != null)
                return;

            int currentWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            int currentHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            if (currentWidth >= width && currentHeight >= height)
            {
                resolutions.Add(new ScreenResolution(width, height));
            }
        }

        private void GetRenderers()
        {
            renderers = new List<DirectDrawWrapper>();

            var renderersIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), RENDERERS_INI));

            var keys = renderersIni.GetSectionKeys("Renderers");
            if (keys == null)
                throw new ClientConfigurationException("[Renderers] not found from Renderers.ini!");

            foreach (string key in keys)
            {
                string internalName = renderersIni.GetStringValue("Renderers", key, string.Empty);

                var ddWrapper = new DirectDrawWrapper(internalName, renderersIni);
                renderers.Add(ddWrapper);
            }

            OSVersion osVersion = ClientConfiguration.Instance.GetOperatingSystemVersion();

            defaultRenderer = renderersIni.GetStringValue("DefaultRenderer", osVersion.ToString(), string.Empty);

            if (defaultRenderer == null)
                throw new ClientConfigurationException("Invalid or missing default renderer for operating system: " + osVersion);

            string renderer = UserINISettings.Instance.Renderer;

            selectedRenderer = renderers.Find(r => r.InternalName == renderer);

            if (selectedRenderer == null)
                selectedRenderer = renderers.Find(r => r.InternalName == defaultRenderer);

            if (selectedRenderer == null)
                throw new ClientConfigurationException("Missing renderer: " + renderer);

            GameProcessLogic.UseQres = selectedRenderer.UseQres;
            GameProcessLogic.SingleCoreAffinity = selectedRenderer.SingleCoreAffinity;
        }

        private void ChkBorderlessMenu_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBorderlessClient.Checked)
            {
                ddClientResolution.AllowDropDown = false;
                string nativeRes = Screen.PrimaryScreen.Bounds.Width +
                    "x" + Screen.PrimaryScreen.Bounds.Height;

                int nativeResIndex = ddClientResolution.Items.FindIndex(i => (string)i.Tag == nativeRes);
                if (nativeResIndex > -1)
                    ddClientResolution.SelectedIndex = nativeResIndex;
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

        /// <summary>
        /// 加载用户首选的渲染器。
        /// </summary>
        private void LoadRenderer()
        {
            int index = ddRenderer.Items.FindIndex(
                           r => ((DirectDrawWrapper)r.Tag).InternalName == selectedRenderer.InternalName);

            if (index < 0 && selectedRenderer.Hidden)
            {
                ddRenderer.AddItem(new XNADropDownItem()
                {
                    Text = selectedRenderer.UIName,
                    Tag = selectedRenderer
                });
                index = ddRenderer.Items.Count - 1;
            }

            ddRenderer.SelectedIndex = index;
        }

        public override void Load()
        {
            base.Load();

            LoadRenderer();
            ddDetailLevel.SelectedIndex = UserINISettings.Instance.DetailLevel;

            string currentRes = UserINISettings.Instance.IngameScreenWidth.Value +
                "x" + UserINISettings.Instance.IngameScreenHeight.Value;

            int index = ddIngameResolution.Items.FindIndex(i => i.Text == currentRes);

            ddIngameResolution.SelectedIndex = index > -1 ? index : 0;

            // 不知道这个"Win8CompatMode"实际上做了什么..
            // 禁用它以前只对TS-DDRAW有效，但一旦你尝试过TS-DDRAW，
            // 它就再也不会被启用，所以大多数玩家可能一直处于禁用状态
            IniSettings.Win8CompatMode.Value = "No";

            var renderer = (DirectDrawWrapper)ddRenderer.SelectedItem.Tag;

            if (renderer.UsesCustomWindowedOption())
            {
                // 对于通过自身配置INI文件启用
                // 自定义窗口模式实现的渲染器
                //（例如DxWnd和CnC-DDRAW）

                IniFile rendererSettingsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, renderer.ConfigFileName));

                chkWindowedMode.Checked = rendererSettingsIni.GetBooleanValue(renderer.WindowedModeSection,
                    renderer.WindowedModeKey, false);

                if (!string.IsNullOrEmpty(renderer.BorderlessWindowedModeKey))
                {
                    bool setting = rendererSettingsIni.GetBooleanValue(renderer.WindowedModeSection,
                        renderer.BorderlessWindowedModeKey, false);
                    chkBorderlessWindowedMode.Checked = renderer.IsBorderlessWindowedModeKeyReversed ? !setting : setting;
                }
                else
                {
                    chkBorderlessWindowedMode.Checked = UserINISettings.Instance.BorderlessWindowedMode;
                }
            }
            else
            {
                chkWindowedMode.Checked = UserINISettings.Instance.WindowedMode;
                chkBorderlessWindowedMode.Checked = UserINISettings.Instance.BorderlessWindowedMode;
            }

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
                    CopyDirectory(folder, dest);// 构建目标路径,递归复制文件
                }
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

            // 计算拖拽选择距离，根据分辨率宽度进行缩放
            int dragDistance = ingameRes[0] / ORIGINAL_RESOLUTION_WIDTH * DRAG_DISTANCE_DEFAULT;
            IniSettings.DragDistance.Value = dragDistance;

            DirectDrawWrapper originalRenderer = selectedRenderer;
            selectedRenderer = (DirectDrawWrapper)ddRenderer.SelectedItem.Tag;

            IniSettings.WindowedMode.Value = chkWindowedMode.Checked &&
                !selectedRenderer.UsesCustomWindowedOption();

            IniSettings.BorderlessWindowedMode.Value = chkBorderlessWindowedMode.Checked &&
                string.IsNullOrEmpty(selectedRenderer.BorderlessWindowedModeKey);

            string[] clientResolution = ((string)ddClientResolution.SelectedItem.Tag).Split('x');

            int[] clientRes = new int[2] { int.Parse(clientResolution[0]), int.Parse(clientResolution[1]) };

            if (clientRes[0] != IniSettings.ClientResolutionX.Value ||
                clientRes[1] != IniSettings.ClientResolutionY.Value)
                restartRequired = true;

            IniSettings.ClientResolutionX.Value = clientRes[0];
            IniSettings.ClientResolutionY.Value = clientRes[1];

            if (IniSettings.BorderlessWindowedClient.Value != chkBorderlessClient.Checked)
                restartRequired = true;

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

            if (selectedRenderer != originalRenderer ||
                !SafePath.GetFile(ProgramConstants.GamePath, selectedRenderer.ConfigFileName).Exists)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != selectedRenderer)
                        renderer.Clean();
                }
            }

            selectedRenderer.Apply();

            GameProcessLogic.UseQres = selectedRenderer.UseQres;
            GameProcessLogic.SingleCoreAffinity = selectedRenderer.SingleCoreAffinity;

            if (selectedRenderer.UsesCustomWindowedOption())
            {
                IniFile rendererSettingsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, selectedRenderer.ConfigFileName));

                rendererSettingsIni.SetBooleanValue(selectedRenderer.WindowedModeSection,
                    selectedRenderer.WindowedModeKey, chkWindowedMode.Checked);

                if (!string.IsNullOrEmpty(selectedRenderer.BorderlessWindowedModeKey))
                {
                    bool borderlessModeIniValue = chkBorderlessWindowedMode.Checked;
                    if (selectedRenderer.IsBorderlessWindowedModeKeyReversed)
                        borderlessModeIniValue = !borderlessModeIniValue;

                    rendererSettingsIni.SetBooleanValue(selectedRenderer.WindowedModeSection,
                        selectedRenderer.BorderlessWindowedModeKey, borderlessModeIniValue);
                }

                rendererSettingsIni.WriteIniFile();
            }

            IniSettings.Renderer.Value = selectedRenderer.InternalName;

            return restartRequired;
        }

        private List<ScreenResolution> GetResolutions(int minWidth, int minHeight, int maxWidth, int maxHeight)
        {
            var screenResolutions = new List<ScreenResolution>();

            foreach (DisplayMode dm in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
            {
                if (dm.Width < minWidth || dm.Height < minHeight || dm.Width > maxWidth || dm.Height > maxHeight)
                    continue;

                var resolution = new ScreenResolution(dm.Width, dm.Height);

                // SupportedDisplayModes可能多次包含相同分辨率
                // 因为它会考虑刷新率。
                // 这意味着我们必须检查该分辨率是否已在列表中
                if (screenResolutions.Find(res => res.Equals(resolution)) != null)
                    continue;

                screenResolutions.Add(resolution);
            }

            // 添加1000*600低分辨率支持
            var subResolutions = new List<ScreenResolution>
            { 
                new ScreenResolution(1000, 600),
                new ScreenResolution(1100, 600) 
            };
            foreach (var subResolution in subResolutions)
            {
                if (!screenResolutions.Any(res => res.Equals(subResolution)))
                    screenResolutions.Add(subResolution);
            }
            return screenResolutions;
        }

        /// <summary>
        /// 单个屏幕分辨率。
        /// </summary>
        sealed class ScreenResolution : IComparable<ScreenResolution>
        {
            public ScreenResolution(int width, int height)
            {
                Width = width;
                Height = height;
            }

            /// <summary>
            /// 分辨率的宽度（像素）。
            /// </summary>
            public int Width { get; set; }

            /// <summary>
            /// 分辨率的高度（像素）。
            /// </summary>
            public int Height { get; set; }

            public override string ToString()
            {
                return Width + "x" + Height;
            }

            public int CompareTo(ScreenResolution res2)
            {
                if (this.Width < res2.Width)
                    return -1;
                else if (this.Width > res2.Width)
                    return 1;
                else // 相等
                {
                    if (this.Height < res2.Height)
                        return -1;
                    else if (this.Height > res2.Height)
                        return 1;
                    else return 0;
                }
            }

            public override bool Equals(object obj)
            {
                var resolution = obj as ScreenResolution;

                if (resolution == null)
                    return false;

                return CompareTo(resolution) == 0;
            }

            public override int GetHashCode()
            {
                return new { Width, Height }.GetHashCode();
            }
        }
    }
}
