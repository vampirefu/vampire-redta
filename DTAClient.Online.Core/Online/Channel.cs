using ClientCore;
using DTAClient.Online.EventArguments;
using System;
using System.Collections.Generic;
namespace DTAClient.Online
{
    public class Channel : IMessageView
    {
        const int MESSAGE_LIMIT = 1024;

        public event EventHandler<ChannelUserEventArgs> UserAdded;
        public event EventHandler<UserNameEventArgs> UserLeft;
        public event EventHandler<UserNameEventArgs> UserKicked;
        public event EventHandler<UserNameEventArgs> UserQuitIRC;
        public event EventHandler<ChannelUserEventArgs> UserGameIndexUpdated;
        public event EventHandler<UserNameChangedEventArgs> UserNameChanged;
        public event EventHandler UserListReceived;
        public event EventHandler UserListCleared;

        public event EventHandler<IRCMessageEventArgs> MessageAdded;
        public event EventHandler<ChannelModeEventArgs> ChannelModesChanged;
        public event EventHandler<ChannelCTCPEventArgs> CTCPReceived;
        public event EventHandler InvalidPasswordEntered;
        public event EventHandler InviteOnlyErrorOnJoin;

        /// <summary>
        /// 当服务器通知客户端无法加入频道因为频道已满时引发。
        /// </summary>
        public event EventHandler ChannelFull;

        /// <summary>
        /// 当服务器通知客户端无法加入频道因为客户端尝试过快加入过多频道时引发。
        /// </summary>
        public event EventHandler<MessageEventArgs> TargetChangeTooFast;

        public Channel(string uiName, string channelName, bool persistent, bool isChatChannel, string password, Connection connection)
        {
            if (isChatChannel)
                users = new SortedUserCollection<ChannelUser>(ChannelUser.ChannelUserComparison);
            else
                users = new UnsortedUserCollection<ChannelUser>();

            UIName = uiName;
            ChannelName = channelName.ToLowerInvariant();
            Persistent = persistent;
            IsChatChannel = isChatChannel;
            Password = password;
            this.connection = connection;

            if (persistent)
            {
                Instance_SettingsSaved(null, EventArgs.Empty);
                UserINISettings.Instance.SettingsSaved += Instance_SettingsSaved;
            }
        }

        #region Public members

        public string UIName { get; }

        public string ChannelName { get; }

        public bool Persistent { get; }

        public bool IsChatChannel { get; }

        public string Password { get; private set; }

        private readonly Connection connection;

        string _topic;
        public string Topic
        {
            get { return _topic; }
            set
            {
                _topic = value;
                if (Persistent)
                    AddMessage(new ChatMessage(
                        string.Format("{0}话题是:{1}", UIName, _topic)));
            }
        }

        List<ChatMessage> messages = new List<ChatMessage>();
        public List<ChatMessage> Messages => messages;

        IUserCollection<ChannelUser> users;
        public IUserCollection<ChannelUser> Users => users;

        #endregion

        bool notifyOnUserListChange = true;

        private void Instance_SettingsSaved(object sender, EventArgs e)
        {
#if YR
            notifyOnUserListChange = false;
#else
            notifyOnUserListChange = UserINISettings.Instance.NotifyOnUserListChange;
#endif
        }

        public void AddUser(ChannelUser user)
        {
            users.Add(user.IRCUser.Name, user);
            UserAdded?.Invoke(this, new ChannelUserEventArgs(user));
        }

        public void OnUserJoined(ChannelUser user)
        {
            AddUser(user);

            if (notifyOnUserListChange)
            {
                AddMessage(new ChatMessage(
                    string.Format("{0}已加入{1}.", user.IRCUser.Name, UIName)));
            }

#if !YR
            if (Persistent && IsChatChannel && user.IRCUser.Name == ProgramConstants.PLAYERNAME)
                RequestUserInfo();
#endif
        }

        public void OnUserListReceived(List<ChannelUser> userList)
        {
            for (int i = 0; i < userList.Count; i++)
            {
                ChannelUser user = userList[i];
                var existingUser = users.Find(user.IRCUser.Name);
                if (existingUser == null)
                {
                    users.Add(user.IRCUser.Name, user);
                }
                else if (IsChatChannel)
                {
                    if (existingUser.IsAdmin != user.IsAdmin)
                    {
                        existingUser.IsAdmin = user.IsAdmin;
                        existingUser.IsFriend = user.IsFriend;
                        users.Reinsert(user.IRCUser.Name);
                    }
                }
            }

            UserListReceived?.Invoke(this, EventArgs.Empty);
        }

        public void OnUserKicked(string userName)
        {
            if (users.Remove(userName))
            {
                if (userName == ProgramConstants.PLAYERNAME)
                {
                    users.Clear();
                }

                AddMessage(new ChatMessage(
                    string.Format("{0}被踢出{1}.", userName, UIName)));

                UserKicked?.Invoke(this, new UserNameEventArgs(userName));
            }
        }

        public void OnUserLeft(string userName)
        {
            if (users.Remove(userName))
            {
                if (notifyOnUserListChange)
                {
                    AddMessage(new ChatMessage(
                         string.Format("{0}已离开{1}.", userName, UIName)));
                }

                UserLeft?.Invoke(this, new UserNameEventArgs(userName));
            }
        }

        public void OnUserQuitIRC(string userName)
        {
            if (users.Remove(userName))
            {
                if (notifyOnUserListChange)
                {
                    AddMessage(new ChatMessage(
                        string.Format("{0}已退出CnCNet.", userName)));
                }

                UserQuitIRC?.Invoke(this, new UserNameEventArgs(userName));
            }
        }

