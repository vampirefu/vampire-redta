using System.Collections.Generic;
using Rampastring.XNAUI;
using System.Linq;
using System;
using System.IO;
using System.Reflection;
using Rampastring.Tools;
using SixLabors.ImageSharp;

namespace ClientCore.CnCNet5
{
    /// <summary>
    /// 存储支持的 CnCNet 游戏集合的类。
    /// </summary>
    public class GameCollection
    {
        public List<CnCNetGame> GameList { get; private set; }

        public GameCollection()
        {
            Initialize();
        }

        public void Initialize()
        {
            GameList = new List<CnCNetGame>();

            var assembly = Assembly.GetAssembly(typeof(GameCollection));
            using Stream dtaIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.dtaicon.png");
            using Stream tiIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.tiicon.png");
            using Stream tsIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.tsicon.png");
            using Stream moIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.moicon.png");
            using Stream yrIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.yricon.png");
            using Stream rrIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.rricon.png");
            using Stream reIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.reicon.png");
            using Stream cncrIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncricon.png");
            using Stream cncnetIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncneticon.png");
            using Stream tdIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.tdicon.png");
            using Stream raIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.raicon.png");
            using Stream d2kIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.d2kicon.png");
            using Stream ssIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.ssicon.png");
            using Stream unknownIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
            using var dtaIcon = Image.Load(dtaIconStream);
            using var tiIcon = Image.Load(tiIconStream);
            using var tsIcon = Image.Load(tsIconStream);
            using var moIcon = Image.Load(moIconStream);
            using var yrIcon = Image.Load(yrIconStream);
            using var rrIcon = Image.Load(rrIconStream);
            using var reIcon = Image.Load(reIconStream);
            using var cncrIcon = Image.Load(cncrIconStream);
            using var cncnetIcon = Image.Load(cncnetIconStream);
            using var tdIcon = Image.Load(tdIconStream);
            using var raIcon = Image.Load(raIconStream);
            using var d2kIcon = Image.Load(d2kIconStream);
            using var ssIcon = Image.Load(ssIconStream);
            using var unknownIcon = Image.Load(unknownIconStream);

            // 默认支持的游戏。
            CnCNetGame[] defaultGames =
            {
                //new()
                //{
                //    ChatChannel = "#cncnet-dta",
                //    ClientExecutableName = "DTA.exe",
                //    GameBroadcastChannel = "#cncnet-dta-games",
                //    InternalName = "dta",
                //    RegistryInstallPath = "HKCU\\Software\\TheDawnOfTheTiberiumAge",
                //    UIName = "Dawn of the Tiberium Age",
                //    Texture = AssetLoader.TextureFromImage(dtaIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-ti",
                //    ClientExecutableName = "TI_Launcher.exe",
                //    GameBroadcastChannel = "#cncnet-ti-games",
                //    InternalName = "ti",
                //    RegistryInstallPath = "HKCU\\Software\\TwistedInsurrection",
                //    UIName = "Twisted Insurrection",
                //    Texture = AssetLoader.TextureFromImage(tiIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-mo",
                //    ClientExecutableName = "MentalOmegaClient.exe",
                //    GameBroadcastChannel = "#cncnet-mo-games",
                //    InternalName = "mo",
                //    RegistryInstallPath = "HKCU\\Software\\MentalOmega",
                //    UIName = "Mental Omega",
                //    Texture = AssetLoader.TextureFromImage(moIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#redres-lobby",
                //    ClientExecutableName = "RRLauncher.exe",
                //    GameBroadcastChannel = "#redres-games",
                //    InternalName = "rr",
                //    RegistryInstallPath = "HKML\\Software\\RedResurrection",
                //    UIName = "YR Red-Resurrection",
                //    Texture = AssetLoader.TextureFromImage(rrIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#riseoftheeast",
                //    ClientExecutableName = "RELauncher.exe",
                //    GameBroadcastChannel = "#rote-games",
                //    InternalName = "re",
                //    RegistryInstallPath = "HKML\\Software\\RiseoftheEast",
                //    UIName = "Rise of the East",
                //    Texture = AssetLoader.TextureFromImage(reIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncreloaded",
                //    ClientExecutableName = "CnCReloadedClient.exe",
                //    GameBroadcastChannel = "#cncreloaded-games",
                //    InternalName = "cncr",
                //    RegistryInstallPath = "HKCU\\Software\\CnCReloaded",
                //    UIName = "C&C: Reloaded",
                //    Texture = AssetLoader.TextureFromImage(cncrIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-td",
                //    ClientExecutableName = "TiberianDawn.exe",
                //    GameBroadcastChannel = "#cncnet-td-games",
                //    InternalName = "td",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Tiberian Dawn",
                //    UIName = "Tiberian Dawn",
                //    Texture = AssetLoader.TextureFromImage(tdIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-ra",
                //    ClientExecutableName = "RedAlert.exe",
                //    GameBroadcastChannel = "#cncnet-ra-games",
                //    InternalName = "ra",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Red Alert",
                //    UIName = "Red Alert",
                //    Texture = AssetLoader.TextureFromImage(raIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-d2k",
                //    ClientExecutableName = "Dune2000.exe",
                //    GameBroadcastChannel = "#cncnet-d2k-games",
                //    InternalName = "d2k",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Dune 2000",
                //    UIName = "Dune 2000",
                //    Texture = AssetLoader.TextureFromImage(d2kIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-ts",
                //    ClientExecutableName = "TiberianSun.exe",
                //    GameBroadcastChannel = "#cncnet-ts-games",
                //    InternalName = "ts",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Tiberian Sun",
                //    UIName = "Tiberian Sun",
                //    Texture = AssetLoader.TextureFromImage(tsIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-yr",
                //    ClientExecutableName = "CnCNetClientYR.exe",
                //    GameBroadcastChannel = "#cncnet-yr-games",
                //    InternalName = "yr",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Yuri's Revenge",
                //    UIName = "Yuri's Revenge",
                //    Texture = AssetLoader.TextureFromImage(yrIcon)
                //},

                //new()
                //{
                //    ChatChannel = "#cncnet-ss",
                //    ClientExecutableName = "SoleSurvivor.exe",
                //    GameBroadcastChannel = "#cncnet-ss-games",
                //    InternalName = "ss",
                //    RegistryInstallPath = "HKLM\\Software\\Westwood\\Sole Survivor",
                //    UIName = "Sole Survivor",
                //    Texture = AssetLoader.TextureFromImage(ssIcon)
                //}
            };

            // CnCNet 聊天。
            CnCNetGame[] otherGames =
            {
                new()
                {
                    ChatChannel = "#cncnet",
                    InternalName = "cncnet",
                    UIName = "CnCNet综合聊天",
                    AlwaysEnabled = true,
                    Texture = AssetLoader.TextureFromImage(cncnetIcon)
                }
            };

            GameList.AddRange(defaultGames);
            GameList.AddRange(GetCustomGames(defaultGames.Concat(otherGames).ToList()));
            GameList.AddRange(otherGames);

            if (GetGameIndexFromInternalName(ClientConfiguration.Instance.LocalGame) == -1)
            {
                throw new ClientConfigurationException("Could not find a game in the game collection matching LocalGame value of " +
                    ClientConfiguration.Instance.LocalGame + ".");
            }
        }

