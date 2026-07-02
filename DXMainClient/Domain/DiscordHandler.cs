using System;
using ClientCore;
using DiscordRPC;
using DiscordRPC.Message;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System.Text.RegularExpressions;

namespace DTAClient.Domain
{
    /// <summary>
    /// 处理Discord集成的类。
    /// </summary>
    public class DiscordHandler: IDisposable
    {
        private DiscordRpcClient client;

        private RichPresence _currentPresence;

        /// <summary>
        /// 当前正在显示的RichPresence实例。
        /// </summary>
        public RichPresence CurrentPresence
        {
            get
            {
                return _currentPresence;
            }
            set
            {
                if (_currentPresence == null || !_currentPresence.Equals(PreviousPresence))
                {
                    PreviousPresence = _currentPresence;
                    _currentPresence = value;
                    client?.SetPresence(_currentPresence);
                }
            }
        }

        /// <summary>
        /// 上一个显示的RichPresence实例。
        /// </summary>
        public RichPresence PreviousPresence { get; private set; }

        /// <summary>
        /// 创建Discord处理器的新实例。
        /// </summary>
        public DiscordHandler()
        {
            if (!UserINISettings.Instance.DiscordIntegration || string.IsNullOrEmpty(ClientConfiguration.Instance.DiscordAppId))
                return;

            InitializeClient();
            UpdatePresence();
            Connect();
        }

        #region overrides

        #endregion

        #region methods

        /// <summary>
        /// 初始化或重新初始化Discord RPC客户端对象及事件处理器。
        /// </summary>
        private void InitializeClient()
        {
            if (client != null && client.IsInitialized)
            {
                client.ClearPresence();
                client.Dispose();
                client = null;
            }

            client = new DiscordRpcClient(ClientConfiguration.Instance.DiscordAppId);
            client.OnReady += OnReady;
            client.OnClose += OnClose;
            client.OnError += OnError;
            client.OnConnectionEstablished += OnConnectionEstablished;
            client.OnConnectionFailed += OnConnectionFailed;
            client.OnPresenceUpdate += OnPresenceUpdate;
            client.OnSubscribe += OnSubscribe;
            client.OnUnsubscribe += OnUnsubscribe;

            if (CurrentPresence != null)
                client.SetPresence(CurrentPresence);
        }

        /// <summary>
        /// 连接到Discord。
        /// 如果Discord RPC客户端尚未初始化或已连接，则不做任何操作。
        /// </summary>
        public void Connect()
        {
            if (client == null || client.IsInitialized)
                return;

            bool success = client.Initialize();

            if (success)
                Logger.Log("DiscordHandler: Connected Discord RPC client.");
            else
                Logger.Log("DiscordHandler: Failed to connect Discord RPC client.");
        }

        /// <summary>
        /// 断开与Discord的连接。
        /// 如果Discord RPC客户端尚未初始化或未连接，则不做任何操作。
        /// </summary>
        public void Disconnect()
        {
            if (client == null || !client.IsInitialized)
                return;

            // HACK 警告
            // 目前DiscordRpcClient似乎没有任何可靠的方式使用同一个客户端对象断开并重新连接。
            // Deinitialize似乎没有完全重置连接状态和资源，之后任何调用Initialize的尝试都会失败。
            // 一个权宜之计是释放当前客户端对象并创建和初始化一个新的。
            InitializeClient(); //client.Deinitialize();

            Logger.Log("DiscordHandler: Disconnected Discord RPC client.");
        }

        /// <summary>
        /// 使用默认信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence()
        {
            CurrentPresence = new RichPresence()
            {
                Details = "在客户端中",
                Assets = new Assets()
                {
                    LargeImageKey = "logo"
                }
            };
        }

