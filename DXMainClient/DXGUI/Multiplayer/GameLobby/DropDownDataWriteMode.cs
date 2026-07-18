namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    /// <summary>
    /// 用于控制游戏大厅下拉控件数据如何写入spawn INI的枚举。
    /// </summary>
    public enum DropDownDataWriteMode
    {
        /// <summary>
        /// 下拉控件的基于0的选中索引将被写入INI。
        /// </summary>
        INDEX,

        /// <summary>
        /// 如果选中索引0，将写入"false"。
        /// 否则客户端将写入"true"。
        /// </summary>
        BOOLEAN,

        /// <summary>
        /// 下拉框在UI中显示的值将被写入INI。
        /// </summary>
        STRING,

        /// <summary>
        /// 下拉框的值是地图代码INI文件的文件名，将被应用到地图。
        /// 不会向spawn INI写入任何内容。
        /// </summary>
        MAPCODE
    }
}
