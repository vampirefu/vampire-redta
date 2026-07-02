using ClientCore;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DTAClient.Online
{
    public sealed class CnCNetUserData
    {
        private const string FRIEND_LIST_PATH = "Client/friend_list";
        private const string IGNORE_LIST_PATH = "Client/ignore_list";
        private const string RECENT_LIST_PATH = "Client/recent_list";

        private const int RECENT_LIMIT = 50;

        /// <summary>
        /// 包含好友用户名称的列表。如果直接操作此列表，
        /// 还必须为每个添加或移除的用户名调用 UserFriendToggled 事件处理程序。
        /// </summary>
        public List<string> FriendList { get; private set; } = new();

        /// <summary>
        /// 包含已忽略用户标识的列表。如果直接操作此列表，
        /// 还必须为每个添加或移除的用户标识调用 UserIgnoreToggled 事件处理程序。
        /// </summary>
        public List<string> IgnoreList { get; private set; } = new();

        /// <summary>
        /// 包含最近游戏中玩家名称的列表。
        /// </summary>
        public List<RecentPlayer> RecentList { get; private set; } = new();

        public event EventHandler<UserNameEventArgs> UserFriendToggled;
        public event EventHandler<IdentEventArgs> UserIgnoreToggled;

        public CnCNetUserData(WindowManager windowManager)
        {
            LoadFriendList();
            LoadIgnoreList();
            LoadRecentPlayerList();

            windowManager.GameClosing += WindowManager_GameClosing;
        }

        private static List<string> LoadTextList(string path)
        {
            try
            {
                FileInfo listFile = SafePath.GetFile(ProgramConstants.GamePath, path);

                if (listFile.Exists)
                    return File.ReadAllLines(listFile.FullName).ToList();

                Logger.Log($"Loading {path} failed! File does not exist.");
                return new();
            }
            catch
            {
                Logger.Log($"Loading {path} list failed!");
                return new();
            }
        }

        private static List<T> LoadJsonList<T>(string path)
        {
            try
            {
                FileInfo listFile = SafePath.GetFile(ProgramConstants.GamePath, path);

                if (listFile.Exists)
                    return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(listFile.FullName)) ?? new List<T>();

                Logger.Log($"Loading {path} failed! File does not exist.");
                return new();
            }
            catch
            {
                Logger.Log($"Loading {path} list failed!");
                return new();
            }
        }

        private static void SaveTextList(string path, List<string> textList)
        {
            Logger.Log($"Saving {path}.");

            try
            {
                FileInfo listFileInfo = SafePath.GetFile(ProgramConstants.GamePath, path);

                listFileInfo.Delete();
                File.WriteAllLines(listFileInfo.FullName, textList.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Log($"Saving {path} failed! Error message: " + ex.Message);
            }
        }

        private static void SaveJsonList<T>(string path, IReadOnlyCollection<T> jsonList)
        {
            Logger.Log($"Saving {path}.");

            try
            {
                FileInfo listFileInfo = SafePath.GetFile(ProgramConstants.GamePath, path);

                listFileInfo.Delete();
                File.WriteAllText(listFileInfo.FullName, JsonSerializer.Serialize(jsonList));
            }
            catch (Exception ex)
            {
                Logger.Log($"Saving {path} failed! Error message: " + ex.Message);
            }
        }

        private static void Toggle(string value, ICollection<string> list)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (list.Contains(value))
                list.Remove(value);
            else
                list.Add(value);
        }

        private void LoadFriendList() => FriendList = LoadTextList(FRIEND_LIST_PATH);

        private void LoadIgnoreList() => IgnoreList = LoadTextList(IGNORE_LIST_PATH);

        private void LoadRecentPlayerList() => RecentList = LoadJsonList<RecentPlayer>(RECENT_LIST_PATH);

        private void WindowManager_GameClosing(object sender, EventArgs e) => Save();

        private void SaveFriends() => SaveTextList(FRIEND_LIST_PATH, FriendList);

        private void SaveIgnoreList() => SaveTextList(IGNORE_LIST_PATH, IgnoreList);

        private void SaveRecentList() => SaveJsonList(RECENT_LIST_PATH, RecentList);

        private void Save()
        {
            SaveFriends();
            SaveIgnoreList();
            SaveRecentList();
        }

        /// <summary>
        /// 根据指定用户是否已在好友列表中，将其添加到或从好友列表中移除。
        /// </summary>
        /// <param name="name">用户的名称。</param>
        public void ToggleFriend(string name)
        {
            Toggle(name, FriendList);
            UserFriendToggled?.Invoke(this, new(name));
        }

        /// <summary>
        /// 根据指定用户是否已在忽略列表中，将其添加到或从聊天忽略列表中移除。
        /// </summary>
        /// <param name="ident">IRCUser 的标识符。</param>
        public void ToggleIgnoreUser(string ident)
        {
            Toggle(ident, IgnoreList);
            UserIgnoreToggled?.Invoke(this, new(ident));
        }

        public void AddRecentPlayers(IEnumerable<string> recentPlayerNames, string gameName)
        {
            recentPlayerNames = recentPlayerNames.Where(name => name != ProgramConstants.PLAYERNAME);
            var now = DateTime.UtcNow;
            RecentList.AddRange(recentPlayerNames.Select(rp => new RecentPlayer()
            {
                PlayerName = rp,
                GameName = gameName,
                GameTime = now
            }));
            int skipCount = Math.Max(0, RecentList.Count - RECENT_LIMIT);
            RecentList = RecentList.Skip(skipCount).ToList();
        }

        /// <summary>
        /// 检查用户是否在忽略列表中。
        /// </summary>
        /// <param name="ident">用户的 IRC 标识符。</param>
        public bool IsIgnored(string ident) => IgnoreList.Contains(ident);

        /// <summary>
        /// 检查指定用户是否属于好友列表。
        /// </summary>
        /// <param name="name">用户的名称。</param>
        public bool IsFriend(string name) => FriendList.Contains(name);
    }

    public sealed class IdentEventArgs : EventArgs
    {
        public IdentEventArgs(string ident)
        {
            Ident = ident;
        }

        public string Ident { get; }
    }
}