using ClientCore;
using DTAClient.Online;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace DTAClient.Domain.Multiplayer.CnCNet
{
    public class TunnelHandler : GameComponent
    {
        /// <summary>
        /// 确定当前隧道（如果已设置）的ping间隔时间。
        /// </summary>
        private const double CURRENT_TUNNEL_PING_INTERVAL = 20.0;

        /// <summary>
        /// 决定完整隧道列表刷新频率（而非仅ping当前隧道）的值的倒数
        /// （1/N的当前隧道ping刷新将被完整列表刷新替代）。
        /// 乘以<see cref="CURRENT_TUNNEL_PING_INTERVAL"/>可获得完整列表刷新的间隔时间。
        /// </summary>
        private const uint CYCLES_PER_TUNNEL_LIST_REFRESH = 6;

        private const int SUPPORTED_TUNNEL_VERSION = 2;

        public TunnelHandler(WindowManager wm, CnCNetManager connectionManager) : base(wm.Game)
        {
            this.wm = wm;
            this.connectionManager = connectionManager;

            wm.Game.Components.Add(this);

            Enabled = false;

            connectionManager.Connected += ConnectionManager_Connected;
            connectionManager.Disconnected += ConnectionManager_Disconnected;
            connectionManager.ConnectionLost += ConnectionManager_ConnectionLost;
        }

        public List<CnCNetTunnel> Tunnels { get; private set; } = new List<CnCNetTunnel>();
        public CnCNetTunnel CurrentTunnel { get; set; } = null;

        public event EventHandler TunnelsRefreshed;
        public event EventHandler CurrentTunnelPinged;
        public event Action<int> TunnelPinged;

        private WindowManager wm;
        private CnCNetManager connectionManager;

        private TimeSpan timeSinceTunnelRefresh = TimeSpan.MaxValue;
        private uint skipCount = 0;

        private void DoTunnelPinged(int index)
        {
            if (TunnelPinged != null)
                wm.AddCallback(TunnelPinged, index);
        }

        private void DoCurrentTunnelPinged()
        {
            if (CurrentTunnelPinged != null)
                wm.AddCallback(CurrentTunnelPinged, this, EventArgs.Empty);
        }

        private void ConnectionManager_Connected(object sender, EventArgs e) => Enabled = true;

        private void ConnectionManager_ConnectionLost(object sender, Online.EventArguments.ConnectionLostEventArgs e) => Enabled = false;

        private void ConnectionManager_Disconnected(object sender, EventArgs e) => Enabled = false;

        private void RefreshTunnelsAsync()
        {
            List<CnCNetTunnel> tunnels = new List<CnCNetTunnel>()
            {
                //CnCNetTunnel.Parse("101.33.75.60:40086;China;CN;Vampire_Server;0;0;30;0;0;0;2;0"),
                //CnCNetTunnel.Parse("60.247.152.72:50000;China;CN;[CN]Alliance and Hegemony;1;0;100;0;0;0;2;0")
            };

            Task.Factory.StartNew(() =>
            {
                tunnels.AddRange(RefreshTunnels());
                wm.AddCallback(new Action<List<CnCNetTunnel>>(HandleRefreshedTunnels), tunnels);
            });
        }

        private void HandleRefreshedTunnels(List<CnCNetTunnel> tunnels)
        {
            if (tunnels.Count > 0)
                Tunnels = tunnels;

            TunnelsRefreshed?.Invoke(this, EventArgs.Empty);

            Task[] pingTasks = new Task[Tunnels.Count];

            for (int i = 0; i < Tunnels.Count; i++)
            {
                if (UserINISettings.Instance.PingUnofficialCnCNetTunnels || Tunnels[i].Official || Tunnels[i].Recommended)
                    pingTasks[i] = PingListTunnelAsync(i);
            }

            if (CurrentTunnel != null)
            {
                var updatedTunnel = Tunnels.Find(t => t.Address == CurrentTunnel.Address && t.Port == CurrentTunnel.Port);
                if (updatedTunnel != null)
                {
                    // 如果隧道仍在列表中则不重新ping，只更新隧道实例并
                    // 触发事件处理器（遍历隧道列表时已ping过该隧道）
                    CurrentTunnel = updatedTunnel;
                    DoCurrentTunnelPinged();
                }
                else
                {
                    // 隧道已不在列表中，因此未用列表实例更新和ping
                    PingCurrentTunnelAsync();
                }
            }
        }

        private Task PingListTunnelAsync(int index)
        {
            return Task.Factory.StartNew(() =>
            {
                Tunnels[index].UpdatePing();
                DoTunnelPinged(index);
            });
        }

        private Task PingCurrentTunnelAsync(bool checkTunnelList = false)
        {
            return Task.Factory.StartNew(() =>
            {
                CurrentTunnel.UpdatePing();
                DoCurrentTunnelPinged();

                if (checkTunnelList)
                {
                    int tunnelIndex = Tunnels.FindIndex(t => t.Address == CurrentTunnel.Address && t.Port == CurrentTunnel.Port);
                    if (tunnelIndex > -1)
                        DoTunnelPinged(tunnelIndex);
                }
            });
        }

        /// <summary>
        /// 下载并解析CnCNet隧道列表。
        /// </summary>
        /// <returns>隧道服务器列表。</returns>
        private List<CnCNetTunnel> RefreshTunnels()
        {
            FileInfo tunnelCacheFile = SafePath.GetFile(ProgramConstants.ClientUserFilesPath, "tunnel_cache");

            List<CnCNetTunnel> returnValue = new List<CnCNetTunnel>();

            WebClient client = new WebClient();

            byte[] data;

            Logger.Log("Fetching tunnel server info.");

            try
            {
                data = client.DownloadData(MainClientConstants.CNCNET_TUNNEL_LIST_URL);
            }
            catch (Exception ex)
            {
                Logger.Log("Error when downloading tunnel server info: " + ex.Message);
                Logger.Log("Retrying.");
                try
                {
                    data = client.DownloadData(MainClientConstants.CNCNET_TUNNEL_LIST_URL);
                }
                catch
                {
                    if (!tunnelCacheFile.Exists)
                    {
                        Logger.Log("Tunnel cache file doesn't exist!");
                        return returnValue;
                    }
                    else
                    {
                        Logger.Log("Fetching tunnel server list failed. Using cached tunnel data.");
                        data = File.ReadAllBytes(tunnelCacheFile.FullName);
                    }
                }
            }

            string convertedData = Encoding.Default.GetString(data);

            string[] serverList = convertedData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // 跳过第一个标题行（"address;country;countrycode;name;password;clients;maxclients;official;latitude;longitude;version;distance"）
            foreach (string serverInfo in serverList.Skip(1))
            {
                try
                {
                    CnCNetTunnel tunnel = CnCNetTunnel.Parse(serverInfo);

                    if (tunnel == null)
                        continue;

                    if (tunnel.RequiresPassword)
                        continue;

                    if (tunnel.Version != SUPPORTED_TUNNEL_VERSION)
                        continue;

                    returnValue.Add(tunnel);
                }
                catch (Exception ex)
                {
                    Logger.Log("Caught an exception when parsing a tunnel server: " + ex.Message);
                }
            }

            if (returnValue.Count > 0)
            {
                try
                {
                    if (tunnelCacheFile.Exists)
                        tunnelCacheFile.Delete();

                    DirectoryInfo clientDirectoryInfo = SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath);

                    if (!clientDirectoryInfo.Exists)
                        clientDirectoryInfo.Create();

                    File.WriteAllBytes(tunnelCacheFile.FullName, data);
                }
                catch (Exception ex)
                {
                    Logger.Log("Refreshing tunnel cache file failed! Returned error: " + ex.Message);
                }
            }

            return returnValue;
        }

        public override void Update(GameTime gameTime)
        {
            if (timeSinceTunnelRefresh > TimeSpan.FromSeconds(CURRENT_TUNNEL_PING_INTERVAL))
            {
                if (skipCount % CYCLES_PER_TUNNEL_LIST_REFRESH == 0)
                {
                    skipCount = 0;
                    RefreshTunnelsAsync();
                }
                else if (CurrentTunnel != null)
                {
                    PingCurrentTunnelAsync(true);
                }

                timeSinceTunnelRefresh = TimeSpan.Zero;
                skipCount++;
            }
            else
                timeSinceTunnelRefresh += gameTime.ElapsedGameTime;

            base.Update(gameTime);
        }
    }
}
