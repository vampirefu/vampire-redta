using Microsoft.Xna.Framework.Graphics;

namespace ClientCore.CnCNet5
{
    /// <summary>
    /// CnCNet 支持的游戏类（DTA、TI、TS、RA1/2 等）
    /// </summary>
    public class CnCNetGame
    {
        /// <summary>
        /// 在用户界面上显示的游戏名称。
        /// </summary>
        public string UIName { get; set; }

        /// <summary>
        /// 游戏的内部名称（后缀）。
        /// </summary>
        public string InternalName { get; set; }

        /// <summary>
        /// 游戏的 IRC 聊天频道 ID。
        /// </summary>
        public string ChatChannel { get; set; }

        /// <summary>
        /// 游戏的 IRC 游戏广播频道 ID。
        /// </summary>
        public string GameBroadcastChannel { get; set; }

        /// <summary>
        /// 游戏客户端的可执行文件名称。
        /// </summary>
        public string ClientExecutableName { get; set; }

        public Texture2D Texture { get; set; }

        /// <summary>
        /// 从注册表中读取游戏安装路径的位置。
        /// </summary>
        public string RegistryInstallPath { get; set; }

        private bool supported = true;

        /// <summary>
        /// 确定此客户端是否正确支持该游戏。默认为 true。
        /// </summary>
        public bool Supported
        {
            get { return supported; }
            set { supported = value; }
        }

        /// <summary>
        /// 如果为 true，客户端应始终连接到此游戏的聊天频道。
        /// </summary>
        public bool AlwaysEnabled { get; set; }
    }
}
