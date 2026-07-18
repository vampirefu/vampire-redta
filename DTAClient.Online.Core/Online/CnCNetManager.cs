using ClientCore;
using ClientCore.CnCNet5;
using DTAClient.Online.EventArguments;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DTAClient.Online
{
    /// <summary>
    /// 作为 CnCNet 连接类与用户界面类之间的接口。
    /// </summary>
    public class CnCNetManager : IConnectionManager
    {
        // 在实现 IConnectionManager 函数时，请特别注意线程安全。
        // IConnectionManager 中的函数通常从网络线程调用，因此如果它们
        // 影响到 UI 中的任何内容或影响到 UI 线程可能正在读取的数据，
        // 请使用 WindowManager.AddCallback 在 UI 线程上执行函数，
        // 而不是直接修改数据或引发事件。

        public delegate void UserListDelegate(string channelName, string[] userNames);

        public event EventHandler<ServerMessageEventArgs> WelcomeMessageReceived;
        public event EventHandler<UserAwayEventArgs> AwayMessageReceived;
        public event EventHandler<WhoEventArgs> WhoReplyReceived;
        public event EventHandler<CnCNetPrivateMessageEventArgs> PrivateMessageReceived;
        public event EventHandler<PrivateCTCPEventArgs> PrivateCTCPReceived;
        public event EventHandler<ChannelEventArgs> BannedFromChannel;

        public event EventHandler<AttemptedServerEventArgs> AttemptedServerChanged;
        public event EventHandler ConnectAttemptFailed;
        public event EventHandler<ConnectionLostEventArgs> ConnectionLost;
        public event EventHandler ReconnectAttempt;
        public event EventHandler Disconnected;
        public event EventHandler Connected;

        public event EventHandler<UserEventArgs> UserAdded;
        public event EventHandler<UserEventArgs> UserGameIndexUpdated;
        public event EventHandler<UserNameIndexEventArgs> UserRemoved;
        public event EventHandler MultipleUsersAdded;

        public CnCNetManager(WindowManager wm, GameCollection gc, CnCNetUserData cncNetUserData)
        {
            gameCollection = gc;
            this.cncNetUserData = cncNetUserData;
            connection = new Connection(this);

            this.wm = wm;

            cDefaultChatColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.DefaultChatColor);

            ircChatColors = new IRCColor[]
            {
                new IRCColor("默认颜色", false, cDefaultChatColor, 0),
                new IRCColor("默认颜色#2", false, cDefaultChatColor, 1),
                new IRCColor("亮蓝色", true, Color.LightBlue, 2),
                new IRCColor("绿色", true, Color.ForestGreen, 3),
                new IRCColor("深红色", true, new Color(180, 0, 0, 255), 4),
                new IRCColor("红色", true, Color.Red, 5),
                new IRCColor("紫色", true, Color.MediumOrchid, 6),
                new IRCColor("橙色", true, Color.Orange, 7),
                new IRCColor("黄色", true, Color.Yellow, 8),
                new IRCColor("柠檬绿色", true, Color.Lime, 9),
                new IRCColor("碧绿色", true, Color.Turquoise, 10),
                new IRCColor("天蓝色", true, Color.LightSkyBlue, 11),
                new IRCColor("浅紫色", true, Color.RoyalBlue, 12),
                new IRCColor("粉色", true, Color.Fuchsia, 13),
                new IRCColor("灰色", true, Color.LightGray, 14),
                new IRCColor("灰色#2", false, Color.Gray, 15)
            };
        }

        public Channel MainChannel { get; private set; }

        private bool connected = false;

        /// <summary>
        /// 获取一个值，该值确定客户端当前是否已连接到 CnCNet。
        /// </summary>
        public bool IsConnected
        {
            get { return connected; }
        }

        public bool IsAttemptingConnection
        {
            get { return connection.AttemptingConnection; }
        }

        /// <summary>
        /// 我们在 IRC 网络上可以看到的所有用户列表。
        /// </summary>
        public List<IRCUser> UserList = new List<IRCUser>();

        private Connection connection;

        private List<Channel> channels = new List<Channel>();

        private GameCollection gameCollection;
        private readonly CnCNetUserData cncNetUserData;

        private Color cDefaultChatColor;
        private IRCColor[] ircChatColors;

        private WindowManager wm;

        private bool disconnect = false;

        public bool IsCnCNetInitialized()
        {
            return Connection.IsIdSet();
        }

        /// <summary>
        /// 创建新频道的工厂方法。
        /// </summary>
        /// <param name="uiName">频道的用户界面名称。</param>
        /// <param name="channelName">频道的名称。</param>
        /// <param name="persistent">确定断开连接后频道信息是否仍保留在内存中。</param>
        /// <param name="password">频道的密码。无密码则使用 null。</param>
        /// <returns>一个频道。</returns>
        public Channel CreateChannel(string uiName, string channelName,
            bool persistent, bool isChatChannel, string password)
        {
            return new Channel(uiName, channelName, persistent, isChatChannel, password, connection);
        }

        public void AddChannel(Channel channel)
        {
            if (FindChannel(channel.ChannelName) != null)
                throw new ArgumentException("频道已经存在!", "channel");

            channels.Add(channel);
        }

        public void RemoveChannel(Channel channel)
        {
            if (channel.Persistent)
                throw new ArgumentException("常驻频道无法被移除.", "channel");

            channels.Remove(channel);
        }

        public IRCColor[] GetIRCColors()
        {
            return ircChatColors;
        }

        public void LeaveFromChannel(Channel channel)
        {
            connection.QueueMessage(QueuedMessageType.SYSTEM_MESSAGE, 10, "PART " + channel.ChannelName);

            if (!channel.Persistent)
                channels.Remove(channel);
        }

        public void SetMainChannel(Channel channel)
        {
            MainChannel = channel;
        }

        public void SendCustomMessage(QueuedMessage qm)
        {
            connection.QueueMessage(qm);
        }

        public void SendWhoIsMessage(string nick)
        {
            SendCustomMessage(new QueuedMessage($"WHOIS {nick}", QueuedMessageType.WHOIS_MESSAGE, 0));
        }

        public void OnAttemptedServerChanged(string serverName)
        {
            // AddCallback 对于线程安全是必要的；OnAttemptedServerChanged
            // 由网络线程调用，AddCallback 将 DoAttemptedServerChanged
            // 调度到主（UI）线程上执行。
            wm.AddCallback(new Action<string>(DoAttemptedServerChanged), serverName);
        }

        private void DoAttemptedServerChanged(string serverName)
        {
            MainChannel.AddMessage(new ChatMessage(
                string.Format("尝试连接到{0}", serverName)));
            AttemptedServerChanged?.Invoke(this, new AttemptedServerEventArgs(serverName));
        }

        public void OnAwayMessageReceived(string userName, string reason)
        {
            wm.AddCallback(new Action<string, string>(DoAwayMessageReceived), userName, reason);
        }

        private void DoAwayMessageReceived(string userName, string reason)
        {
            AwayMessageReceived?.Invoke(this, new UserAwayEventArgs(userName, reason));
        }

        public void OnChannelFull(string channelName)
        {
            wm.AddCallback(new Action<string>(DoChannelFull), channelName);
        }

        private void DoChannelFull(string channelName)
        {
            var channel = FindChannel(channelName);

            if (channel != null)
                channel.OnChannelFull();
        }

        public void OnTargetChangeTooFast(string channelName, string message)
        {
            wm.AddCallback(new Action<string, string>(DoTargetChangeTooFast), channelName, message);
        }

        private void DoTargetChangeTooFast(string channelName, string message)
        {
            var channel = FindChannel(channelName);

            if (channel != null)
                channel.OnTargetChangeTooFast(message);
        }

        public void OnChannelInviteOnly(string channelName)
        {
            wm.AddCallback(new Action<string>(DoChannelInviteOnly), channelName);
        }

        private void DoChannelInviteOnly(string channelName)
        {
            var channel = FindChannel(channelName);

            if (channel != null)
                channel.OnInviteOnlyOnJoin();
        }

        public void OnChannelModesChanged(string userName, string channelName, string modeString, List<string> modeParameters)
        {
            wm.AddCallback(new Action<string, string, string, List<string>>(DoChannelModesChanged),
                userName, channelName, modeString, modeParameters);
        }

        private void DoChannelModesChanged(string userName, string channelName, string modeString, List<string> modeParameters)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            ApplyChannelModes(channel, modeString, modeParameters);

            channel.OnChannelModesChanged(userName, modeString);
        }

        private void ApplyChannelModes(Channel channel, string modeString, List<string> modeParameters)
        {
            bool addMode = true;
            int parameterCount = 0;
            foreach (char modeChar in modeString)
            {
                if (modeChar == '+')
                    addMode = true;
                else if (modeChar == '-')
                    addMode = false;
                else
                {
                    switch (modeChar)
                    {
                        // 添加/移除用户的频道管理员状态。
                        case 'o':
                            if (parameterCount >= modeParameters.Count)
                                break;
                            string parameter = modeParameters[parameterCount++];
                            ChannelUser user = channel.Users.Find(parameter);
                            if (user == null)
                                break;
                            user.IsAdmin = addMode;
                            break;
                    }
                }
            }
        }

        public void OnChannelTopicReceived(string channelName, string topic)
        {
            wm.AddCallback(new Action<string, string>(DoChannelTopicReceived), channelName, topic);
        }

        private void DoChannelTopicReceived(string channelName, string topic)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            channel.Topic = topic;
        }

        public void OnChannelTopicChanged(string userName, string channelName, string topic)
        {
            wm.AddCallback(new Action<string, string>(DoChannelTopicReceived), channelName, topic);
        }

        public void OnChatMessageReceived(string receiver, string senderName, string ident, string message)
        {
            wm.AddCallback(new Action<string, string, string, string>(DoChatMessageReceived),
                receiver, senderName, ident, message);
        }

        private void DoChatMessageReceived(string receiver, string senderName, string ident, string message)
        {
            Channel channel = FindChannel(receiver);

            if (channel == null)
                return;

            Color foreColor;

            // 处理 ACTION
            if (message.Contains("ACTION"))
            {
                message = message.Remove(0, 7);
                message = "====> " + senderName + " " + message;
                senderName = String.Empty;

                // 将 Funky 的游戏标识符替换为真实游戏名称
                for (int i = 0; i < gameCollection.GameList.Count; i++)
                    // TODO 是否需要本地化？
                    message = message.Replace("new " + gameCollection.GetGameIdentifierFromIndex(i) + " game",
                        "new " + gameCollection.GetFullGameNameFromIndex(i) + " game");

                foreColor = Color.White;
            }
            else
            {
                // 颜色解析
                if (message.Contains(Convert.ToString((char)03)))
                {
                    if (message.Length < 3)
                    {
                        foreColor = cDefaultChatColor;
                    }
                    else
                    {
                        string colorString = message.Substring(1, 2);
                        message = message.Remove(0, 3);
                        int colorIndex = Conversions.IntFromString(colorString, -1);
                        // 尝试解析消息颜色信息；如果失败，使用默认颜色
                        if (colorIndex < ircChatColors.Length && colorIndex > -1)
                            foreColor = ircChatColors[colorIndex].XnaColor;
                        else
                            foreColor = cDefaultChatColor;
                    }
                }
                else
                    foreColor = cDefaultChatColor;
            }

            if (message.Length > 1 && message[message.Length - 1] == '\u001f')
                message = message.Remove(message.Length - 1);

            ChannelUser user = channel.Users.Find(senderName);
            bool senderIsAdmin = user != null && user.IsAdmin;

            channel.AddMessage(new ChatMessage(senderName, ident, senderIsAdmin, foreColor, DateTime.Now, message.Replace('\r', ' ')));
        }

        public void OnCTCPParsed(string channelName, string userName, string message)
        {
            wm.AddCallback(new Action<string, string, string>(DoCTCPParsed),
                channelName, userName, message);
        }

        private void DoCTCPParsed(string channelName, string userName, string message)
        {
            Channel channel = FindChannel(channelName);

            // 可能我们通过 PRIVMSG 收到了此 CTCP，在这种情况下
            // 我们期望第一个参数是用户名而不是频道名
            if (channel == null)
            {
                if (channelName == ProgramConstants.PLAYERNAME)
                {
                    PrivateCTCPEventArgs e = new PrivateCTCPEventArgs(userName, message);

                    PrivateCTCPReceived?.Invoke(this, e);
                }

                return;
            }

            channel.OnCTCPReceived(userName, message);
        }

        public void OnConnectAttemptFailed()
        {
            wm.AddCallback(new Action(DoConnectAttemptFailed), null);
        }

        private void DoConnectAttemptFailed()
        {
            ConnectAttemptFailed?.Invoke(this, EventArgs.Empty);

            MainChannel.AddMessage(new ChatMessage(Color.Red, "连接到CnCNet失败!"));
        }

        public void OnConnected()
        {
            wm.AddCallback(new Action(DoConnected), null);
        }

        private void DoConnected()
        {
            connected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            MainChannel.AddMessage(new ChatMessage("已连接到CnCNet."));
        }

        /// <summary>
        /// 当连接意外断开时调用。
        /// </summary>
        /// <param name="reason">断开原因。</param>
        public void OnConnectionLost(string reason)
        {
            wm.AddCallback(new Action<string>(DoConnectionLost), reason);
        }

        private void DoConnectionLost(string reason)
        {
            ConnectionLost?.Invoke(this, new ConnectionLostEventArgs(reason));

            for (int i = 0; i < channels.Count; i++)
            {
                if (!channels[i].Persistent)
                {
                    channels.RemoveAt(i);
                    i--;
                }
                else
                {
                    channels[i].ClearUsers();
                }
            }

            UserList.Clear();

            MainChannel.AddMessage(new ChatMessage(Color.Red, "CnCNet连接已丢失."));
            connected = false;
        }

        /// <summary>
        /// 断开与 CnCNet 的连接。
        /// </summary>
        public void Disconnect()
        {
            connection.Disconnect();
            disconnect = true;
        }

        /// <summary>
        /// 连接到 CnCNet。
        /// </summary>
        public void Connect()
        {
            disconnect = false;
            MainChannel.AddMessage(new ChatMessage("正在连接到CnCNet..."));
            connection.ConnectAsync();
        }

        /// <summary>
        /// 当连接被有意中止时调用。
        /// </summary>
        public void OnDisconnected()
        {
            wm.AddCallback(new Action(DoDisconnected), null);
        }

        private void DoDisconnected()
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (!channels[i].Persistent)
                {
                    channels.RemoveAt(i);
                    i--;
                }
                else
                {
                    channels[i].ClearUsers();
                }
            }

            MainChannel.AddMessage(new ChatMessage("您已断开CnCNet联机."));
            connected = false;

            UserList.Clear();

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void OnErrorReceived(string errorMessage)
        {
            MainChannel.AddMessage(new ChatMessage(Color.Red, errorMessage));
        }

        public void OnGenericServerMessageReceived(string message)
        {
            wm.AddCallback(new Action<string>(DoGenericServerMessageReceived), message);
        }

        private void DoGenericServerMessageReceived(string message)
        {
            MainChannel.AddMessage(new ChatMessage(message));
        }

        public void OnIncorrectChannelPassword(string channelName)
        {
            wm.AddCallback(new Action<string>(DoIncorrectChannelPassword), channelName);
        }

        private void DoIncorrectChannelPassword(string channelName)
        {
            var channel = FindChannel(channelName);
            if (channel != null)
                channel.OnInvalidJoinPassword();
        }

        public void OnNoticeMessageParsed(string notice, string userName)
        {
            // TODO 解析为私聊消息
        }

        public void OnPrivateMessageReceived(string sender, string message)
        {
            wm.AddCallback(new Action<string, string>(DoPrivateMessageReceived),
                sender, message);
        }

        private void DoPrivateMessageReceived(string sender, string message)
        {
            CnCNetPrivateMessageEventArgs e = new CnCNetPrivateMessageEventArgs(sender, message);

            PrivateMessageReceived?.Invoke(this, e);
        }

        public void OnReconnectAttempt()
        {
            wm.AddCallback(new Action(DoReconnectAttempt), null);
        }

        private void DoReconnectAttempt()
        {
            ReconnectAttempt?.Invoke(this, EventArgs.Empty);

            MainChannel.AddMessage(new ChatMessage("尝试重新连接到CnCNet..."));

            connection.ConnectAsync();
        }

        public void OnUserJoinedChannel(string channelName, string host, string userName, string ident)
        {
            wm.AddCallback(new Action<string, string, string, string>(DoUserJoinedChannel),
                channelName, host, userName, ident);
        }

        private void DoUserJoinedChannel(string channelName, string host, string userName, string userAddress)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            bool isAdmin = false;
            string name = userName;

            if (userName.StartsWith("@"))
            {
                isAdmin = true;
                name = userName.Remove(0, 1);
            }

            IRCUser ircUser = null;

            // 检查我们是否已从其他频道认识此用户
            // 出于性能原因，此处避免使用 LINQ
            foreach (var user in UserList)
            {
                if (user.Name == name)
                {
                    ircUser = (IRCUser)user.Clone();
                    break;
                }
            }

            // 如果我们不认识该用户，创建一个新用户
            if (ircUser == null)
            {
                string identifier = userAddress.Split('@')[0];
                string[] parts = identifier.Split('.');
                ircUser = new IRCUser(name, identifier, host);

                if (parts.Length > 1)
                {
                    ircUser.GameID = gameCollection.GameList.FindIndex(g => g.InternalName.ToUpper() == parts[0].Replace("~", string.Empty));
                }

                AddUserToGlobalUserList(ircUser);
            }

            var channelUser = new ChannelUser(ircUser);
            channelUser.IsAdmin = isAdmin;
            channelUser.IsFriend = cncNetUserData.IsFriend(channelUser.IRCUser.Name);

            ircUser.Channels.Add(channelName);
            channel.OnUserJoined(channelUser);

            //UserJoinedChannel?.Invoke(this, new ChannelUserEventArgs(channelName, userName));
        }

        private void AddUserToGlobalUserList(IRCUser user)
        {
            UserList.Add(user);
            UserList = UserList.OrderBy(u => u.Name).ToList();
            UserAdded?.Invoke(this, new UserEventArgs(user));
        }

        public void OnUserKicked(string channelName, string userName)
        {
            wm.AddCallback(new Action<string, string>(DoUserKicked),
                channelName, userName);
        }

        private void DoUserKicked(string channelName, string userName)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            channel.OnUserKicked(userName);

            if (userName == ProgramConstants.PLAYERNAME)
            {
                channel.Users.DoForAllUsers(user =>
                {
                    RemoveChannelFromUser(user.IRCUser.Name, channelName);
                });

                if (!channel.Persistent)
                    channels.Remove(channel);

                channel.ClearUsers();
                return;
            }

            RemoveChannelFromUser(userName, channelName);
        }

        public void OnUserLeftChannel(string channelName, string userName)
        {
            wm.AddCallback(new Action<string, string>(DoUserLeftChannel),
                channelName, userName);
        }

        private void DoUserLeftChannel(string channelName, string userName)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            channel.OnUserLeft(userName);

            if (userName == ProgramConstants.PLAYERNAME)
            {
                channel.Users.DoForAllUsers(user =>
                {
                    RemoveChannelFromUser(user.IRCUser.Name, channelName);
                });

                if (!channel.Persistent)
                    channels.Remove(channel);

                channel.ClearUsers();

                return;
            }

            RemoveChannelFromUser(userName, channelName);
        }

        /// <summary>
        /// 在全局用户列表中查找用户并从用户中移除一个频道。
        /// 如果用户剩下 0 个频道（意味着我们与该用户没有共同频道），
        /// 则从全局用户列表中移除该用户。
        /// </summary>
        /// <param name="userName">用户的名称。</param>
        /// <param name="channelName">频道的名称。</param>
        public void RemoveChannelFromUser(string userName, string channelName)
        {
            var userIndex = UserList.FindIndex(user => user.Name.ToLower() == userName.ToLower());
            if (userIndex > -1)
            {
                var ircUser = UserList[userIndex];
                ircUser.Channels.Remove(channelName);

                if (ircUser.Channels.Count == 0)
                {
                    UserList.RemoveAt(userIndex);
                    UserRemoved?.Invoke(this, new UserNameIndexEventArgs(userIndex, userName));
                }
            }
        }

        public void OnUserListReceived(string channelName, string[] userList)
        {
            wm.AddCallback(new UserListDelegate(DoUserListReceived),
                channelName, userList);
        }

        private void DoUserListReceived(string channelName, string[] userList)
        {
            Channel channel = FindChannel(channelName);

            if (channel == null)
                return;

            var channelUserList = new List<ChannelUser>();

            foreach (string userName in userList)
            {
                string name = userName;
                bool isAdmin = false;

                if (userName.StartsWith("@"))
                {
                    isAdmin = true;
                    name = userName.Substring(1);
                }
                else if (userName.StartsWith("+"))
                    name = userName.Substring(1);

                // 检查我们是否已从其他频道认识此 IRC 用户
                IRCUser ircUser = UserList.Find(u => u.Name == name);

                // 如果我们还不认识该用户，
                // 创建新的用户实例并将其添加到全局用户列表
                if (ircUser == null)
                {
                    ircUser = new IRCUser(name);
                    UserList.Add(ircUser);
                }

                var channelUser = new ChannelUser(ircUser);
                channelUser.IsAdmin = isAdmin;
                channelUser.IsFriend = cncNetUserData.IsFriend(channelUser.IRCUser.Name);

                channelUserList.Add(channelUser);
            }

            UserList = UserList.OrderBy(u => u.Name).ToList();
            MultipleUsersAdded?.Invoke(this, EventArgs.Empty);

            channel.OnUserListReceived(channelUserList);
        }

        public void OnUserQuitIRC(string userName)
        {
            wm.AddCallback(new Action<string>(DoUserQuitIRC), userName);
        }

        private void DoUserQuitIRC(string userName)
        {
            new List<Channel>(channels).ForEach(ch => ch.OnUserQuitIRC(userName));

            int userIndex = UserList.FindIndex(user => user.Name == userName);

            if (userIndex > -1)
            {
                UserList.RemoveAt(userIndex);
                UserRemoved?.Invoke(this, new UserNameIndexEventArgs(userIndex, userName));
            }
        }

        public void OnWelcomeMessageReceived(string message)
        {
            wm.AddCallback(new Action<string>(DoWelcomeMessageReceived), message);
        }


        /// <summary>
        /// 按指定内部名称查找频道，不区分大小写。
        /// </summary>
        /// <param name="channelName">频道的内部名称。</param>
        /// <returns>如果找到匹配名称的频道则返回该频道，否则返回 null。</returns>
        public Channel FindChannel(string channelName)
        {
            channelName = channelName.ToLower();

            foreach (var channel in channels)
            {
                if (channel.ChannelName.ToLower() == channelName)
                    return channel;
            }

            return null;
        }

        private void DoWelcomeMessageReceived(string message)
        {
            channels.ForEach(ch => ch.AddMessage(new ChatMessage(message)));

            WelcomeMessageReceived?.Invoke(this, new ServerMessageEventArgs(message));
        }

        public void OnWhoReplyReceived(string ident, string hostName, string userName, string extraInfo)
        {
            wm.AddCallback(new Action<string, string, string, string>(DoWhoReplyReceived),
                ident, hostName, userName, extraInfo);
        }

        private void DoWhoReplyReceived(string ident, string hostName, string userName, string extraInfo)
        {
            WhoReplyReceived?.Invoke(this, new WhoEventArgs(ident, userName, extraInfo));

            string[] eInfoParts = extraInfo.Split(' ');

            int gameIndex = -1;
            if (eInfoParts.Length > 2)
            {
                string gameName = eInfoParts[2];

                gameIndex = gameCollection.GetGameIndexFromInternalName(gameName);

                if (gameIndex == -1)
                    return;
            }

            var user = UserList.Find(u => u.Name == userName);
            if (user != null)
            {
                user.GameID = gameIndex;
                user.Ident = ident;
                user.Hostname = hostName;

                if (gameIndex != -1)
                {
                    channels.ForEach(ch => ch.UpdateGameIndexForUser(userName));
                    UserGameIndexUpdated?.Invoke(this, new UserEventArgs(user));
                }
            }
        }

        public bool GetDisconnectStatus()
        {
            return disconnect;
        }

        public void OnNameAlreadyInUse()
        {
            wm.AddCallback(new Action(DoNameAlreadyInUse), null);
        }

        /// <summary>
        /// 处理请求的名称已被其他 IRC 用户使用的情况。
        /// 在名称后添加下划线或将现有字符替换为下划线。
        /// </summary>
        private void DoNameAlreadyInUse()
        {
            var charList = ProgramConstants.PLAYERNAME.ToList();
            int maxNameLength = ClientConfiguration.Instance.MaxNameLength;

            if (charList.Count < maxNameLength)
                charList.Add('_');
            else
            {
                int lastNonUnderscoreIndex = charList.FindLastIndex(c => c != '_');

                if (lastNonUnderscoreIndex == -1)
                {
                    MainChannel.AddMessage(new ChatMessage(Color.White,
                        "您的名称无效或已被使用.请在登入窗口重设名称."));
                    UserINISettings.Instance.SkipConnectDialog.Value = false;
                    Disconnect();
                    return;
                }

                charList[lastNonUnderscoreIndex] = '_';
            }

            var sb = new StringBuilder();
            foreach (char c in charList)
                sb.Append(c);

            MainChannel.AddMessage(new ChatMessage(Color.White,
                string.Format("您的名称已被使用.重试为{0}...", sb.ToString())));

            ProgramConstants.PLAYERNAME = sb.ToString();
            connection.ChangeNickname();
        }

        public void OnBannedFromChannel(string channelName)
        {
            wm.AddCallback(new Action<string>(DoBannedFromChannel), channelName);
        }

        private void DoBannedFromChannel(string channelName)
        {
            BannedFromChannel?.Invoke(this, new ChannelEventArgs(channelName));
        }

        public void OnUserNicknameChange(string oldNickname, string newNickname)
            => wm.AddCallback(new Action<string, string>(DoUserNicknameChange), oldNickname, newNickname);

        private void DoUserNicknameChange(string oldNickname, string newNickname)
        {
            IRCUser user = UserList.Find(u => u.Name.ToUpper() == oldNickname.ToUpper());
            if (user == null)
            {
                Logger.Log("DoUserNicknameChange: Failed to find user with nickname " + oldNickname);
                return;
            }
            string realOldNickname = user.Name; // 确保大小写匹配
            user.Name = newNickname;

            channels.ForEach(ch => ch.OnUserNameChanged(realOldNickname, newNickname));
        }

        public void OnServerLatencyTested(int candidateCount, int closerCount)
        {
            wm.AddCallback(new Action<int, int>(DoServerLatencyTested), candidateCount, closerCount);
        }

        private void DoServerLatencyTested(int candidateCount, int closerCount)
        {
            MainChannel.AddMessage(new ChatMessage(
                string.Format(
                    "Lobby servers: {0} available, {1} fast.",
                    candidateCount, closerCount)));
        }
    }

    public class UserEventArgs : EventArgs
    {
        public UserEventArgs(IRCUser ircUser)
        {
            User = ircUser;
        }

        public IRCUser User { get; private set; }
    }

    public class IndexEventArgs : EventArgs
    {
        public IndexEventArgs(int index)
        {
            Index = index;
        }

        public int Index { get; private set; }
    }

    public class UserNameChangedEventArgs : EventArgs
    {
        public UserNameChangedEventArgs(string oldUserName, IRCUser user)
        {
            OldUserName = oldUserName;
            User = user;
        }

        public string OldUserName { get; }
        public IRCUser User { get; }
    }
}
