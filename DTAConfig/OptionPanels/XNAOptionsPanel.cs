using ClientCore;
using ClientGUI;
using DTAConfig.Settings;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System.Collections.Generic;

namespace DTAConfig.OptionPanels
{
    /// <summary>
    /// 所有选项面板的基类。
    /// 处理INI文件中定义的自定义游戏特定面板选项。
    /// </summary>
    internal abstract class XNAOptionsPanel : XNAWindowBase
    {
        public XNAOptionsPanel(WindowManager windowManager, 
            UserINISettings iniSettings) : base(windowManager)
        {
            IniSettings = iniSettings;
        }

        private readonly List<IUserSetting> userSettings = new List<IUserSetting>();

        public override void Initialize()
        {
            ClientRectangle = new Rectangle(12, 47,
                Parent.Width - 24,
                Parent.Height - 94);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 2, 2);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            base.Initialize();
        }

        /// <summary>
        /// 从INI文件中解析用户定义的游戏选项。
        /// </summary>
        /// <param name="iniFile">INI文件。</param>
        public void ParseUserOptions(IniFile iniFile)
        {
            GetAttributes(iniFile);
            ParseExtraControls(iniFile, Name + "ExtraControls");
            ReadChildControlAttributes(iniFile);
        }

        public override void AddChild(XNAControl child)
        {
            base.AddChild(child);

            if (child is IUserSetting setting)
                userSettings.Add(setting);
        }

        protected UserINISettings IniSettings { get; private set; }

        /// <summary>
        /// 保存此面板的选项。
        /// <returns>一个布尔值，指示客户端是否需要重启以应用更改。</returns>
        /// </summary>
        public virtual bool Save()
        {
            bool restartRequired = false;
            foreach (var setting in userSettings)
                restartRequired = setting.Save() || restartRequired;

            return restartRequired;
        }

        /// <summary>
        /// 刷新面板设置以应对可能影响功能的变更。
        /// </summary>
        /// <returns>一个布尔值，指示设置的值是否已变更。</returns>
        public virtual bool RefreshPanel()
        {
            bool valuesChanged = false;
            foreach (var setting in userSettings)
            {
                if (setting is IFileSetting fileSetting)
                    valuesChanged = fileSetting.RefreshSetting() || valuesChanged;
            }

            return valuesChanged;
        }

        /// <summary>
        /// 加载此面板的选项。
        /// </summary>
        public virtual void Load()
        {
            foreach (var setting in userSettings)
                setting.Load();
        }

        /// <summary>
        /// 启用或禁用仅在主菜单中打开选项窗口时才可用的选项。
        /// </summary>
        /// <param name="enable">如果为true则启用选项，如果为false则禁用。</param>
        public virtual void ToggleMainMenuOnlyOptions(bool enable)
        {
        }
    }
}
