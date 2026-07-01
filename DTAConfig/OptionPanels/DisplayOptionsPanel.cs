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

#if WINFORMS
using System.Windows.Forms;
#endif
#if TS
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
#endif

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

        private XNAClientCheckBox chkRandom_wallpaper;

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

#if TS
        private XNALabel lblCompatibilityFixes;
        private XNALabel lblGameCompatibilityFix;
        private XNALabel lblMapEditorCompatibilityFix;
        private XNAClientButton btnGameCompatibilityFix;
        private XNAClientButton btnMapEditorCompatibilityFix;

        private bool GameCompatFixInstalled = false;
        private bool FinalSunCompatFixInstalled = false;
        private bool GameCompatFixDeclined = false;
        //private bool FinalSunCompatFixDeclined = false;
#endif


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

            // Add "optimal" client resolutions for windowed mode
            // if they're not supported in fullscreen mode

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

            // So we add the optimal resolutions to the list, sort it and then find
            // out the optimal resolution index - it's inefficient, but works

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

            //����������
            chkRandom_wallpaper = new XNAClientCheckBox(WindowManager);
            chkRandom_wallpaper.Name = "chkRandom_wallpaper";
            chkRandom_wallpaper.ClientRectangle = new Rectangle(
                lblClientResolution.X,
                ddRenderer.Bottom + 16 + 24, 0, 0);//ddRenderer.Bottom + 16
            chkRandom_wallpaper.Text = "随机启动封面";
            chkRandom_wallpaper.Checked = false;

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
#if TS
            lblCompatibilityFixes = new XNALabel(WindowManager);
            lblCompatibilityFixes.Name = "lblCompatibilityFixes";
            lblCompatibilityFixes.FontIndex = 1;
            lblCompatibilityFixes.Text = "兼容性修复 (高级):";
            AddChild(lblCompatibilityFixes);
            lblCompatibilityFixes.CenterOnParent();
            lblCompatibilityFixes.Y = Height - 103;

            lblGameCompatibilityFix = new XNALabel(WindowManager);
            lblGameCompatibilityFix.Name = "lblGameCompatibilityFix";
            lblGameCompatibilityFix.ClientRectangle = new Rectangle(132,
                lblCompatibilityFixes.Bottom + 20, 0, 0);
            lblGameCompatibilityFix.Text = "DTA/TI/TS兼容性修复:";

            btnGameCompatibilityFix = new XNAClientButton(WindowManager);
            btnGameCompatibilityFix.Name = "btnGameCompatibilityFix";
            btnGameCompatibilityFix.ClientRectangle = new Rectangle(
                lblGameCompatibilityFix.Right + 20,
                lblGameCompatibilityFix.Y - 4, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnGameCompatibilityFix.FontIndex = 1;
            btnGameCompatibilityFix.Text = "启用";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                btnGameCompatibilityFix.LeftClick += BtnGameCompatibilityFix_LeftClick;
            else
                btnGameCompatibilityFix.AllowClick = false;

            lblMapEditorCompatibilityFix = new XNALabel(WindowManager);
            lblMapEditorCompatibilityFix.Name = "lblMapEditorCompatibilityFix";
            lblMapEditorCompatibilityFix.ClientRectangle = new Rectangle(
                lblGameCompatibilityFix.X,
                lblGameCompatibilityFix.Bottom + 20, 0, 0);
            lblMapEditorCompatibilityFix.Text = "FinalSun兼容性修复:";

            btnMapEditorCompatibilityFix = new XNAClientButton(WindowManager);
            btnMapEditorCompatibilityFix.Name = "btnMapEditorCompatibilityFix";
            btnMapEditorCompatibilityFix.ClientRectangle = new Rectangle(
                btnGameCompatibilityFix.X,
                lblMapEditorCompatibilityFix.Y - 4,
                btnGameCompatibilityFix.Width,
                btnGameCompatibilityFix.Height);
            btnMapEditorCompatibilityFix.FontIndex = 1;
            btnMapEditorCompatibilityFix.Text = "启用";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                btnMapEditorCompatibilityFix.LeftClick += BtnMapEditorCompatibilityFix_LeftClick;
            else
                btnMapEditorCompatibilityFix.AllowClick = false;

            AddChild(lblGameCompatibilityFix);
            AddChild(btnGameCompatibilityFix);
            AddChild(lblMapEditorCompatibilityFix);
            AddChild(btnMapEditorCompatibilityFix);
#endif
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
            //�������
            AddChild(chkRandom_wallpaper);
        }

        /// <summary>
        /// Adds a screen resolution to a list of resolutions if it fits on the screen.
        /// Checks if the resolution already exists before adding it.
        /// </summary>
        /// <param name="width">The width of the new resolution.</param>
        /// <param name="height">The height of the new resolution.</param>
        /// <param name="resolutions">A list of screen resolutions.</param>
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
#if TS

        /// <summary>
        /// Asks the user whether they want to install the DTA/TI/TS compatibility fix.
        /// </summary>
        public void PostInit()
        {
            Load();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            if (!GameCompatFixInstalled && !GameCompatFixDeclined)
            {
                string defaultGame = ClientConfiguration.Instance.LocalGame;

                var messageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "新的兼容性修复",
                    string.Format("此版本的{0}包含了适用于现代Windows版本的性能增强兼容性修复" + Environment.NewLine +
                        "启用它需要管理员权限。您想安装此兼容性修复吗?" + Environment.NewLine + Environment.NewLine +
                        "您随时可以在选项菜单中安装或卸载此兼容性修复。", defaultGame
                    ));
                messageBox.YesClickedAction = MessageBox_YesClicked;
                messageBox.NoClickedAction = MessageBox_NoClicked;
            }
        }

        [SupportedOSPlatform("windows")]
        private void MessageBox_NoClicked(XNAMessageBox messageBox)
        {
            // Set compatibility fix declined flag in registry
            try
            {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Tiberian Sun Client");

                try
                {
                    regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
                    regKey = regKey.CreateSubKey("Tiberian Sun Client");
                    regKey.SetValue("TSCompatFixDeclined", "Yes");
                }
                catch (Exception ex)
                {
                    Logger.Log("Setting TSCompatFixDeclined failed! Returned error: " + ex.Message);
                }
            }
            catch { }
        }

        [SupportedOSPlatform("windows")]
        private void MessageBox_YesClicked(XNAMessageBox messageBox)
        {
            BtnGameCompatibilityFix_LeftClick(messageBox, EventArgs.Empty);
        }

        [SupportedOSPlatform("windows")]
        private void BtnGameCompatibilityFix_LeftClick(object sender, EventArgs e)
        {
            if (GameCompatFixInstalled)
            {
                try
                {
                    Process sdbinst = Process.Start("sdbinst.exe", "-q -n \"TS Compatibility Fix\"");

                    sdbinst.WaitForExit();

                    Logger.Log("DTA/TI/TS Compatibility Fix succesfully uninstalled.");
                    XNAMessageBox.Show(WindowManager, "兼容性修复已卸载",
                        "DTA/TI/TS兼容性修复已成功卸载。");

                    RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
                    regKey = regKey.CreateSubKey("Tiberian Sun Client");
                    regKey.SetValue("TSCompatFixInstalled", "No");

                    btnGameCompatibilityFix.Text = "启用";

                    GameCompatFixInstalled = false;
                }
                catch (Exception ex)
                {
                    Logger.Log("Uninstalling DTA/TI/TS Compatibility Fix failed. Error message: " + ex.Message);
                    XNAMessageBox.Show(WindowManager, "卸载兼容性修复失败",
                        "卸载DTA/TI/TS兼容性修复失败。错误信息:" + " " + ex.Message);
                }

                return;
            }

            try
            {
                Process sdbinst = Process.Start("sdbinst.exe", "-q \"" + ProgramConstants.GamePath + "Resources/compatfix.sdb\"");

                sdbinst.WaitForExit();

                Logger.Log("DTA/TI/TS Compatibility Fix succesfully installed.");
                XNAMessageBox.Show(WindowManager, "兼容性修复已安装",
                    "DTA/TI/TS兼容性修复已成功安装。");

                RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
                regKey = regKey.CreateSubKey("Tiberian Sun Client");
                regKey.SetValue("TSCompatFixInstalled", "Yes");

                btnGameCompatibilityFix.Text = "禁用";

                GameCompatFixInstalled = true;
            }
            catch (Exception ex)
            {
                Logger.Log("Installing DTA/TI/TS Compatibility Fix failed. Error message: " + ex.Message);
                XNAMessageBox.Show(WindowManager, "安装兼容性修复失败",
                    "安装DTA/TI/TS兼容性修复失败。错误信息:" + " " + ex.Message);
            }
        }

        [SupportedOSPlatform("windows")]
        private void BtnMapEditorCompatibilityFix_LeftClick(object sender, EventArgs e)
        {
            if (FinalSunCompatFixInstalled)
            {
                try
                {
                    Process sdbinst = Process.Start("sdbinst.exe", "-q -n \"Final Sun Compatibility Fix\"");

                    sdbinst.WaitForExit();

                    RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
                    regKey = regKey.CreateSubKey("Tiberian Sun Client");
                    regKey.SetValue("FSCompatFixInstalled", "No");

                    btnMapEditorCompatibilityFix.Text = "启用";

                    Logger.Log("FinalSun Compatibility Fix succesfully uninstalled.");
                    XNAMessageBox.Show(WindowManager, "兼容性修复已卸载",
                        "FinalSun兼容性修复已成功卸载。");

                    FinalSunCompatFixInstalled = false;
                }
                catch (Exception ex)
                {
                    Logger.Log("Uninstalling FinalSun Compatibility Fix failed. Error message: " + ex.Message);
                    XNAMessageBox.Show(WindowManager, "卸载兼容性修复失败",
                        "卸载FinalSun兼容性修复失败。错误信息:" + " " + ex.Message);
                }

                return;
            }

            try
            {
                Process sdbinst = Process.Start("sdbinst.exe", "-q \"" + ProgramConstants.GamePath + "Resources/FSCompatFix.sdb\"");

                sdbinst.WaitForExit();

                RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
                regKey = regKey.CreateSubKey("Tiberian Sun Client");
                regKey.SetValue("FSCompatFixInstalled", "Yes");

                btnMapEditorCompatibilityFix.Text = "禁用";

                Logger.Log("FinalSun Compatibility Fix succesfully installed.");
                XNAMessageBox.Show(WindowManager, "兼容性修复已安装",
                    "FinalSun兼容性修复已成功安装。");

                FinalSunCompatFixInstalled = true;
            }
            catch (Exception ex)
            {
                Logger.Log("Installing FinalSun Compatibility Fix failed. Error message: " + ex.Message);
                XNAMessageBox.Show(WindowManager, "安装兼容性修复失败",
                    "安装FinalSun兼容性修复失败。错误信息:" + " " + ex.Message);
            }
        }
