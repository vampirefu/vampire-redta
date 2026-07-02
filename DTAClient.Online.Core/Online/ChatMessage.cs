using Microsoft.Xna.Framework;
using System;

namespace DTAClient.Online
{
    public class ChatMessage
    {
        /// <summary>
        /// 创建新的 ChatMessage 实例。
        /// </summary>
        /// <param name="senderName">消息的发送者。对于无发送者（系统消息）使用 null。</param>
        /// <param name="color">消息的颜色。</param>
        /// <param name="dateTime">消息的日期和时间。</param>
        /// <param name="message">消息内容。</param>
        public ChatMessage(string senderName, Color color, DateTime dateTime, string message)
        {
            SenderName = senderName;
            Color = color;
            DateTime = dateTime;
            Message = message;
        }

        /// <summary>
        /// 创建日期和时间设置为当前系统日期和时间的聊天消息。
        /// </summary>
        /// <param name="senderName">消息的发送者。对于无发送者（系统消息）使用 null。</param>
        /// <param name="color">消息的颜色。</param>
        /// <param name="message">消息内容。</param>
        public ChatMessage(string senderName, Color color, string message) : this(senderName, color, DateTime.Now, message) { }

        /// <summary>
        /// 创建新的 ChatMessage 实例。
        /// </summary>
        /// <param name="senderName">消息的发送者。对于无发送者（系统消息）使用 null。</param>
        /// <param name="ident">发送者的 IRC 标识符。</param>
        /// <param name="senderIsAdmin">消息的发送者是否为频道管理员。</param>
        /// <param name="color">消息的颜色。</param>
        /// <param name="dateTime">消息的日期和时间。</param>
        /// <param name="message">消息内容。</param>
        public ChatMessage(string senderName, string ident, bool senderIsAdmin, Color color, DateTime dateTime, string message) : this(senderName, color, dateTime, message)
        {
            SenderIdent = ident;
            SenderIsAdmin = senderIsAdmin;
        }

        /// <summary>
        /// 创建无发送者且日期和时间设置为当前系统日期和时间的聊天消息。
        /// </summary>
        /// <param name="color">消息的颜色。</param>
        /// <param name="message">消息内容。</param>
        public ChatMessage(Color color, string message) : this(null, color, DateTime.Now, message) { }

        /// <summary>
        /// 创建无发送者且日期和时间设置为当前系统日期和时间的聊天消息。
        /// </summary>
        /// <param name="message">消息内容。</param>
        public ChatMessage(string message) : this(Color.White, message) { }

        public string SenderName { get; private set; }
        public string SenderIdent { get; private set; }
        public Color Color { get; private set; }
        public DateTime DateTime { get; private set; }
        public string Message { get; private set; }
        public bool SenderIsAdmin { get; private set; }

        public bool IsUser => SenderIdent != null;
    }
}
