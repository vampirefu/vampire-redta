using System;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    /// <summary>
    /// 一种命令，可以通过在多人游戏大厅的聊天框中输入以/开头的消息来执行。
    /// </summary>
    public class ChatBoxCommand
    {
        public ChatBoxCommand(string command, string description, bool hostOnly, Action<string> action)
        {
            Command = command;
            Description = description;
            HostOnly = hostOnly;
            Action = action;
        }

        public string Command { get; private set; }
        public string Description { get; private set; }
        public bool HostOnly { get; private set; }
        public Action<string> Action { get; private set; }
    }
}