        public void UpdateGameIndexForUser(string userName)
        {
            var user = users.Find(userName);
            if (user != null)
                UserGameIndexUpdated?.Invoke(this, new ChannelUserEventArgs(user));
        }

        public void OnUserNameChanged(string oldUserName, string newUserName)
        {
            var user = users.Find(oldUserName);
            if (user != null)
            {
                users.Remove(oldUserName);
                users.Add(newUserName, user);
                UserNameChanged?.Invoke(this, new UserNameChangedEventArgs(oldUserName, user.IRCUser));
            }
        }

        public void OnChannelModesChanged(string sender, string modes)
        {
            ChannelModesChanged?.Invoke(this, new ChannelModeEventArgs(sender, modes));
        }

        public void OnCTCPReceived(string userName, string message)
        {
            CTCPReceived?.Invoke(this, new ChannelCTCPEventArgs(userName, message));
        }

        public void OnInvalidJoinPassword()
        {
            InvalidPasswordEntered?.Invoke(this, EventArgs.Empty);
        }

        public void OnInviteOnlyOnJoin()
        {
            InviteOnlyErrorOnJoin?.Invoke(this, EventArgs.Empty);
        }

        public void OnChannelFull()
        {
            ChannelFull?.Invoke(this, EventArgs.Empty);
        }

        public void OnTargetChangeTooFast(string message)
        {
            TargetChangeTooFast?.Invoke(this, new MessageEventArgs(message));
        }

        public void AddMessage(ChatMessage message)
        {
            if (messages.Count == MESSAGE_LIMIT)
                messages.RemoveAt(0);

            messages.Add(message);

            MessageAdded?.Invoke(this, new IRCMessageEventArgs(message));
        }

        public void SendChatMessage(string message, IRCColor color)
        {
            AddMessage(new ChatMessage(ProgramConstants.PLAYERNAME, color.XnaColor, DateTime.Now, message));

            string colorString = ((char)03).ToString() + color.IrcColorId.ToString("D2");

            connection.QueueMessage(QueuedMessageType.CHAT_MESSAGE, 0,
                "PRIVMSG " + ChannelName + " :" + colorString + message);
        }

        /// <param name="message">消息内容。</param>
        /// <param name="qmType">队列消息类型。</param>
        /// <param name="priority">优先级。</param>
        /// <param name="replace">
        ///     可用于帮助防止快速更改多个选项导致的洪水问题。它允许用单条消息处理多次更改。
        /// </param>
        public void SendCTCPMessage(string message, QueuedMessageType qmType, int priority, bool replace = false)
        {
            char CTCPChar1 = (char)58;
            char CTCPChar2 = (char)01;

            connection.QueueMessage(qmType, priority,
                "NOTICE " + ChannelName + " " + CTCPChar1 + CTCPChar2 + message + CTCPChar2, replace);
        }

        /// <summary>
        /// 向频道发送"踢出用户"消息。
        /// </summary>
        /// <param name="userName">应被踢出的用户名称。</param>
        /// <param name="priority">消息在发送队列中的优先级。</param>
        public void SendKickMessage(string userName, int priority)
        {
            connection.QueueMessage(QueuedMessageType.INSTANT_MESSAGE, priority, "KICK " + ChannelName + " " + userName);
        }

        /// <summary>
        /// 向频道发送"封禁主机"消息。
        /// </summary>
        /// <param name="host">应被封禁的主机。</param>
        /// <param name="priority">消息在发送队列中的优先级。</param>
        public void SendBanMessage(string host, int priority)
        {
            connection.QueueMessage(QueuedMessageType.INSTANT_MESSAGE, priority,
                string.Format("MODE {0} +b *!*@{1}", ChannelName, host));
        }

        public void Join()
        {
            // 加入前等待随机时间以防止加入/离开洪水
            if (Persistent)
            {
                int rn = connection.Rng.Next(1, 10000);

                if (string.IsNullOrEmpty(Password))
                    connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, rn, "JOIN " + ChannelName);
                else
                    connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, rn, "JOIN " + ChannelName + " " + Password);
            }
            else
            {
                if (string.IsNullOrEmpty(Password))
                    connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, "JOIN " + ChannelName);
                else
                    connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, "JOIN " + ChannelName + " " + Password);
            }
        }

        public void RequestUserInfo()
        {
            connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, "WHO " + ChannelName);
        }

        public void Leave()
        {
            // 离开前等待随机时间以防止加入/离开洪水
            if (Persistent)
            {
                int rn = connection.Rng.Next(1, 10000);
                connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, rn, "PART " + ChannelName);
            }
            else
            {
                connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 9, "PART " + ChannelName);
            }
            ClearUsers();
        }

        public void ClearUsers()
        {
            users.Clear();
            UserListCleared?.Invoke(this, EventArgs.Empty);
        }
    }

    public class ChannelUserEventArgs : EventArgs
    {
        public ChannelUserEventArgs(ChannelUser user)
        {
            User = user;
        }

        public ChannelUser User { get; private set; }
    }

    public class UserNameIndexEventArgs : EventArgs
    {
        public UserNameIndexEventArgs(int index, string userName)
        {
            UserIndex = index;
            UserName = userName;
        }

        public int UserIndex { get; private set; }
        public string UserName { get; private set; }
    }

    public class UserNameEventArgs : EventArgs
    {
        public UserNameEventArgs(string userName)
        {
            UserName = userName;
        }

        public string UserName { get; private set; }
    }

    public class IRCMessageEventArgs : EventArgs
    {
        public IRCMessageEventArgs(ChatMessage ircMessage)
        {
            Message = ircMessage;
        }

        public ChatMessage Message { get; private set; }
    }

    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}
