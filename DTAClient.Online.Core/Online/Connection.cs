using ClientCore;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DTAClient.Online
{
    /// <summary>
    /// CnCNet 连接处理程序。
    /// </summary>
    public class Connection
    {
        private const int MAX_RECONNECT_COUNT = 8;
        private const int RECONNECT_WAIT_DELAY = 4000;
        private const int ID_LENGTH = 9;
        private const int MAXIMUM_LATENCY = 400;

        public Connection(IConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        IConnectionManager connectionManager;

        /// <summary>
        /// 要连接的 CnCNet / GameSurge IRC 服务器列表。
        /// </summary>
        private static readonly IList<Server> Servers = new List<Server>
        {
            //new Server("vampirefu.eu.org","ergo.test",new int[]{ 6668,6697 }),

            new Server("Burstfire.UK.EU.GameSurge.net", "GameSurge London, UK", new int[3] { 6667, 6668, 7000 }),
            new Server("ColoCrossing.IL.US.GameSurge.net", "GameSurge Chicago, IL", new int[5] { 6660, 6666, 6667, 6668, 6669 }),
            new Server("Gameservers.NJ.US.GameSurge.net", "GameSurge Newark, NJ", new int[7] { 6665, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("Krypt.CA.US.GameSurge.net", "GameSurge Santa Ana, CA", new int[4] { 6666, 6667, 6668, 6669 }),
            new Server("NuclearFallout.WA.US.GameSurge.net", "GameSurge Seattle, WA", new int[2] { 6667, 5960 }),
            new Server("Portlane.SE.EU.GameSurge.net", "GameSurge Stockholm, Sweden", new int[5] { 6660, 6666, 6667, 6668, 6669 }),
            new Server("Prothid.NY.US.GameSurge.Net", "GameSurge NYC, NY", new int[7] { 5960, 6660, 6666, 6667, 6668, 6669, 6697 }),
            new Server("TAL.DE.EU.GameSurge.net", "GameSurge Wuppertal, Germany", new int[5] { 6660, 6666, 6667, 6668, 6669 }),
            new Server("208.167.237.120", "GameSurge IP 208.167.237.120", new int[7] {  6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("192.223.27.109", "GameSurge IP 192.223.27.109", new int[7] {  6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("108.174.48.100", "GameSurge IP 108.174.48.100", new int[7] { 6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("208.146.35.105", "GameSurge IP 208.146.35.105", new int[7] { 6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("195.8.250.180", "GameSurge IP 195.8.250.180", new int[7] { 6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("91.217.189.76", "GameSurge IP 91.217.189.76", new int[7] { 6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("195.68.206.250", "GameSurge IP 195.68.206.250", new int[7] { 6660, 6666, 6667, 6668, 6669, 7000, 8080 }),
            new Server("irc.gamesurge.net", "GameSurge", new int[1] { 6667 }),
        }.AsReadOnly();

        bool _isConnected = false;
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        bool _attemptingConnection = false;
        public bool AttemptingConnection
        {
            get { return _attemptingConnection; }
        }

        Random _rng = new Random();
        public Random Rng
        {
            get { return _rng; }
        }

        private List<QueuedMessage> MessageQueue = new List<QueuedMessage>();
        private TimeSpan MessageQueueDelay;

        private NetworkStream serverStream;
        private TcpClient tcpClient;

        volatile int reconnectCount = 0;

        private volatile bool connectionCut = false;
        private volatile bool welcomeMessageReceived = false;
        private volatile bool sendQueueExited = false;
        bool _disconnect = false;
        private bool disconnect
        {
            get
            {
                lock (locker)
                    return _disconnect;
            }
            set
            {
                lock (locker)
                    _disconnect = value;
            }
        }

        private string overMessage;

        private readonly Encoding encoding = Encoding.UTF8;

        /// <summary>
        /// 已断开我们连接的服务器 IP 列表。
        /// 客户端在尝试重新连接时会跳过这些服务器，以防止服务器先接受连接
        /// 然后立即断开从而导致无法在线游戏。
        /// </summary>
        private readonly List<string> failedServerIPs = new List<string>();
        private volatile string currentConnectedServerIP;

        private static readonly object locker = new object();
        private static readonly object messageQueueLocker = new object();

        private static bool idSet = false;
        private static string systemId;
        private static readonly object idLocker = new object();

        public static void SetId(string id)
        {
            lock (idLocker)
            {
                int maxLength = ID_LENGTH - (ClientConfiguration.Instance.LocalGame.Length + 1);
                systemId = Utilities.CalculateSHA1ForString(id).Substring(0, maxLength);
                idSet = true;
            }
        }

        public static bool IsIdSet()
        {
            lock (idLocker)
            {
                return idSet;
            }
        }

        /// <summary>
        /// 尝试连接到 CnCNet 而不阻塞调用线程。
        /// </summary>
        public void ConnectAsync()
        {
            if (_isConnected)
                throw new InvalidOperationException("客户端已连接!");

            if (_attemptingConnection)
                return; // 也许我们也应该在这种情况下抛出异常？

            welcomeMessageReceived = false;
            connectionCut = false;
            _attemptingConnection = true;
            disconnect = false;

            MessageQueueDelay = TimeSpan.FromMilliseconds(ClientConfiguration.Instance.SendSleep);

            Thread connection = new Thread(ConnectToServer);
            connection.Start();
        }

        /// <summary>
        /// 尝试连接到 CnCNet。
        /// </summary>
        private void ConnectToServer()
        {
            IList<Server> sortedServerList = GetServerListSortedByLatency();

            foreach (Server server in sortedServerList)
            {
                try
                {
                    for (int i = 0; i < server.Ports.Length; i++)
                    {
                        connectionManager.OnAttemptedServerChanged(server.Name);

                        TcpClient client = new TcpClient(AddressFamily.InterNetwork);
                        var result = client.BeginConnect(server.Host, server.Ports[i], null, null);
                        result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3), false);

                        Logger.Log("Attempting connection to " + server.Host + ":" + server.Ports[i]);

                        if (!client.Connected)
                        {
                            Logger.Log("Connecting to " + server.Host + " port " + server.Ports[i] + " timed out!");
                            continue; // 使用下一个端口重新开始
                        }

                        Logger.Log("Succesfully connected to " + server.Host + " on port " + server.Ports[i]);
                        client.EndConnect(result);

                        _isConnected = true;
                        _attemptingConnection = false;

                        connectionManager.OnConnected();

                        Thread sendQueueHandler = new Thread(RunSendQueue);
                        sendQueueHandler.Start();

                        tcpClient = client;
                        serverStream = tcpClient.GetStream();
                        serverStream.ReadTimeout = 1000;

                        currentConnectedServerIP = server.Host;
                        HandleComm();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Unable to connect to the server. " + ex.Message);
                }
            }

            Logger.Log("Connecting to CnCNet failed!");
            // 如果连接所有服务器都失败了，清除失败服务器列表
            failedServerIPs.Clear();
            _attemptingConnection = false;
            connectionManager.OnConnectAttemptFailed();
        }

        private void HandleComm()
        {
            int errorTimes = 0;
            byte[] message = new byte[1024];

            Register();

            Timer timer = new Timer(AutoPing, null, 30000, 120000);

            connectionCut = true;

            while (true)
            {
                if (connectionManager.GetDisconnectStatus())
                {
                    connectionManager.OnDisconnected();
                    connectionCut = false; // 此断开连接是有意的
                    break;
                }

                if (!serverStream.DataAvailable)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int bytesRead;

                try
                {
                    bytesRead = serverStream.Read(message, 0, 1024);
                }
                catch (Exception ex)
                {
                    Logger.Log("Disconnected from CnCNet due to a socket error. Message: " + ex.Message);
                    errorTimes++;

                    if (errorTimes > MAX_RECONNECT_COUNT)
                    {
                        const string errorMessage = "Disconnected from CnCNet after reaching the maximum number of connection retries.";
                        Logger.Log(errorMessage);
                        failedServerIPs.Add(currentConnectedServerIP);
                        connectionManager.OnConnectionLost(errorMessage);
                        break;
                    }

                    continue;
                }

                errorTimes = 0;

                // 已成功接收到消息
                string msg = encoding.GetString(message, 0, bytesRead);
                Logger.Log("Message received: " + msg);

                HandleMessage(msg);
                timer.Change(30000, 30000);
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            timer.Dispose();

            _isConnected = false;
            disconnect = false;

            if (connectionCut)
            {
                while (!sendQueueExited)
                    Thread.Sleep(100);

                reconnectCount++;

                if (reconnectCount > MAX_RECONNECT_COUNT)
                {
                    Logger.Log("Reconnect attempt count exceeded!");
                    return;
                }

                Thread.Sleep(RECONNECT_WAIT_DELAY);

                if (IsConnected || AttemptingConnection)
                {
                    Logger.Log("Cancelling reconnection attempt because the user has attempted to reconnect manually.");
                    return;
                }

                Logger.Log("Attempting to reconnect to CnCNet.");
                connectionManager.OnReconnectAttempt();
            }
        }

        /// <summary>
        /// 通过解析主机名获取大厅服务器的所有 IP 地址，并测试到服务器的延迟。
        /// 最大延迟在 <c>MAXIMUM_LATENCY</c> 中定义，参见 <see cref="Connection.MAXIMUM_LATENCY"/>。
        /// 未及时响应 ICMP 消息的服务器将被放置在列表末尾。
        /// </summary>
        /// <returns>按延迟排序的大厅服务器列表。</returns>
        private IList<Server> GetServerListSortedByLatency()
        {
            // 解析主机名。
            ICollection<Task<IEnumerable<Tuple<IPAddress, string, int[]>>>>
                dnsTasks = new List<Task<IEnumerable<Tuple<IPAddress, string, int[]>>>>(Servers.Count);

            foreach (Server server in Servers)
            {
                string serverHostnameOrIPAddress = server.Host;
                string serverName = server.Name;
                int[] serverPorts = server.Ports;

                Task<IEnumerable<Tuple<IPAddress, string, int[]>>> dnsTask = new Task<IEnumerable<Tuple<IPAddress, string, int[]>>>(() =>
                {
                    Logger.Log($"Attempting to DNS resolve {serverName} ({serverHostnameOrIPAddress}).");
                    ICollection<Tuple<IPAddress, string, int[]>> _serverInfos = new List<Tuple<IPAddress, string, int[]>>();

                    try
                    {
                        // 如果 hostNameOrAddress 是 IP 地址，则直接返回该地址而不查询 DNS 服务器。
                        IEnumerable<IPAddress> serverIPAddresses = Dns.GetHostAddresses(serverHostnameOrIPAddress)
                                                                      .Where(IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetwork);

                        Logger.Log($"DNS resolved {serverName} ({serverHostnameOrIPAddress}): " +
                            $"{string.Join(", ", serverIPAddresses.Select(item => item.ToString()))}");

                        // 将每个 IPAddress 存储在不同的元组中。
                        foreach (IPAddress serverIPAddress in serverIPAddresses)
                        {
                            _serverInfos.Add(new Tuple<IPAddress, string, int[]>(serverIPAddress, serverName, serverPorts));
                        }
                    }
                    catch (SocketException ex)
                    {
                        Logger.Log($"Caught an exception when DNS resolving {serverName} ({serverHostnameOrIPAddress}) Lobby server: {ex.Message}");
                    }

                    return _serverInfos;
                });

                dnsTask.Start();
                dnsTasks.Add(dnsTask);
            }

            Task.WaitAll(dnsTasks.ToArray());

            // 按 IPAddress 对元组进行分组以合并重复的服务器。
            IEnumerable<IGrouping<IPAddress, Tuple<string, int[]>>>
                serverInfosGroupedByIPAddress = dnsTasks.SelectMany(dnsTask => dnsTask.Result)      // Tuple<IPAddress, 服务器名称, 服务器端口>
                                                        .GroupBy(
                                                            serverInfo => serverInfo.Item1,         // IPAddress
                                                            serverInfo => new Tuple<string, int[]>(
                                                                serverInfo.Item2,                   // 服务器名称
                                                                serverInfo.Item3                    // 服务器端口
                                                            )
                                                        );

            // 处理每个分组：
            //   1. 获取 IPAddress。
            //   2. 拼接服务器名称。
            //   3. 移除重复的端口。
            //   4. 构造并返回包含 IPAddress、拼接的服务器名称和唯一端口的元组。
            IEnumerable<Tuple<IPAddress, string, int[]>> serverInfos = serverInfosGroupedByIPAddress.Select(serverInfoGroup =>
            {
                IPAddress ipAddress = serverInfoGroup.Key;
                string serverNames = string.Join(", ", serverInfoGroup.Select(serverInfo => serverInfo.Item1));
                int[] serverPorts = serverInfoGroup.SelectMany(serverInfo => serverInfo.Item2).Distinct().ToArray();

                return new Tuple<IPAddress, string, int[]>(ipAddress, serverNames, serverPorts);
            });

            // 记录日志。
            foreach (Tuple<IPAddress, string, int[]> serverInfo in serverInfos)
            {
                string serverIPAddress = serverInfo.Item1.ToString();
                string serverNames = string.Join(", ", serverInfo.Item2.ToString());
                string serverPorts = string.Join(", ", serverInfo.Item3.Select(port => port.ToString()));

                Logger.Log($"Got a Lobby server. IP: {serverIPAddress}; Name: {serverNames}; Ports: {serverPorts}.");
            }

            Logger.Log($"The number of Lobby servers is {serverInfos.Count() }.");

            // 测试延迟。
            ICollection<Task<Tuple<Server, long>>> pingTasks = new List<Task<Tuple<Server, long>>>(serverInfos.Count());

            foreach (Tuple<IPAddress, string, int[]> serverInfo in serverInfos)
            {
                IPAddress serverIPAddress = serverInfo.Item1;
                string serverNames = serverInfo.Item2;
                int[] serverPorts = serverInfo.Item3;

                if (failedServerIPs.Contains(serverIPAddress.ToString()))
                {
                    Logger.Log($"Skipped a failed server {serverNames} ({serverIPAddress}).");
                    continue;
                }

                Task<Tuple<Server, long>> pingTask = new Task<Tuple<Server, long>>(() =>
                {
                    Logger.Log($"Attempting to ping {serverNames} ({serverIPAddress}).");
                    Server server = new Server(serverIPAddress.ToString(), serverNames, serverPorts);

                    using (Ping ping = new Ping())
                    {
                        try
                        {
                            PingReply pingReply = ping.Send(serverIPAddress, MAXIMUM_LATENCY);

                            if (pingReply.Status == IPStatus.Success)
                            {
                                long pingInMs = pingReply.RoundtripTime;
                                Logger.Log($"The latency in milliseconds to the server {serverNames} ({serverIPAddress}): {pingInMs}.");

                                return new Tuple<Server, long>(server, pingInMs);
                            }
                            else
                            {
                                Logger.Log($"Failed to ping the server {serverNames} ({serverIPAddress}): " +
                                    $"{Enum.GetName(typeof(IPStatus), pingReply.Status)}.");

                                return new Tuple<Server, long>(server, long.MaxValue);
                            }
                        }
                        catch (PingException ex)
                        {
                            Logger.Log($"Caught an exception when pinging {serverNames} ({serverIPAddress}) Lobby server: {ex.Message}");

                            return new Tuple<Server, long>(server, long.MaxValue);
                        }
                    }
                });

                pingTask.Start();
                pingTasks.Add(pingTask);
            }

            Task.WaitAll(pingTasks.ToArray());

            // 按延迟对服务器进行排序。
            IOrderedEnumerable<Tuple<Server, long>>
                sortedServerAndLatencyResults = pingTasks.Select(task => task.Result)              // Tuple<Server, 延迟>
                                                         .OrderBy(taskResult => taskResult.Item2); // 延迟

            // 记录日志。
            foreach (Tuple<Server, long> serverAndLatencyResult in sortedServerAndLatencyResults)
            {
                string serverIPAddress = serverAndLatencyResult.Item1.Host;
                long serverLatencyValue = serverAndLatencyResult.Item2;
                string serverLatencyString = serverLatencyValue <= MAXIMUM_LATENCY ? serverLatencyValue.ToString() : "DNF";

                Logger.Log($"Lobby server IP: {serverIPAddress}, latency: {serverLatencyString}.");
            }

            {
                int candidateCount = sortedServerAndLatencyResults.Count();
                int closerCount = sortedServerAndLatencyResults.Count(
                    serverAndLatencyResult => serverAndLatencyResult.Item2 <= MAXIMUM_LATENCY);

                Logger.Log($"Lobby servers: {candidateCount} available, {closerCount} fast.");
                connectionManager.OnServerLatencyTested(candidateCount, closerCount);
            }

            return sortedServerAndLatencyResults.Select(taskResult => taskResult.Item1).ToList(); // 服务器
        }

        public void Disconnect()
        {
            disconnect = true;
            SendMessage("QUIT");

            tcpClient.Close();
            serverStream.Close();
        }

        #region Handling commands

        /// <summary>
        /// 检查来自 IRC 服务器的消息是部分消息还是完整消息，
        /// 并相应地处理。
        /// </summary>
        /// <param name="message">消息内容。</param>
        private void HandleMessage(string message)
        {
            string msg = overMessage + message;
            overMessage = "";
            while (true)
            {
                int commandEndIndex = msg.IndexOf("\n");

                if (commandEndIndex == -1)
                {
                    overMessage = msg;
                    break;
                }
                else if (msg.Length != commandEndIndex + 1)
                {
                    string command = msg.Substring(0, commandEndIndex - 1);
                    PerformCommand(command);

                    msg = msg.Remove(0, commandEndIndex + 1);
                }
                else
                {
                    string command = msg.Substring(0, msg.Length - 1);
                    PerformCommand(command);
                    break;
                }
            }
        }

        /// <summary>
        /// 处理从 IRC 服务器接收到的特定命令。
        /// </summary>
        private void PerformCommand(string message)
        {
            string prefix = String.Empty;
            string command = String.Empty;
            message = message.Replace("\r", String.Empty);
            List<string> parameters = new List<string>();
            ParseIrcMessage(message, out prefix, out command, out parameters);
            string paramString = String.Empty;
            foreach (string param in parameters) { paramString = paramString + param + ","; }
            Logger.Log("RMP: " + prefix + " " + command + " " + paramString);

            try
            {
                bool success = false;
                int commandNumber = -1;
                success = Int32.TryParse(command, out commandNumber);

                if (success)
                {
                    string serverMessagePart = prefix + ": ";

                    switch (commandNumber)
                    {
                        // 命令描述来自 https://www.alien.net.au/irc/irc2numerics.html

                        case 001: // 欢迎消息
                            message = serverMessagePart + parameters[1];
                            welcomeMessageReceived = true;
                            connectionManager.OnWelcomeMessageReceived(message);
                            reconnectCount = 0;
                            break;
                        case 002: // "您的主机是 x，运行版本 y"
                        case 003: // "此服务器创建于..."
                        case 251: // 有 <int> 个用户和 <int> 个隐身用户在 <int> 个服务器上
                        case 255: // 我有 <int> 个客户端和 <int> 个服务器
                        case 265: // 本地用户数
                        case 266: // 全局用户数
                        case 401: // 用于指示提供给命令的昵称参数当前未被使用
                        case 403: // 用于指示给定的频道名称无效或不存在
                        case 404: // 用于指示用户没有向频道发送消息的权限
                        case 432: // 注册时昵称无效
                        case 461: // 服务器返回此响应，当命令需要更多参数但提供的参数不足时
                        case 465: // 当客户端尝试在配置为禁止该客户端连接的服务器上注册时返回
                            StringBuilder displayedMessage = new StringBuilder(serverMessagePart);
                            for (int i = 1; i < parameters.Count; i++)
                            {
                                displayedMessage.Append(' ');
                                displayedMessage.Append(parameters[i]);
                            }
                            connectionManager.OnGenericServerMessageReceived(displayedMessage.ToString());
                            break;
                        case 439: // 尝试过快发送消息
                            connectionManager.OnTargetChangeTooFast(parameters[1], parameters[2]);
                            break;
                        case 252: // 在线管理员数量
                        case 254: // 已形成的频道数量
                            message = serverMessagePart + parameters[1] + " " + parameters[2];
                            connectionManager.OnGenericServerMessageReceived(message);
                            break;
                        case 301: // 离开消息
                            string awayTarget = parameters[0];
                            if (awayTarget != ProgramConstants.PLAYERNAME)
                                break;
                            string awayPlayer = parameters[1];
                            string awayReason = parameters[2];
                            connectionManager.OnAwayMessageReceived(awayPlayer, awayReason);
                            break;
                        case 332: // 频道主题消息
                            string _target = parameters[0];
                            if (_target != ProgramConstants.PLAYERNAME)
                                break;
                            connectionManager.OnChannelTopicReceived(parameters[1], parameters[2]);
                            break;
                        case 353: // 用户列表（NAMES 的回复）
                            string target = parameters[0];
                            if (target != ProgramConstants.PLAYERNAME)
                                break;
                            string channelName = parameters[2];
                            string[] users = parameters[3].Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            connectionManager.OnUserListReceived(channelName, users);
                            break;
                        case 352: // WHO 查询的回复
                            string ident = parameters[2];
                            string host = parameters[3];
                            string wUserName = parameters[5];
                            string extraInfo = parameters[7];
                            connectionManager.OnWhoReplyReceived(ident, host, wUserName, extraInfo);
                            break;
                        case 311: // WHOIS NAME 查询的回复
                            connectionManager.OnWhoReplyReceived(parameters[2], parameters[3], parameters[1], string.Empty);
                            break;
                        case 433: // 名称已被使用
                            message = serverMessagePart + parameters[1] + ": " + parameters[2];
                            //connectionManager.OnGenericServerMessageReceived(message);
                            connectionManager.OnNameAlreadyInUse();
                            break;
                        case 451: // 尚未注册
                            Register();
                            connectionManager.OnGenericServerMessageReceived(message);
                            break;
                        case 471: // 尝试加入已满的频道时返回（基本上是玩家数量已达上限）
                            connectionManager.OnChannelFull(parameters[1]);
                            break;
                        case 473: // 尝试加入仅限邀请的频道时返回（锁定的游戏）
                            connectionManager.OnChannelInviteOnly(parameters[1]);
                            break;
                        case 474: // 尝试加入用户被封禁的频道时返回
                            connectionManager.OnBannedFromChannel(parameters[1]);
                            break;
                        case 475: // 尝试在没有密钥或使用错误密钥的情况下加入密钥锁定的频道时返回
                            connectionManager.OnIncorrectChannelPassword(parameters[1]);
                            break;
                    }

                    return;
                }

                switch (command)
                {
                    case "NOTICE":
                        int noticeExclamIndex = prefix.IndexOf('!');
                        if (noticeExclamIndex > -1)
                        {
                            if (parameters.Count > 1 && parameters[1][0] == 1)//Conversions.IntFromString(parameters[1].Substring(0, 1), -1) == 1)
                            {
                                // CTCP 协议
                                string channelName = parameters[0];
                                string ctcpMessage = parameters[1];
                                ctcpMessage = ctcpMessage.Remove(0, 1).Remove(ctcpMessage.Length - 2);
                                string ctcpSender = prefix.Substring(0, noticeExclamIndex);
                                connectionManager.OnCTCPParsed(channelName, ctcpSender, ctcpMessage);

                                return;
                            }
                            else
                            {
                                string noticeUserName = prefix.Substring(0, noticeExclamIndex);
                                string notice = parameters[parameters.Count - 1];
                                connectionManager.OnNoticeMessageParsed(notice, noticeUserName);
                                break;
                            }
                        }
                        string noticeParamString = String.Empty;
                        foreach (string param in parameters)
                            noticeParamString = noticeParamString + param + " ";
                        connectionManager.OnGenericServerMessageReceived(prefix + " " + noticeParamString);
                        break;
                    case "JOIN":
                        string channel = parameters[0];
                        int atIndex = prefix.IndexOf('@');
                        int exclamIndex = prefix.IndexOf('!');
                        string userName = prefix.Substring(0, exclamIndex);
                        string ident = prefix.Substring(exclamIndex + 1, atIndex - (exclamIndex + 1));
                        string host = prefix.Substring(atIndex + 1);
                        connectionManager.OnUserJoinedChannel(channel, host, userName, ident);
                        break;
                    case "PART":
                        string pChannel = parameters[0];
                        string pUserName = prefix.Substring(0, prefix.IndexOf('!'));
                        connectionManager.OnUserLeftChannel(pChannel, pUserName);
                        break;
                    case "QUIT":
                        string qUserName = prefix.Substring(0, prefix.IndexOf('!'));
                        connectionManager.OnUserQuitIRC(qUserName);
                        break;
                    case "PRIVMSG":
                        if (parameters.Count > 1 && Convert.ToInt32(parameters[1][0]) == 1 && !parameters[1].Contains("ACTION"))
                        {
                            goto case "NOTICE";
                        }
                        string pmsgUserName = prefix.Substring(0, prefix.IndexOf('!'));
                        string pmsgIdent = GetIdentFromPrefix(prefix);
                        string[] recipients = new string[parameters.Count - 1];
                        for (int pid = 0; pid < parameters.Count - 1; pid++)
                            recipients[pid] = parameters[pid];
                        string privmsg = parameters[parameters.Count - 1];
                        if (parameters[1].StartsWith('\u0001'.ToString() + "ACTION"))
                            privmsg = privmsg.Substring(1).Remove(privmsg.Length - 2);
                        foreach (string recipient in recipients)
                        {
                            if (recipient.StartsWith("#"))
                                connectionManager.OnChatMessageReceived(recipient, pmsgUserName, pmsgIdent, privmsg);
                            else if (recipient == ProgramConstants.PLAYERNAME)
                                connectionManager.OnPrivateMessageReceived(pmsgUserName, privmsg);
                            //else if (pmsgUserName == ProgramConstants.PLAYERNAME)
                            //{
                            //    DoPrivateMessageSent(privmsg, recipient);
                            //}
                        }
                        break;
                    case "MODE":
                        string modeUserName = prefix.Substring(0, prefix.IndexOf('!'));
                        string modeChannelName = parameters[0];
                        string modeString = parameters[1];
                        List<string> modeParameters =
                            parameters.Count > 2 ? parameters.GetRange(2, parameters.Count - 2) : new List<string>();
                        connectionManager.OnChannelModesChanged(modeUserName, modeChannelName, modeString, modeParameters);
                        break;
                    case "KICK":
                        string kickChannelName = parameters[0];
                        string kickUserName = parameters[1];
                        connectionManager.OnUserKicked(kickChannelName, kickUserName);
                        break;
                    case "ERROR":
                        connectionManager.OnErrorReceived(message);
                        break;
                    case "PING":
                        if (parameters.Count > 0)
                        {
                            QueueMessage(new QueuedMessage("PONG " + parameters[0], QueuedMessageType.SYSTEM_MESSAGE, 5000));
                            Logger.Log("PONG " + parameters[0]);
                        }
                        else
                        {
                            QueueMessage(new QueuedMessage("PONG", QueuedMessageType.SYSTEM_MESSAGE, 5000));
                            Logger.Log("PONG");
                        }
                        break;
                    case "TOPIC":
                        if (parameters.Count < 2)
                            break;

                        connectionManager.OnChannelTopicChanged(prefix.Substring(0, prefix.IndexOf('!')),
                            parameters[0], parameters[1]);
                        break;
                    case "NICK":
                        int nickExclamIndex = prefix.IndexOf('!');
                        if (nickExclamIndex > -1 || parameters.Count < 1)
                        {
                            string oldNick = prefix.Substring(0, nickExclamIndex);
                            string newNick = parameters[0];
                            Logger.Log("Nick change - " + oldNick + " -> " + newNick);
                            connectionManager.OnUserNicknameChange(oldNick, newNick);
                        }
                        break;
                }
            }
            catch
            {
                Logger.Log("Warning: Failed to parse command " + message);
            }
        }

        private string GetIdentFromPrefix(string prefix)
        {
            int atIndex = prefix.IndexOf('@');
            int exclamIndex = prefix.IndexOf('!');

            if (exclamIndex == -1 || atIndex == -1)
                return string.Empty;

            return prefix.Substring(exclamIndex + 1, atIndex - (exclamIndex + 1));
        }

        /// <summary>
        /// 解析从服务器接收到的单条 IRC 消息。
        /// </summary>
        /// <param name="message">消息内容。</param>
        /// <param name="prefix">(out) 消息前缀。</param>
        /// <param name="command">(out) 命令。</param>
        /// <param name="parameters">(out) 命令的参数。</param>
        private void ParseIrcMessage(string message, out string prefix, out string command, out List<string> parameters)
        {
            int prefixEnd = -1;
            prefix = command = String.Empty;
            parameters = new List<string>();

            // 如果存在前缀则获取。如果消息以冒号开头，
            // 冒号之后到第一个空格之间的字符就是前缀。
            if (message.StartsWith(":"))
            {
                prefixEnd = message.IndexOf(" ");
                prefix = message.Substring(1, prefixEnd - 1);
            }

            // 如果存在尾部内容则获取。如果消息中包含紧跟在冒号后面的空格，
            // 则冒号之后的所有字符就是尾部内容。
            int trailingStart = message.IndexOf(" :");
            string trailing = null;
            if (trailingStart >= 0)
                trailing = message.Substring(trailingStart + 2);
            else
                trailingStart = message.Length;

            // 使用前缀结束位置和尾部内容起始位置来提取命令和参数。
            var commandAndParameters = message.Substring(prefixEnd + 1, trailingStart - prefixEnd - 1).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (commandAndParameters.Length == 0)
            {
                command = String.Empty;
                Logger.Log("Nonexistant command!");
                return;
            }

            // 命令始终是数组的第一个元素。
            command = commandAndParameters[0];

            // 其余元素是参数（如果存在）。
            // 跳过第一个元素，因为那是命令。
            if (commandAndParameters.Length > 1)
            {
                for (int id = 1; id < commandAndParameters.Length; id++)
                {
                    parameters.Add(commandAndParameters[id]);
                }
            }

            // 如果尾部内容有效，将尾部内容添加到参数的末尾。
            if (!string.IsNullOrEmpty(trailing))
                parameters.Add(trailing);
        }

        #endregion

        #region Sending commands

        private void RunSendQueue()
        {
            while (_isConnected)
            {
                string message = String.Empty;

                lock (messageQueueLocker)
                {
                    for (int i = 0; i < MessageQueue.Count; i++)
                    {
                        QueuedMessage qm = MessageQueue[i];
                        if (qm.Delay > 0)
                        {
                            if (qm.SendAt < DateTime.Now)
                            {
                                message = qm.Command;

                                Logger.Log("Delayed message sent: " + qm.ID);

                                MessageQueue.RemoveAt(i);
                                break;
                            }
                        }
                        else
                        {
                            message = qm.Command;
                            MessageQueue.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (String.IsNullOrEmpty(message))
                {
                    Thread.Sleep(10);
                    continue;
                }

                SendMessage(message);

                Thread.Sleep(MessageQueueDelay);
            }

            lock (messageQueueLocker)
            {
                MessageQueue.Clear();
            }

            sendQueueExited = true;
        }

        /// <summary>
        /// 向服务器发送 PING 消息以指示我们仍然在线。
        /// </summary>
        /// <param name="data">仅是一个虚拟参数，用于匹配 System.Threading.TimerCallback 委托。</param>
        private void AutoPing(object data)
        {
            SendMessage("PING LAG" + new Random().Next(100000, 999999));
        }

        /// <summary>
        /// 注册用户。
        /// </summary>
        private void Register()
        {
            if (welcomeMessageReceived)
                return;

            Logger.Log("Registering.");

            var defaultGame = ClientConfiguration.Instance.LocalGame;

            string realname = ProgramConstants.GAME_VERSION + " " + defaultGame + " CnCNet";

            SendMessage(string.Format("USER {0} 0 * :{1}", defaultGame + "." +
                systemId, realname));

            SendMessage("NICK " + ProgramConstants.PLAYERNAME);
        }

        public void ChangeNickname()
        {
            SendMessage("NICK " + ProgramConstants.PLAYERNAME);
        }

        public void QueueMessage(QueuedMessageType type, int priority, string message, bool replace = false)
        {
            QueuedMessage qm = new QueuedMessage(message, type, priority, replace);
            QueueMessage(qm);
        }

        public void QueueMessage(QueuedMessageType type, int priority, int delay, string message)
        {
            QueuedMessage qm = new QueuedMessage(message, type, priority, delay);
            QueueMessage(qm);
            Logger.Log("Setting delay to " + delay + "ms for " + qm.ID);
        }

        /// <summary>
        /// 向 CnCNet 服务器发送消息。
        /// </summary>
        /// <param name="message">要发送的消息。</param>
        private void SendMessage(string message)
        {
            if (serverStream == null)
                return;

            Logger.Log("SRM: " + message);

            byte[] buffer = encoding.GetBytes(message + "\r\n");
            if (serverStream.CanWrite)
            {
                try
                {
                    serverStream.Write(buffer, 0, buffer.Length);
                    serverStream.Flush();
                }
                catch (IOException ex)
                {
                    Logger.Log("Sending message to the server failed! Reason: " + ex.Message);
                }
            }
        }

        private int NextQueueID { get; set; } = 0;

        /// <summary>
        /// 尝试替换先前排队的同类型消息。
        /// </summary>
        /// <param name="qm">要替换的新消息</param>
        /// <returns>是否发生了替换</returns>
        private bool ReplaceMessage(QueuedMessage qm)
        {
            lock (messageQueueLocker)
            {
                var previousMessageIndex = MessageQueue.FindIndex(m => m.MessageType == qm.MessageType);
                if (previousMessageIndex == -1)
                    return false;

                MessageQueue[previousMessageIndex] = qm;
                return true;
            }
        }

        /// <summary>
        /// 将消息添加到发送队列。
        /// </summary>
        /// <param name="qm">要排队的消息。</param>
        /// <param name="replace">如果为 true，尝试替换先前同类型的消息</param>
        public void QueueMessage(QueuedMessage qm)
        {
            if (!_isConnected)
                return;

            if (qm.Replace && ReplaceMessage(qm))
                return;

            qm.ID = NextQueueID++;

            lock (messageQueueLocker)
            {
                switch (qm.MessageType)
                {
                    case QueuedMessageType.GAME_BROADCASTING_MESSAGE:
                    case QueuedMessageType.GAME_PLAYERS_MESSAGE:
                    case QueuedMessageType.GAME_SETTINGS_MESSAGE:
                    case QueuedMessageType.GAME_PLAYERS_READY_STATUS_MESSAGE:
                    case QueuedMessageType.GAME_LOCKED_MESSAGE:
                    case QueuedMessageType.GAME_GET_READY_MESSAGE:
                    case QueuedMessageType.GAME_NOTIFICATION_MESSAGE:
                    case QueuedMessageType.GAME_HOSTING_MESSAGE:
                    case QueuedMessageType.WHOIS_MESSAGE:
                    case QueuedMessageType.GAME_CHEATER_MESSAGE:
                        AddSpecialQueuedMessage(qm);
                        break;
                    case QueuedMessageType.INSTANT_MESSAGE:
                        SendMessage(qm.Command);
                        break;
                    default:
                        int placeInQueue = MessageQueue.FindIndex(m => m.Priority < qm.Priority);
                        if (ProgramConstants.LOG_LEVEL > 1)
                            Logger.Log("QM Undefined: " + qm.Command + " " + placeInQueue);
                        if (placeInQueue == -1)
                            MessageQueue.Add(qm);
                        else
                            MessageQueue.Insert(placeInQueue, qm);
                        break;
                }
            }
        }

        /// <summary>
        /// 将"特殊"消息添加到发送队列，替换队列中先前同类型的消息。
        /// </summary>
        /// <param name="qm">要排队的消息。</param>
        private void AddSpecialQueuedMessage(QueuedMessage qm)
        {
            int broadcastingMessageIndex = MessageQueue.FindIndex(m => m.MessageType == qm.MessageType);

            qm.ID = NextQueueID++;

            if (broadcastingMessageIndex > -1)
            {
                if (ProgramConstants.LOG_LEVEL > 1)
                    Logger.Log("QM Replace: " + qm.Command + " " + broadcastingMessageIndex);
                MessageQueue[broadcastingMessageIndex] = qm;
            }
            else
            {
                int placeInQueue = MessageQueue.FindIndex(m => m.Priority < qm.Priority);
                if (ProgramConstants.LOG_LEVEL > 1)
                    Logger.Log("QM: " + qm.Command + " " + placeInQueue);
                if (placeInQueue == -1)
                    MessageQueue.Add(qm);
                else
                    MessageQueue.Insert(placeInQueue, qm);
            }
        }

        #endregion
    }
}