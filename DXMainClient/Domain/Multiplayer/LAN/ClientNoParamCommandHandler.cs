using System;

namespace DTAClient.Domain.Multiplayer.LAN
{
    /// <summary>
    /// 无参数的命令处理器。
    /// </summary>
    class ClientNoParamCommandHandler : LANClientCommandHandler
    {
        public ClientNoParamCommandHandler(string commandName, Action commandHandler) : base(commandName)
        {
            this.commandHandler = commandHandler;
        }

        Action commandHandler;

        public override bool Handle(string message)
        {
            if (message != CommandName)
                return false;

            commandHandler();
            return true;
        }
    }
}
