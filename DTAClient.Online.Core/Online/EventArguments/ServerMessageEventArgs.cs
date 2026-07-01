using System;

namespace DTAClient.Online.EventArguments
{
    public class ServerMessageEventArgs : EventArgs
    {
        public ServerMessageEventArgs(string message)
        {
            Message = message;
        }
        public string Message { get; private set; }
    }
}