        private List<CnCNetGame> GetCustomGames(List<CnCNetGame> existingGames)
        {
            IniFile iniFile = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "GameCollectionConfig.ini"));

            List<CnCNetGame> customGames = new List<CnCNetGame>();

            var section = iniFile.GetSection("CustomGames");

            if (section == null)
                return customGames;

            HashSet<string> customGameIDs = new HashSet<string>();
            foreach (var kvp in section.Keys)
            {
                if (!iniFile.SectionExists(kvp.Value))
                    continue;

                string ID = iniFile.GetStringValue(kvp.Value, "InternalName", string.Empty).ToLowerInvariant();

                if (string.IsNullOrEmpty(ID))
                    throw new GameCollectionConfigurationException("InternalName for game " + kvp.Value + " is not defined or set to an empty value.");

                if (ID.Length > ProgramConstants.GAME_ID_MAX_LENGTH)
                {
                    throw new GameCollectionConfigurationException("InternalGame for game " + kvp.Value + " is set to a value that exceeds length limit of " +
                        ProgramConstants.GAME_ID_MAX_LENGTH + " characters.");
                }

                if (existingGames.Find(g => g.InternalName == ID) != null || customGameIDs.Contains(ID))
                    throw new GameCollectionConfigurationException("Game with InternalName " + ID.ToUpperInvariant() + " already exists in the game collection.");

                string iconFilename = iniFile.GetStringValue(kvp.Value, "IconFilename", ID + "icon.png");
                using Stream unknownIconStream = Assembly.GetAssembly(typeof(GameCollection)).GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
                using var unknownIcon = Image.Load(unknownIconStream);
                customGames.Add(new CnCNetGame
                {
                    InternalName = ID,
                    UIName = iniFile.GetStringValue(kvp.Value, "UIName", ID.ToUpperInvariant()),
                    ChatChannel = GetIRCChannelNameFromIniFile(iniFile, kvp.Value, "ChatChannel"),
                    GameBroadcastChannel = GetIRCChannelNameFromIniFile(iniFile, kvp.Value, "GameBroadcastChannel"),
                    ClientExecutableName = iniFile.GetStringValue(kvp.Value, "ClientExecutableName", string.Empty),
                    RegistryInstallPath = iniFile.GetStringValue(kvp.Value, "RegistryInstallPath", "HKCU\\Software\\"
                            + ID.ToUpperInvariant()),
                    Texture = AssetLoader.AssetExists(iconFilename) ? AssetLoader.LoadTexture(iconFilename) :
                            AssetLoader.TextureFromImage(unknownIcon)
                });
                customGameIDs.Add(ID);
            }