        /// <summary>
        /// 使用游戏大厅的信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence(string map, string mode, string type, string state,
            int players, int maxPlayers, string side, string roomName,
            bool isHost = false, bool isPassworded = false,
            bool isLocked = false, bool resetTimer = false)
        {
            string sideKey = new Regex("[^a-zA-Z0-9]").Replace(side.ToLower(), "");
            string stateString = $"{state} [{players}/{maxPlayers}] • {roomName}";
            if (isHost)
                stateString += "👑";
            if (isPassworded)
                stateString += "🔑";
            if (isLocked)
                stateString += "🔒";
            CurrentPresence = new RichPresence()
            {
                State = stateString,
                Details = $"{type} • {map} • {mode}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    SmallImageKey = sideKey,
                    SmallImageText = side
                },
                Timestamps = (client?.CurrentPresence.HasTimestamps() ?? false) && !resetTimer ?
                    client.CurrentPresence.Timestamps : Timestamps.Now
            };
        }

        /// <summary>
        /// 使用游戏加载大厅的信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence(string map, string mode, string type, string state,
            int players, int maxPlayers, string roomName,
            bool isHost = false, bool resetTimer = false)
        {
            string stateString = $"{state} [{players}/{maxPlayers}] • {roomName}";
            stateString += "💾";
            if (isHost)
                stateString += "👑";
            CurrentPresence = new RichPresence()
            {
                State = stateString,
                Details = $"{type} • {map} • {mode}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo"
                },
                Timestamps = (client?.CurrentPresence.HasTimestamps() ?? false) && !resetTimer ?
                    client.CurrentPresence.Timestamps : Timestamps.Now
            };
        }

        /// <summary>
        /// 使用遭遇战"大厅"的信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence(string map, string mode, string state, string side, bool resetTimer = false)
        {
            string sideKey = new Regex("[^a-zA-Z0-9]").Replace(side.ToLower(), "");
            CurrentPresence = new RichPresence()
            {
                State = $"{state}",
                Details = $"遭遇战 • {map} • {mode}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    SmallImageKey = sideKey,
                    SmallImageText = side
                },
                Timestamps = (client?.CurrentPresence.HasTimestamps() ?? false) && !resetTimer ?
                    client.CurrentPresence.Timestamps : Timestamps.Now
            };
        }

        /// <summary>
        /// 使用战役屏幕的信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence(string mission, string difficulty, string side, bool resetTimer = false)
        {
            string sideKey = new Regex("[^a-zA-Z0-9]").Replace(side.ToLower(), "");
            CurrentPresence = new RichPresence()
            {
                State = "正在执行任务",
                Details = $"{mission} • {difficulty}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    SmallImageKey = sideKey,
                    SmallImageText = side
                },
                Timestamps = (client?.CurrentPresence.HasTimestamps() ?? false) && !resetTimer ?
                    client.CurrentPresence.Timestamps : Timestamps.Now
            };
        }

        /// <summary>
        /// 使用游戏加载屏幕的信息更新Discord Rich Presence。
        /// </summary>
        public void UpdatePresence(string save, bool resetTimer = false)
        {
            CurrentPresence = new RichPresence()
            {
                State = "正在玩存档",
                Details = $"{save}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo"
                },
                Timestamps = (client?.CurrentPresence.HasTimestamps() ?? false) && !resetTimer ?
                    client.CurrentPresence.Timestamps : Timestamps.Now
            };
        }

        #endregion

        #region eventhandlers

        private void OnReady(object sender, ReadyMessage args)
        {
            Logger.Log($"Discord: Received Ready from user {args.User.Username}");
            client?.SetPresence(CurrentPresence);
        }

        private void OnClose(object sender, CloseMessage args)
        {
            Logger.Log($"Discord: Lost Connection with client because of '{args.Reason}'");
        }

        private void OnError(object sender, ErrorMessage args)
        {
            Logger.Log($"Discord: Error occured. ({args.Code}) {args.Message}");
        }

        private void OnConnectionEstablished(object sender, ConnectionEstablishedMessage args)
        {
            Logger.Log($"Discord: Pipe Connection Established. Valid on pipe #{args.ConnectedPipe}");
        }

        private void OnConnectionFailed(object sender, ConnectionFailedMessage args)
        {
            Logger.Log($"Discord: Pipe Connection Failed. Could not connect to pipe #{args.FailedPipe}");
        }

        private void OnPresenceUpdate(object sender, PresenceMessage args)
        {
            Logger.Log($"Discord: Rich Presence Updated. State: {args.Presence?.State}; Details: {args.Presence?.Details}");
        }

        private void OnSubscribe(object sender, SubscribeMessage args)
        {
            Logger.Log($"Discord: Subscribed: {args.Event}");
        }

        private void OnUnsubscribe(object sender, UnsubscribeMessage args)
        {
            Logger.Log($"Discord: Unsubscribed: {args.Event}");
        }

        #endregion

        public void Dispose()
        {
            if (client == null)
                return;

            if (client.IsInitialized)
                client.ClearPresence();

            client.Dispose();
        }
    }
}