#endif

        private void ChkBorderlessMenu_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBorderlessClient.Checked)
            {
                ddClientResolution.AllowDropDown = false;
#if WINFORMS
                string nativeRes = Screen.PrimaryScreen.Bounds.Width +
                    "x" + Screen.PrimaryScreen.Bounds.Height;

                int nativeResIndex = ddClientResolution.Items.FindIndex(i => (string)i.Tag == nativeRes);
                if (nativeResIndex > -1)
                    ddClientResolution.SelectedIndex = nativeResIndex;
#endif
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
        /// Loads the user's preferred renderer.
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

            // Wonder what this "Win8CompatMode" actually does..
            // Disabling it used to be TS-DDRAW only, but it was never enabled after 
            // you had tried TS-DDRAW once, so most players probably have it always
            // disabled anyway
            IniSettings.Win8CompatMode.Value = "No";

            var renderer = (DirectDrawWrapper)ddRenderer.SelectedItem.Tag;

            if (renderer.UsesCustomWindowedOption())
            {
                // For renderers that have their own windowed mode implementation
                // enabled through their own config INI file
                // (for example DxWnd and CnC-DDRAW)

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

            //�����ֽ
            chkRandom_wallpaper.Checked = UserINISettings.Instance.Random_wallpaper;

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

#if TS
            chkBackBufferInVRAM.Checked = !UserINISettings.Instance.BackBufferInVRAM;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Tiberian Sun Client");

            if (regKey == null)
                return;

            object tsCompatFixValue = regKey.GetValue("TSCompatFixInstalled", "No");
            string tsCompatFixString = (string)tsCompatFixValue;

            if (tsCompatFixString == "Yes")
            {
                GameCompatFixInstalled = true;
                btnGameCompatibilityFix.Text = "禁用";
            }

            object fsCompatFixValue = regKey.GetValue("FSCompatFixInstalled", "No");
            string fsCompatFixString = (string)fsCompatFixValue;

            if (fsCompatFixString == "Yes")
            {
                FinalSunCompatFixInstalled = true;
                btnMapEditorCompatibilityFix.Text = "禁用";
            }

            object tsCompatFixDeclinedValue = regKey.GetValue("TSCompatFixDeclined", "No");

            if (((string)tsCompatFixDeclinedValue) == "Yes")
            {
                GameCompatFixDeclined = true;
            }

            //object fsCompatFixDeclinedValue = regKey.GetValue("FSCompatFixDeclined", "No");

            //if (((string)fsCompatFixDeclinedValue) == "Yes")
            //{
            //    FinalSunCompatFixDeclined = true;
            //}
#else
            chkBackBufferInVRAM.Checked = UserINISettings.Instance.BackBufferInVRAM;
#endif
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
                    CopyDirectory(folder, dest);//����Ŀ��·��,�ݹ鸴���ļ�
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

            // Calculate drag selection distance, scale it with resolution width
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

            //�����ֽ
            IniSettings.Random_wallpaper.Value = chkRandom_wallpaper.Checked;

#if TS
            IniSettings.BackBufferInVRAM.Value = !chkBackBufferInVRAM.Checked;
#else
            IniSettings.BackBufferInVRAM.Value = chkBackBufferInVRAM.Checked;
#endif

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

#if TS
            if (ClientConfiguration.Instance.CopyResolutionDependentLanguageDLL)
            {
                string languageDllDestinationPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "Language.dll");

                SafePath.DeleteFileIfExists(languageDllDestinationPath);

                if (ingameRes[0] >= 1024 && ingameRes[1] >= 720)
                    System.IO.File.Copy(SafePath.CombineFilePath(ProgramConstants.GamePath, "Resources", "language_1024x720.dll"), languageDllDestinationPath);
                else if (ingameRes[0] >= 800 && ingameRes[1] >= 600)
                    System.IO.File.Copy(SafePath.CombineFilePath(ProgramConstants.GamePath, "Resources", "language_800x600.dll"), languageDllDestinationPath);
                else
                    System.IO.File.Copy(SafePath.CombineFilePath(ProgramConstants.GamePath, "Resources", "language_640x480.dll"), languageDllDestinationPath);
            }
#endif

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

                // SupportedDisplayModes can include the same resolution multiple times
                // because it takes the refresh rate into consideration.
                // Which means that we have to check if the resolution is already listed
                if (screenResolutions.Find(res => res.Equals(resolution)) != null)
                    continue;

                screenResolutions.Add(resolution);
            }

            //����1000*600�ֱ���֧��
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
        /// A single screen resolution.
        /// </summary>
        sealed class ScreenResolution : IComparable<ScreenResolution>
        {
            public ScreenResolution(int width, int height)
            {
                Width = width;
                Height = height;
            }

            /// <summary>
            /// The width of the resolution in pixels.
            /// </summary>
            public int Width { get; set; }

            /// <summary>
            /// The height of the resolution in pixels.
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
                else // equal
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
