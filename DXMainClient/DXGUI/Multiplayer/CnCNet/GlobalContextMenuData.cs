using DTAClient.Online;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class GlobalContextMenuData
    {
        /// <summary>
        /// 要为其显示菜单的频道用户。
        /// </summary>
        public ChannelUser ChannelUser { get; set; }

        /// <summary>
        /// 要为其显示菜单的聊天消息。
        /// </summary>
        public ChatMessage ChatMessage { get; set; }

        /// <summary>
        /// 要为其显示菜单的IRC用户。
        /// </summary>
        public IRCUser IrcUser { get; set; }

        /// <summary>
        /// 要为其显示菜单的玩家。用于在内部确定IRCUser。
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// 邀请属性用于菜单中的邀请选项。
        /// </summary>
        public string inviteChannelName { get; set; }
        public string inviteGameName { get; set; }
        public string inviteChannelPassword { get; set; }

        /// <summary>
        /// 阻止在菜单中显示加入选项。
        /// </summary>
        public bool PreventJoinGame { get; set; }
    }
}
