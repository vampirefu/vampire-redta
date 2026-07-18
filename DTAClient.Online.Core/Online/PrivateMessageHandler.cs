using System;
using DTAClient.Online.EventArguments;

namespace DTAClient.Online
{
    /// <summary>
    /// 负责处理从 CnCNet 接收的私聊消息，并执行独立于 GUI 的逻辑检查以判断消息是否应被忽略。
    /// 然后将有效的私聊消息事件转发给其他消费者。
    /// </summary>
    public class PrivateMessageHandler
    {
        private readonly CnCNetUserData _cncnetUserData;
        private readonly CnCNetManager _connectionManager;
        
        private int UnreadMessageCount;
        
        public event EventHandler<PrivateMessageEventArgs> PrivateMessageReceived;
        public event EventHandler<UnreadMessageCountEventArgs> UnreadMessageCountUpdated;

        public PrivateMessageHandler(
            CnCNetManager connectionManager,
            CnCNetUserData cncnetUserData
        )
        {
            _connectionManager = connectionManager;
            _cncnetUserData = cncnetUserData;

            _connectionManager.PrivateMessageReceived += _PrivateMessageReceived;
        }

        private void _PrivateMessageReceived(object sender, CnCNetPrivateMessageEventArgs e)
        {
            IRCUser iu = _connectionManager.UserList.Find(u => u.Name == e.Sender);

            // 我们不接受来自不共享任何频道的用户的私聊消息
            if (iu == null)
                return;

            // 来自已屏蔽用户的消息不需要
            if (_cncnetUserData.IsIgnored(iu.Ident))
                return;

            var privateMessageEventArgs = new PrivateMessageEventArgs(e.Sender, e.Message, iu);

            PrivateMessageReceived?.Invoke(this, privateMessageEventArgs);
        }

        private void DoUnreadMessageCountUpdated() 
            => UnreadMessageCountUpdated?.Invoke(this, new UnreadMessageCountEventArgs(UnreadMessageCount));

        private void SetUnreadMessageCount(int unreadMessageCount)
        {
            UnreadMessageCount = unreadMessageCount;
            DoUnreadMessageCountUpdated();
        }

        /// <summary>
        /// 可由特定 GUI 组件调用，以触发未读计数重置，
        /// 因为 PrivateMessageWindow 已变为可见。
        /// </summary>
        public void ResetUnreadMessageCount() 
            => SetUnreadMessageCount(0);

        /// <summary>
        /// 可由特定 GUI 组件调用，以触发未读计数递增，
        /// 因为 PrivateMessageWindow 当前可能不可见。
        /// </summary>
        public void IncrementUnreadMessageCount() 
            => SetUnreadMessageCount(UnreadMessageCount + 1);
    }
}
