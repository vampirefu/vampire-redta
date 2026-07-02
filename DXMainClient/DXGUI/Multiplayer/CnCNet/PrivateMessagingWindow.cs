using ClientCore;
using ClientCore.CnCNet5;
using ClientGUI;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClientCore.Enums;
using SixLabors.ImageSharp;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class PrivateMessagingWindow : XNAWindow, ISwitchable
    {
        private const int MESSAGES_INDEX = 0;
        private const int FRIEND_LIST_VIEW_INDEX = 1;
        private const int ALL_PLAYERS_VIEW_INDEX = 2;
        private const int RECENT_PLAYERS_VIEW_INDEX = 3;

        private const int LB_USERS_WIDTH = 150;

        private readonly string DEFAULT_PLAYERS_TEXT = "玩家:";
        private readonly string RECENT_PLAYERS_TEXT = "最近玩家:";

        private CnCNetUserData cncnetUserData;
        private readonly PrivateMessageHandler privateMessageHandler;

        public PrivateMessagingWindow(
            WindowManager windowManager,
            CnCNetManager connectionManager,
            GameCollection gameCollection,
            CnCNetUserData cncnetUserData,
            PrivateMessageHandler privateMessageHandler
        ) : base(windowManager)
        {
            this.gameCollection = gameCollection;
            this.connectionManager = connectionManager;
            this.cncnetUserData = cncnetUserData;
            this.privateMessageHandler = privateMessageHandler;
        }

        private XNALabel lblPrivateMessaging;

        private XNAClientTabControl tabControl;

        private XNALabel lblPlayers;
        private XNAListBox lbUserList;
        private RecentPlayerTable mclbRecentPlayerList;

        private XNALabel lblMessages;
        private ChatListBox lbMessages;

        private XNATextBox tbMessageInput;

        private GlobalContextMenu globalContextMenu;

        private CnCNetManager connectionManager;

        private GameCollection gameCollection;

        private Texture2D unknownGameIcon;
        private Texture2D adminGameIcon;

        private Color personalMessageColor;
        private Color otherUserMessageColor;

        private string lastReceivedPMSender;
        private string lastConversationPartner;

        /// <summary>
        /// 保存本地用户在此客户端会话期间与之进行过对话的用户。
        /// </summary>
        private List<PrivateMessageUser> privateMessageUsers = new List<PrivateMessageUser>();

        private PrivateMessageNotificationBox notificationBox;

        private EnhancedSoundEffect sndPrivateMessageSound;
        private EnhancedSoundEffect sndMessageSound;

        /// <summary>
        /// 因为用户在游戏中无法查看私信，我们将游戏期间收到的最新私信
        /// 存储在此变量中，并在用户从游戏返回后显示。
        /// </summary>
        private PrivateMessage pmReceivedDuringGame;

        // 这些由右键菜单中的"邀请加入游戏"功能使用，
        // 由大厅保持更新
        private string inviteChannelName;
        private string inviteGameName;
        private string inviteChannelPassword;

        private Action<IRCUser, IMessageView> JoinUserAction;

        public override void Initialize()
        {
            Name = nameof(PrivateMessagingWindow);
            ClientRectangle = new Rectangle(0, 0, 600, 600);
            BackgroundTexture = AssetLoader.LoadTextureUncached("privatemessagebg.png");

            var assembly = Assembly.GetAssembly(typeof(GameCollection));
            using Stream unknownIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
            using Stream cncnetIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncneticon.png");

            unknownGameIcon = AssetLoader.TextureFromImage(Image.Load(unknownIconStream));
            adminGameIcon = AssetLoader.TextureFromImage(Image.Load(cncnetIconStream));

            personalMessageColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.SentPMColor);
            otherUserMessageColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.ReceivedPMColor);

            lblPrivateMessaging = new XNALabel(WindowManager);
            lblPrivateMessaging.Name = nameof(lblPrivateMessaging);
            lblPrivateMessaging.FontIndex = 1;
            lblPrivateMessaging.Text = "私信";

            AddChild(lblPrivateMessaging);
            lblPrivateMessaging.CenterOnParent();
            lblPrivateMessaging.ClientRectangle = new Rectangle(
                lblPrivateMessaging.X, 12,
                lblPrivateMessaging.Width,
                lblPrivateMessaging.Height);

            tabControl = new XNAClientTabControl(WindowManager);
            tabControl.Name = nameof(tabControl);
            tabControl.ClientRectangle = new Rectangle(34, 50, 0, 0);
            tabControl.ClickSound = new EnhancedSoundEffect("button.wav");
            tabControl.FontIndex = 1;
            tabControl.AddTab("消息", UIDesignConstants.BUTTON_WIDTH_133);
            tabControl.AddTab("好友列表", UIDesignConstants.BUTTON_WIDTH_133);
            tabControl.AddTab("所有玩家", UIDesignConstants.BUTTON_WIDTH_133);
            tabControl.AddTab("最近玩家", UIDesignConstants.BUTTON_WIDTH_133);
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            lblPlayers = new XNALabel(WindowManager);
            lblPlayers.Name = nameof(lblPlayers);
            lblPlayers.ClientRectangle = new Rectangle(12, tabControl.Bottom + 24, 0, 0);
            lblPlayers.FontIndex = 1;
            lblPlayers.Text = DEFAULT_PLAYERS_TEXT;

            lbUserList = new XNAListBox(WindowManager);
            lbUserList.Name = nameof(lbUserList);
            lbUserList.ClientRectangle = new Rectangle(lblPlayers.X,
                lblPlayers.Bottom + 6,
                LB_USERS_WIDTH, Height - lblPlayers.Bottom - 18);
            lbUserList.RightClick += LbUserList_RightClick;
            lbUserList.SelectedIndexChanged += LbUserList_SelectedIndexChanged;
            lbUserList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbUserList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbUserList.DoubleLeftClick += UserList_LeftDoubleClick;

            lblMessages = new XNALabel(WindowManager);
            lblMessages.Name = nameof(lblMessages);
            lblMessages.ClientRectangle = new Rectangle(lbUserList.Right + 12,
                lblPlayers.Y, 0, 0);
            lblMessages.FontIndex = 1;
            lblMessages.Text = "消息:";

            lbMessages = new ChatListBox(WindowManager);
            lbMessages.Name = nameof(lbMessages);
            lbMessages.ClientRectangle = new Rectangle(lblMessages.X,
                lbUserList.Y,
                Width - lblMessages.X - 12,
                lbUserList.Height - 25);
            lbMessages.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbMessages.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbMessages.RightClick += ChatListBox_RightClick;

            tbMessageInput = new XNATextBox(WindowManager);
            tbMessageInput.Name = nameof(tbMessageInput);
            tbMessageInput.ClientRectangle = new Rectangle(lbMessages.X,
                lbMessages.Bottom + 6, lbMessages.Width, 19);
            tbMessageInput.EnterPressed += TbMessageInput_EnterPressed;
            tbMessageInput.MaximumTextLength = 200;
            tbMessageInput.Enabled = false;

            mclbRecentPlayerList = new RecentPlayerTable(WindowManager, connectionManager);
            mclbRecentPlayerList.ClientRectangle = new Rectangle(lbUserList.X, lbUserList.Y, lbMessages.Right - lbUserList.X, lbUserList.Height);
            mclbRecentPlayerList.PlayerRightClick += RecentPlayersList_RightClick;
            mclbRecentPlayerList.Disable();

            globalContextMenu = new GlobalContextMenu(WindowManager, connectionManager, cncnetUserData, this);
            globalContextMenu.JoinEvent += PlayerContextMenu_JoinUser;

            notificationBox = new PrivateMessageNotificationBox(WindowManager);
            notificationBox.Enabled = false;
            notificationBox.Visible = false;
            notificationBox.LeftClick += NotificationBox_LeftClick;

            AddChild(tabControl);
            AddChild(lblPlayers);
            AddChild(lbUserList);
            AddChild(lblMessages);
            AddChild(lbMessages);
            AddChild(tbMessageInput);
            AddChild(mclbRecentPlayerList);
            AddChild(globalContextMenu);
            WindowManager.AddAndInitializeControl(notificationBox);

            base.Initialize();

            CenterOnParent();

            tabControl.SelectedTab = MESSAGES_INDEX;

            privateMessageHandler.PrivateMessageReceived += PrivateMessageHandler_PrivateMessageReceived;
            connectionManager.UserAdded += ConnectionManager_UserAdded;
            connectionManager.UserRemoved += ConnectionManager_UserRemoved;
            connectionManager.UserGameIndexUpdated += ConnectionManager_UserGameIndexUpdated;

            sndMessageSound = new EnhancedSoundEffect("message.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundMessageCooldown);

            sndPrivateMessageSound = new EnhancedSoundEffect("pm.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundPrivateMessageCooldown);

            sndMessageSound.Enabled = UserINISettings.Instance.MessageSound;

            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;
        }

        private void ChatListBox_RightClick(object sender, EventArgs e)
        {
            if (lbMessages.HoveredIndex < 0 || lbMessages.HoveredIndex >= lbMessages.Items.Count)
                return;

            lbMessages.SelectedIndex = lbMessages.HoveredIndex;
            var chatMessage = lbMessages.SelectedItem.Tag as ChatMessage;
            if (chatMessage == null)
                return;

            globalContextMenu.Show(chatMessage, GetCursorPoint());
        }

        private void UserList_LeftDoubleClick(object sender, EventArgs e)
        {
            if (lbUserList.SelectedItem != null)
                tabControl.SelectedTab = MESSAGES_INDEX;
        }

        private void RecentPlayersList_RightClick(object sender, RecentPlayerTableRightClickEventArgs e)
            => globalContextMenu.Show(e.IrcUser, GetCursorPoint());

        private void ConnectionManager_UserGameIndexUpdated(object sender, UserEventArgs e)
        {
            var userItem = FindItemForName(e.User.Name);

            if (userItem != null)
                userItem.Texture = GetUserTexture(e.User);
        }

        private void ConnectionManager_UserRemoved(object sender, UserNameIndexEventArgs e)
        {
            var pmUser = privateMessageUsers.Find(pmsgUser => pmsgUser.IrcUser.Name == e.UserName);
            ChatMessage leaveMessage = null;

            if (pmUser != null)
            {
                leaveMessage = new ChatMessage(Color.White,
                    string.Format("{0}已离线.", e.UserName));
                pmUser.Messages.Add(leaveMessage);
            }

            if (tabControl.SelectedTab == ALL_PLAYERS_VIEW_INDEX)
            {
                if (e.UserIndex >= lbUserList.Items.Count || e.UserIndex < 0)
                    return;

                if (e.UserIndex == lbUserList.SelectedIndex)
                {
                    lbUserList.SelectedIndex = -1;
                }
                else if (e.UserIndex < lbUserList.SelectedIndex)
                {
                    lbUserList.SelectedIndexChanged -= LbUserList_SelectedIndexChanged;
                    lbUserList.SelectedIndex--;
                    lbUserList.SelectedIndexChanged += LbUserList_SelectedIndexChanged;
                }

                lbUserList.Items.RemoveAt(e.UserIndex);
            }
            else
            {
                XNAListBoxItem lbItem = FindItemForName(e.UserName);

                if (lbItem != null)
                {
                    lbItem.TextColor = UISettings.ActiveSettings.DisabledItemColor;
                    lbItem.Texture = null;
                    lbItem.Tag = null;

                    if (lbItem == lbUserList.SelectedItem && leaveMessage != null)
                    {
                        tbMessageInput.Enabled = false;
                        lbMessages.AddMessage(leaveMessage);
                    }
                }
            }
        }

        private void ConnectionManager_UserAdded(object sender, UserEventArgs e)
        {
            var pmUser = privateMessageUsers.Find(pmsgUser => pmsgUser.IrcUser.Name == e.User.Name);

            ChatMessage joinMessage = null;

            if (pmUser != null)
            {
                joinMessage = new ChatMessage(string.Format("{0}已离线.", e.User.Name));
                pmUser.Messages.Add(joinMessage);
            }

            if (tabControl.SelectedTab == ALL_PLAYERS_VIEW_INDEX)
            {
                RefreshAllUsers();
            }
            else // if (tabControl.SelectedTab == 0 or 1)
            {
                XNAListBoxItem lbItem = FindItemForName(e.User.Name);

                if (lbItem != null)
                {
                    lbItem.Tag = e.User;
                    lbItem.Texture = GetUserTexture(e.User);

                    if (lbItem == lbUserList.SelectedItem)
                    {
                        tbMessageInput.Enabled = true;

                        if (joinMessage != null)
                            lbMessages.AddMessage(joinMessage);
                    }
                }
            }
        }

        private void RefreshAllUsers()
        {
            lbUserList.SelectedIndexChanged -= LbUserList_SelectedIndexChanged;

            string selectedUserName = string.Empty;

            var selectedItem = lbUserList.SelectedItem;
            if (selectedItem != null)
                selectedUserName = selectedItem.Text;

            lbUserList.Clear();

            foreach (var ircUser in connectionManager.UserList)
            {
                var item = new XNAListBoxItem(ircUser.Name);
                item.Tag = ircUser;
                item.Texture = GetUserTexture(ircUser);
                lbUserList.AddItem(item);
            }

            lbUserList.SelectedIndex = FindItemIndexForName(selectedUserName);

            if (lbUserList.SelectedIndex == -1)
            {
                // 如果我们之前选中了一个用户，而该用户现在已下线，
                // 则清除消息和消息输入
                tbMessageInput.Text = string.Empty;
                tbMessageInput.Enabled = false;
                lbMessages.Clear();
                lbMessages.SelectedIndex = -1;
                lbMessages.TopIndex = 0;
            }

            lbUserList.SelectedIndexChanged += LbUserList_SelectedIndexChanged;
        }

        public void SetInviteChannelInfo(string channelName, string gameName, string channelPassword)
        {
            inviteChannelName = channelName;
            inviteGameName = gameName;
            inviteChannelPassword = channelPassword;
        }

        public void ClearInviteChannelInfo() => SetInviteChannelInfo(string.Empty, string.Empty, string.Empty);

        private void NotificationBox_LeftClick(object sender, EventArgs e) => SwitchOn();

        private void LbUserList_RightClick(object sender, EventArgs e)
        {
            lbUserList.SelectedIndex = lbUserList.HoveredIndex;
            var ircUser = (IRCUser)lbUserList.SelectedItem?.Tag;
            if (ircUser == null)
                return;

            globalContextMenu.Show(new GlobalContextMenuData()
            {
                IrcUser = ircUser,
                inviteChannelName = inviteChannelName,
                inviteChannelPassword = inviteChannelPassword,
                inviteGameName = inviteGameName
            }, GetCursorPoint());
        }

        private void PlayerContextMenu_JoinUser(object sender, JoinUserEventArgs args)
        {
            if (tabControl.SelectedTab == RECENT_PLAYERS_VIEW_INDEX)
                JoinUserAction(args.IrcUser, new RecentPlayerMessageView(WindowManager));
            else
                JoinUserAction(args.IrcUser, lbMessages);
        }

        private void SharedUILogic_GameProcessExited() =>
            WindowManager.AddCallback(new Action(HandleGameProcessExited), null);

        private void HandleGameProcessExited()
        {
            if (pmReceivedDuringGame != null)
            {
                ShowNotification(pmReceivedDuringGame.User, pmReceivedDuringGame.Message);
                pmReceivedDuringGame = null;
            }
        }

        private bool IsPlayerOnline(string playerName) => !string.IsNullOrEmpty(playerName) && connectionManager.UserList.Find(u => u.Name == playerName) != null;

        private void PrivateMessageHandler_PrivateMessageReceived(object sender, PrivateMessageEventArgs e)
        {
            if (UserINISettings.Instance.AllowPrivateMessagesFromState == (int)AllowPrivateMessagesFromEnum.None)
                return;

            PrivateMessageUser pmUser = privateMessageUsers.Find(u => u.IrcUser.Name == e.Sender);

            if (pmUser == null)
            {
                pmUser = new PrivateMessageUser(e.ircUser);
                privateMessageUsers.Add(pmUser);

                if (tabControl.SelectedTab == MESSAGES_INDEX)
                {
                    string selecterUserName = string.Empty;

                    if (lbUserList.SelectedItem != null)
                        selecterUserName = lbUserList.SelectedItem.Text;

                    lbUserList.Clear();
                    privateMessageUsers.ForEach(pmsgUser => AddPlayerToList(pmsgUser.IrcUser,
                        IsPlayerOnline(pmsgUser.IrcUser.Name)));

                    lbUserList.SelectedIndex = FindItemIndexForName(selecterUserName);
                }
            }

            if (UserINISettings.Instance.AllowPrivateMessagesFromState == (int)AllowPrivateMessagesFromEnum.Friends && !cncnetUserData.IsFriend(pmUser.IrcUser.Name))
                return;

            ChatMessage message = new ChatMessage(e.Sender, otherUserMessageColor, DateTime.Now, e.Message);

            pmUser.Messages.Add(message);

            lastReceivedPMSender = e.Sender;
            lastConversationPartner = e.Sender;

            if (!Visible)
            {
                HandleNotification(pmUser.IrcUser, e.Message);

                if (lbUserList.SelectedItem == null || lbUserList.SelectedItem.Text != e.Sender)
                    return;
            }
            else if (lbUserList.SelectedItem == null || lbUserList.SelectedItem.Text != e.Sender)
            {
                HandleNotification(pmUser.IrcUser, e.Message);
                return;
            }

            lbMessages.AddMessage(message);
            if (sndMessageSound != null)
                sndMessageSound.Play();
        }

        /// <summary>
        /// 如果用户不在游戏中则显示私信消息，如果用户在游戏中则将其排队等待游戏结束后显示。
        /// </summary>
        /// <param name="ircUser">私信的发送者。</param>
        /// <param name="message">私信的内容。</param>
        private void HandleNotification(IRCUser ircUser, string message)
        {
            if (!ProgramConstants.IsInGame)
            {
                ShowNotification(ircUser, message);
            }
            else
                pmReceivedDuringGame = new PrivateMessage(ircUser, message);
        }

        private void ShowNotification(IRCUser ircUser, string message)
        {
            if (!UserINISettings.Instance.DisablePrivateMessagePopups)
                notificationBox.Show(GetUserTexture(ircUser), ircUser.Name, message);
            else
                privateMessageHandler.IncrementUnreadMessageCount();

            if (sndPrivateMessageSound != null)
                sndPrivateMessageSound.Play();
        }

        private Predicate<XNAListBoxItem> MatchItemForName(string userName) => item => ((IRCUser)item.Tag)?.Name == userName;

        private XNAListBoxItem FindItemForName(string userName) => lbUserList.Items.Find(MatchItemForName(userName));

        private int FindItemIndexForName(string userName) => lbUserList.Items.FindIndex(MatchItemForName(userName));

        private void TbMessageInput_EnterPressed(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbMessageInput.Text))
                return;

            if (lbUserList.SelectedItem == null)
                return;

            string userName = lbUserList.SelectedItem.Text;

            connectionManager.SendCustomMessage(new QueuedMessage("PRIVMSG " + userName + " :" + tbMessageInput.Text,
                QueuedMessageType.CHAT_MESSAGE, 0));

            PrivateMessageUser pmUser = privateMessageUsers.Find(u => u.IrcUser.Name == userName);
            if (pmUser == null)
            {
                IRCUser iu = connectionManager.UserList.Find(u => u.Name == userName);

                if (iu == null)
                {
                    Logger.Log("Null IRCUser in private messaging?");
                    return;
                }

                pmUser = new PrivateMessageUser(iu);
                privateMessageUsers.Add(pmUser);
            }

            ChatMessage sentMessage = new ChatMessage(ProgramConstants.PLAYERNAME,
                personalMessageColor, DateTime.Now, tbMessageInput.Text);

            pmUser.Messages.Add(sentMessage);

            lbMessages.AddMessage(sentMessage);
            if (sndMessageSound != null)
                sndMessageSound.Play();

            lastConversationPartner = userName;

            if (tabControl.SelectedTab != MESSAGES_INDEX)
            {
                tabControl.SelectedTab = MESSAGES_INDEX;
                lbUserList.SelectedIndex = FindItemIndexForName(userName);
            }

            tbMessageInput.Text = string.Empty;
        }

        private void LbUserList_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbMessages.Clear();
            lbMessages.SelectedIndex = -1;
            lbMessages.TopIndex = 0;
            tbMessageInput.Text = string.Empty;

            if (lbUserList.SelectedItem == null)
            {
                tbMessageInput.Enabled = false;
                return;
            }

            var ircUser = (IRCUser)lbUserList.SelectedItem.Tag;
            tbMessageInput.Enabled = IsPlayerOnline(ircUser?.Name);

            var pmUser = privateMessageUsers.Find(u =>
                u.IrcUser.Name == lbUserList.SelectedItem.Text);

            if (pmUser == null)
            {
                return;
            }

            foreach (ChatMessage message in pmUser.Messages)
            {
                lbMessages.AddMessage(message);
            }

            lbMessages.ScrollToBottom();
        }

        private void MessagesTabSelected()
        {
            ShowRecentPlayers(false);
            var _privateMessageUsers = privateMessageUsers.Select(pMsgUser =>
                new
                {
                    ircUser = pMsgUser.IrcUser,
                    isFriend = cncnetUserData.FriendList.Contains(pMsgUser.IrcUser.Name),
                    isOnline = connectionManager.UserList.Any(u => u.Name == pMsgUser.IrcUser.Name)
                });

            var sortedPrivateMessageUsers = _privateMessageUsers
                .OrderBy(pMsgUser => !pMsgUser.isOnline)
                .ThenBy(pMsgUser => !pMsgUser.isFriend)
                .ThenBy(pMsguser => pMsguser.ircUser.Name);

            foreach (var pMsgUser in sortedPrivateMessageUsers)
                AddPlayerToList(pMsgUser.ircUser, pMsgUser.isOnline);
        }

        private void FriendsListTabSelected()
        {
            ShowRecentPlayers(false);
            var friends = cncnetUserData.FriendList.Select(friendName =>
            {
                var ircUser = connectionManager.UserList.Find(u => u.Name == friendName);

                return new
                {
                    ircUser = ircUser ?? new IRCUser(friendName),
                    isOnline = ircUser != null
                };
            });

            friends
                .OrderBy(friend => !friend.isOnline)
                .ThenBy(friend => friend.ircUser.Name)
                .ToList()
                .ForEach(friend => AddPlayerToList(friend.ircUser, friend.isOnline));
        }

        private void RecentPlayersTabSelected()
        {
            ShowRecentPlayers(true);
            var recentPlayers = cncnetUserData.RecentList.OrderByDescending(rp => rp.GameTime);
            mclbRecentPlayerList.ClearItems();

            foreach (RecentPlayer recentPlayer in recentPlayers)
                mclbRecentPlayerList.AddRecentPlayer(recentPlayer);
        }

        private void AllPlayersTabSelected()
        {
            ShowRecentPlayers(false);

            foreach (var user in connectionManager.UserList)
                AddPlayerToList(user, true);
        }

        private void ShowRecentPlayers(bool show)
        {
            if (show)
            {
                lbMessages.Disable();
                tbMessageInput.Disable();
                lblMessages.Disable();
                lbUserList.Disable();
                lblPlayers.Text = RECENT_PLAYERS_TEXT;
                mclbRecentPlayerList.Enable();
            }
            else
            {
                lbMessages.Enable();
                tbMessageInput.Enable();
                lblMessages.Enable();
                lbUserList.Enable();
                lblPlayers.Text = DEFAULT_PLAYERS_TEXT;
                mclbRecentPlayerList.Disable();
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbMessages.Clear();
            lbMessages.SelectedIndex = -1;
            lbMessages.TopIndex = 0;
            lbUserList.Clear();
            lbUserList.SelectedIndex = -1;
            lbUserList.TopIndex = 0;
            tbMessageInput.Text = string.Empty;

            switch (tabControl.SelectedTab)
            {
                case MESSAGES_INDEX:
                    MessagesTabSelected();
                    break;
                case FRIEND_LIST_VIEW_INDEX:
                    FriendsListTabSelected();
                    break;
                case ALL_PLAYERS_VIEW_INDEX:
                    AllPlayersTabSelected();
                    break;
                case RECENT_PLAYERS_VIEW_INDEX:
                    RecentPlayersTabSelected();
                    break;
            }
        }

        private void AddPlayerToList(IRCUser user, bool isOnline, string label = null)
        {
            XNAListBoxItem lbItem = new XNAListBoxItem();
            lbItem.Text = label ?? user.Name;

            lbItem.TextColor = isOnline ?
                UISettings.ActiveSettings.AltColor : UISettings.ActiveSettings.DisabledItemColor;
            lbItem.Tag = user;
            lbItem.Texture = isOnline ? GetUserTexture(user) : null;

            lbUserList.AddItem(lbItem);
        }

        private Texture2D GetUserTexture(IRCUser user)
        {
            if (user.GameID < 0 || user.GameID >= gameCollection.GameList.Count)
                return unknownGameIcon;
            else
                return gameCollection.GameList[user.GameID].Texture;
        }

        /// <summary>
        /// 准备私信接收者。
        /// </summary>
        /// <param name="name">接收者名称。</param>
        public void InitPM(string name)
        {
            Visible = true;
            Enabled = true;

            // 检查我们在此会话期间是否已经与该用户交谈过
            // 如果是，则打开之前的对话
            int pmUserIndex = privateMessageUsers.FindIndex(
                pmUser => pmUser.IrcUser.Name == name);

            if (pmUserIndex > -1)
            {
                tabControl.SelectedTab = MESSAGES_INDEX;
                lbUserList.SelectedIndex = FindItemIndexForName(name);
                WindowManager.SelectedControl = tbMessageInput;
                return;
            }

            if (cncnetUserData.IsFriend(name))
            {
                // 如果我们尚未与该用户交谈过，检查他们是否为好友，
                // 如果是，则进入好友列表并在那里与他们交谈
                tabControl.SelectedTab = FRIEND_LIST_VIEW_INDEX;
            }
            else
            {
                // 如果该用户不是好友，切换到"所有玩家"视图
                // 并在那里打开对话
                tabControl.SelectedTab = ALL_PLAYERS_VIEW_INDEX;
            }

            lbUserList.SelectedIndex = FindItemIndexForName(name);

            if (lbUserList.SelectedIndex > -1)
            {
                WindowManager.SelectedControl = tbMessageInput;

                lbUserList.TopIndex = lbUserList.SelectedIndex > -1 ? lbUserList.SelectedIndex : 0;
            }

            if (lbUserList.LastIndex - lbUserList.TopIndex < lbUserList.NumberOfLinesOnList - 1)
                lbUserList.ScrollToBottom();
        }

        public void SwitchOn()
        {
            tabControl.SelectedTab = MESSAGES_INDEX;
            notificationBox.Hide();

            WindowManager.SelectedControl = null;
            privateMessageHandler.ResetUnreadMessageCount();

            if (Visible)
            {
                if (!string.IsNullOrEmpty(lastReceivedPMSender))
                {
                    int index = FindItemIndexForName(lastReceivedPMSender);

                    if (index > -1)
                        lbUserList.SelectedIndex = index;
                }
            }
            else
            {
                Enable();

                if (!string.IsNullOrEmpty(lastConversationPartner))
                {
                    int index = FindItemIndexForName(lastConversationPartner);

                    if (index > -1)
                    {
                        lbUserList.SelectedIndex = index;
                        WindowManager.SelectedControl = tbMessageInput;
                    }
                }
            }
        }

        public void SetJoinUserAction(Action<IRCUser, IMessageView> joinUserAction)
        {
            JoinUserAction = joinUserAction;
        }

        public void SwitchOff() => Disable();

        public string GetSwitchName() => "私信";

        /// <summary>
        /// 用于在内存中存储私信的类。
        /// </summary>
        class PrivateMessage
        {
            public PrivateMessage(IRCUser user, string message)
            {
                User = user;
                Message = message;
            }

            public IRCUser User;
            public string Message;
        }

        class RecentPlayerMessageView : IMessageView
        {
            private readonly WindowManager windowManager;

            public RecentPlayerMessageView(WindowManager windowManager)
            {
                this.windowManager = windowManager;
            }

            public void AddMessage(ChatMessage message)
                => XNAMessageBox.Show(windowManager, "消息", message.Message);
        }
    }
}
