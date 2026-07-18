using System;
using ClientCore;
using ClientGUI;
using Rampastring.Tools;
using Rampastring.XNAUI;

namespace DTAConfig.Settings
{
    /// <summary>
    /// 用于切换用户设置INI文件中选项的复选框。
    /// </summary>
    public class SettingCheckBox : SettingCheckBoxBase
    {
        public SettingCheckBox(WindowManager windowManager) : base(windowManager)
        {
        }

        public SettingCheckBox(WindowManager windowManager, bool defaultValue, string settingSection, string settingKey,
            bool writeSettingValue = false, string enabledValue = "", string disabledValue = "", bool restartRequired = false)
            : base(windowManager, defaultValue, settingSection, settingKey, restartRequired)
        {
            WriteSettingValue = writeSettingValue;
            EnabledSettingValue = enabledValue;
            DisabledSettingValue = disabledValue;
        }

        private bool _writeSettingValue;
        /// <summary>
        /// 如果设置，在读写用户设置INI时使用单独的启用/禁用值，而非复选框的选中状态。
        /// </summary>
        public bool WriteSettingValue
        {
            get => _writeSettingValue;
            set
            {
                _writeSettingValue = value;
                defaultKeySuffix = _writeSettingValue ? "_Value" : "_Checked";
            }
        }

        /// <summary>
        /// 复选框启用时要写入的值（替代true）。
        /// </summary>
        public string EnabledSettingValue { get; set; } = string.Empty;

        /// <summary>
        /// 复选框禁用时要写入的值（替代false）。
        /// </summary>
        public string DisabledSettingValue { get; set; } = string.Empty;

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "WriteSettingValue":
                    WriteSettingValue = Conversions.BooleanFromString(value, false);
                    return;
                case "EnabledSettingValue":
                    EnabledSettingValue = value;
                    return;
                case "DisabledSettingValue":
                    DisabledSettingValue = value;
                    return;
            }

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        public override void Load()
        {
            string value = UserINISettings.Instance.GetValue(SettingSection, SettingKey, string.Empty);

            if (WriteSettingValue)
            {
                if (value == EnabledSettingValue)
                    Checked = true;
                else if (value == DisabledSettingValue)
                    Checked = false;
                else
                    Checked = DefaultValue;
            }
            else
                Checked = Conversions.BooleanFromString(value, DefaultValue);

            originalState = Checked;
        }

        public override bool Save()
        {
            if (WriteSettingValue)
                UserINISettings.Instance.SetValue(SettingSection, SettingKey, Checked ? EnabledSettingValue : DisabledSettingValue);
            else
                UserINISettings.Instance.SetValue(SettingSection, SettingKey, Checked);

            return RestartRequired && (Checked != originalState);
        }
    }
}
