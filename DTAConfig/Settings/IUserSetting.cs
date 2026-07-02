namespace DTAConfig.Settings
{
    interface IUserSetting
    {

        /// <summary>
        /// 用户设置文件中存储此设置值的INI节名称。
        /// </summary>
        string SettingSection { get; }

        /// <summary>
        /// 用户设置文件中存储此设置值的INI键名称。
        /// </summary>
        string SettingKey { get; }

        /// <summary>
        /// 确定此设置是否需要重启客户端才能正确应用。
        /// </summary>
        bool RestartRequired { get; }

        /// <summary>
        /// 加载用户设置的当前值。
        /// </summary>
        void Load();

        /// <summary>
        /// 根据当前设置状态应用操作。
        /// </summary>
        /// <returns>一个布尔值，指示客户端是否需要重启以应用更改。</returns>
        bool Save();
    }
}
