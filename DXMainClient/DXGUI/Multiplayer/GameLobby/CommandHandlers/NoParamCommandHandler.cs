using System;

namespace DTAClient.DXGUI.Multiplayer.GameLobby.CommandHandlers
{
    /// <summary>
    /// 一种命令处理器，处理除发送者之外没有其他参数的命令。
    /// </summary>
    public class NoParamCommandHandler : CommandHandlerBase
    {
        public NoParamCommandHandler(string commandName, Action<string> commandHandler) : base(commandName)
        {
            this.commandHandler = commandHandler;
        }

        Action<string> commandHandler;

        public override bool Handle(string sender, string message)
        {
            if (message == CommandName)
            {
                commandHandler(sender);
                return true;
            }

            return false;
        }
    }
}
