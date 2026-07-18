namespace DTAConfig.Settings
{
    interface IFileSetting : IUserSetting
    {
        /// <summary>
        /// 确定是否在运行时检查设置的可用性。
        /// </summary>
        bool CheckAvailability { get; }

        /// <summary>
        /// 确定当当前值变得不可用时，客户端是否自动调整设置值。
        /// </summary>
        bool ResetUnavailableValue { get; }

        /// <summary>
        /// 刷新设置以应对可能影响其功能的变更。
        /// </summary>
        /// <returns>一个布尔值，指示设置的值是否已变更。</returns>
        bool RefreshSetting();
    }
}
