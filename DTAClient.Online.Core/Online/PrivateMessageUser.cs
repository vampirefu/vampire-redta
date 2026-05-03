using System.Collections.Generic;

namespace DTAClient.Online
{
    public class PrivateMessageUser
    {
        public PrivateMessageUser(IRCUser user)
        {
            IrcUser = user;
        }

        public IRCUser IrcUser { get; private set; }

        public List<ChatMessage> Messages = new List<ChatMessage>();
    }
}
