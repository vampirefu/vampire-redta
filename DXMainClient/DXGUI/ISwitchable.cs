namespace DTAClient.DXGUI
{
    /// <summary>
    /// 所有可切换窗口的接口。
    /// </summary>
    public interface ISwitchable
    {
        void SwitchOn();

        void SwitchOff();

        string GetSwitchName();
    }
}
