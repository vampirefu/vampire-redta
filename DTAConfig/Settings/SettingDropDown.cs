using ClientCore;
using ClientGUI;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAConfig.Settings
{
    /// <summary>
    /// 用于切换用户设置INI文件中选项的下拉框。
    /// </summary>
    public class SettingDropDown : SettingDropDownBase
    {
        public SettingDropDown(WindowManager windowManager) : base(windowManager) {}

        public SettingDropDown(WindowManager windowManager, int defaultValue, string settingSection, string settingKey, bool writeItemValue = false, bool restartRequired = false)
            : base(windowManager, defaultValue, settingSection, settingKey, restartRequired)
        {
            WriteItemValue = writeItemValue;
        }

        private bool _writeItemValue;
        /// <summary>
        /// 如果设置，将下拉框项的值而非索引写入用户设置INI。
        /// </summary>
        public bool WriteItemValue
        {
            get => _writeItemValue;
            set
            {
                _writeItemValue = value;
                defaultKeySuffix = _writeItemValue ? "_Value" : "_SelectedIndex";
            }
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "WriteItemValue":
                    WriteItemValue = Conversions.BooleanFromString(value, false);
                    return;
            }

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        public override void Load()
        {
            if (WriteItemValue)
                SelectedIndex = FindItemIndexByValue(UserINISettings.Instance.GetValue(SettingSection, SettingKey, null));
            else
                SelectedIndex = UserINISettings.Instance.GetValue(SettingSection, SettingKey, DefaultValue);

            originalState = SelectedIndex;
        }

        public override bool Save()
        {
            if (WriteItemValue)
                UserINISettings.Instance.SetValue(SettingSection, SettingKey, SelectedItem.Text);
            else
                UserINISettings.Instance.SetValue(SettingSection, SettingKey, SelectedIndex);

            return RestartRequired && (SelectedIndex != originalState);
        }

        private int FindItemIndexByValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return DefaultValue;

            int index = Items.FindIndex(x => x.Text == value);

            if (index < 0)
                return DefaultValue;

            return index;
        }
    }
}