            return customGames;
        }

        private string GetIRCChannelNameFromIniFile(IniFile iniFile, string section, string key)
        {
            string channel = iniFile.GetStringValue(section, key, string.Empty);

            if (string.IsNullOrEmpty(channel))
                throw new GameCollectionConfigurationException(key + " for game " + section + " is not defined or set to an empty value.");

            if (channel.Contains(' ') || channel.Contains(',') || channel.Contains((char)7))
                throw new GameCollectionConfigurationException(key + " for game " + section + " contains characters not allowed on IRC channel names.");

            if (!channel.StartsWith("#"))
                return "#" + channel;

            return channel;
        }

        /// <summary>
        /// 根据内部名称获取 CnCNet 支持游戏的索引。
        /// </summary>
        /// <param name="gameName">游戏的内部名称（后缀）。</param>
        /// <returns>指定 CnCNet 游戏的索引。如果游戏未知或不受支持则返回 -1。</returns>
        public int GetGameIndexFromInternalName(string gameName)
        {
            for (int gId = 0; gId < GameList.Count; gId++)
            {
                CnCNetGame game = GameList[gId];

                if (gameName.ToLowerInvariant() == game.InternalName)
                    return gId;
            }

            return -1;
        }

        /// <summary>
        /// 在支持的游戏列表中查找特定游戏的内部名称，如果找到则返回游戏的完整名称。
        /// 否则返回参数中指定的内部名称。
        /// </summary>
        /// <param name="gameName">要查找的游戏内部名称。</param>
        /// <returns>基于内部名称的支持游戏的完整名称。如果在支持的游戏列表中未找到该名称则返回给定的参数。</returns>
        public string GetGameNameFromInternalName(string gameName)
        {
            CnCNetGame game = GameList.Find(g => g.InternalName == gameName.ToLowerInvariant());

            if (game == null)
                return gameName;

            return game.UIName;
        }

        /// <summary>
        /// 根据游戏在列表中的索引返回游戏的完整 UI 名称。
        /// </summary>
        /// <param name="gameIndex">CnCNet 支持游戏的索引。</param>
        /// <returns>游戏的 UI 名称。</returns>
        public string GetFullGameNameFromIndex(int gameIndex)
        {
            return GameList[gameIndex].UIName;
        }

        /// <summary>
        /// 根据游戏在列表中的索引返回游戏的内部名称。
        /// </summary>
        /// <param name="gameIndex">CnCNet 支持游戏的索引。</param>
        /// <returns>游戏的内部名称（后缀）。</returns>
        public string GetGameIdentifierFromIndex(int gameIndex)
        {
            return GameList[gameIndex].InternalName;
        }

        public string GetGameBroadcastingChannelNameFromIdentifier(string gameIdentifier)
        {
            CnCNetGame game = GameList.Find(g => g.InternalName == gameIdentifier.ToLowerInvariant());
            if (game == null)
                return null;
            return game.GameBroadcastChannel;
        }

        public string GetGameChatChannelNameFromIdentifier(string gameIdentifier)
        {
            CnCNetGame game = GameList.Find(g => g.InternalName == gameIdentifier.ToLowerInvariant());
            if (game == null)
                return null;
            return game.ChatChannel;
        }
    }

    /// <summary>
    /// 当要添加到游戏集合的游戏配置包含无效或意外的设置/数据，或缺少必需的设置/数据时抛出的异常。
    /// </summary>
    class GameCollectionConfigurationException : Exception
    {
        public GameCollectionConfigurationException(string message) : base(message)
        {
        }
    }
}