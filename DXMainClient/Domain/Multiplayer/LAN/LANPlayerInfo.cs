using ClientCore;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DTAClient.Domain.Multiplayer.LAN
{
    public class LANPlayerInfo : PlayerInfo
    {
        public LANPlayerInfo(Encoding encoding)
        {
            this.encoding = encoding;
            Port = PORT;
        }

        public event EventHandler<NetworkMessageEventArgs> MessageReceived;
        public event EventHandler ConnectionLost;
        public event EventHandler PlayerPinged;

        private const int PORT = 1234;
        private const int LOBBY_PORT = 1233;
        private const double SEND_PING_TIMEOUT = 10.0;
        private const double DROP_TIMEOUT = 20.0;
        private const int LAN_PING_TIMEOUT = 1000;

        public TimeSpan TimeSinceLastReceivedMessage { get; set; }
        public TimeSpan TimeSinceLastSentMessage { get; set; }

        public TcpClient TcpClient { get; private set; }

        NetworkStream networkStream;

        Encoding encoding;

        string overMessage = string.Empty;

        public void SetClient(TcpClient client)
        {
            if (TcpClient != null)
                throw new InvalidOperationException("TcpClient has already been set for this LANPlayerInfo!");

            TcpClient = client;
            TcpClient.SendTimeout = 1000;
            networkStream = client.GetStream();
        }

        /// <summary>
        /// 更新玩家的逻辑计时器。
        /// </summary>
        /// <param name="gameTime">提供计时值的快照。</param>
        /// <returns>如果玩家仍被视为已连接则返回true，否则返回false。</returns>
        public bool Update(GameTime gameTime)
        {
            TimeSinceLastReceivedMessage += gameTime.ElapsedGameTime;
            TimeSinceLastSentMessage += gameTime.ElapsedGameTime;

            if (TimeSinceLastSentMessage > TimeSpan.FromSeconds(SEND_PING_TIMEOUT)
                || TimeSinceLastReceivedMessage > TimeSpan.FromSeconds(SEND_PING_TIMEOUT))
                SendMessage("PING");

            if (TimeSinceLastReceivedMessage > TimeSpan.FromSeconds(DROP_TIMEOUT))
                return false;

            return true;
        }

        public override string IPAddress
        {
            get
            {
                if (TcpClient != null)
                    return ((IPEndPoint)TcpClient.Client.RemoteEndPoint).Address.ToString();

                return base.IPAddress;
            }

            set
            {
                base.IPAddress = value;
                //throw new InvalidOperationException("Cannot set LANPlayerInfo's IPAddress!");
            }
        }

        /// <summary>
        /// 通过网络向玩家发送消息。
        /// </summary>
        /// <param name="message">要发送的消息。</param>
        public void SendMessage(string message)
        {
            byte[] buffer;

            buffer = encoding.GetBytes(message + ProgramConstants.LAN_MESSAGE_SEPARATOR);

            try
            {
                networkStream.Write(buffer, 0, buffer.Length);
                networkStream.Flush();
            }
            catch
            {
                Logger.Log("Sending message to " + ToString() + " failed!");
            }

            TimeSinceLastSentMessage = TimeSpan.Zero;
        }

        public override string ToString()
        {
            return Name + " (" + IPAddress + ")";
        }

        /// <summary>
        /// 异步开始接收来自玩家的消息。
        /// </summary>
        public void StartReceiveLoop()
        {
            Thread thread = new Thread(ReceiveMessages);
            thread.Start();
        }

        /// <summary>
        /// 接收客户端发送的消息，
        /// 并通过事件将其交给另一个类处理。
        /// </summary>
        private void ReceiveMessages()
        {
            byte[] message = new byte[1024];

            string msg = String.Empty;

            int bytesRead = 0;

            NetworkStream ns = TcpClient.GetStream();

            while (true)
            {
                bytesRead = 0;

                try
                {
                    //阻塞直到客户端发送消息
                    bytesRead = ns.Read(message, 0, message.Length);
                }
                catch (Exception ex)
                {
                    //发生了套接字错误
                    Logger.Log("Socket error with client " + Name + "; removing. Message: " + ex.Message);
                    ConnectionLost?.Invoke(this, EventArgs.Empty);
                    break;
                }

                if (bytesRead > 0)
                {
                    msg = encoding.GetString(message, 0, bytesRead);

                    msg = overMessage + msg;
                    List<string> commands = new List<string>();

                    while (true)
                    {
                        int index = msg.IndexOf(ProgramConstants.LAN_MESSAGE_SEPARATOR);

                        if (index == -1)
                        {
                            overMessage = msg;
                            break;
                        }
                        else
                        {
                            commands.Add(msg.Substring(0, index));
                            msg = msg.Substring(index + 1);
                        }
                    }

                    foreach (string cmd in commands)
                    {
                        MessageReceived?.Invoke(this, new NetworkMessageEventArgs(cmd));
                    }

                    continue;
                }

                ConnectionLost?.Invoke(this, EventArgs.Empty);
                break;
            }
        }

        public void UpdatePing(WindowManager wm)
        {
            using (Ping p = new Ping())
            {
                try
                {
                    PingReply reply = p.Send(System.Net.IPAddress.Parse(IPAddress), LAN_PING_TIMEOUT);
                    if (reply.Status == IPStatus.Success)
                        Ping = Convert.ToInt32(reply.RoundtripTime);

                    wm.AddCallback(PlayerPinged, this, EventArgs.Empty);
                }
                catch (PingException ex)
                {
                    Logger.Log($"Caught an exception when pinging {Name} LAN player: {ex.Message}");
                }
            }
        }
    }
}
