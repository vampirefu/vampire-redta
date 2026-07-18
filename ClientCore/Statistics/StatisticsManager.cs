using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    public class StatisticsManager : GenericStatisticsManager
    {
        private const string VERSION = "1.06";
        private const string SCORE_FILE_PATH = "Client/dscore.dat";
        private const string OLD_SCORE_FILE_PATH = "dscore.dat";
        private static StatisticsManager _instance;

        public event EventHandler GameAdded;


        public static StatisticsManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new StatisticsManager();
                return _instance;
            }
        }

        public override void ReadStatistics(string gamePath)
        {
            

            FileInfo scoreFileInfo = SafePath.GetFile(gamePath, SCORE_FILE_PATH);

            if (!scoreFileInfo.Exists)
            {
                Logger.Log("Skipping reading statistics because the file doesn't exist!");
                return;
            }

            Logger.Log("Reading statistics.");

            Statistics.Clear();

            FileInfo oldScoreFileInfo = SafePath.GetFile(gamePath, OLD_SCORE_FILE_PATH);
            bool resave = ReadFile(oldScoreFileInfo.FullName);
            bool resaveNew = ReadFile(scoreFileInfo.FullName);

            PurgeStats();

            if (resave || resaveNew)
            {
                if (oldScoreFileInfo.Exists)
                {
                    File.Copy(oldScoreFileInfo.FullName, SafePath.CombineFilePath(ProgramConstants.ClientUserFilesPath, "dscore_old.dat"));
                    SafePath.DeleteFileIfExists(oldScoreFileInfo.FullName);
                }

                SaveDatabase();
            }
            
        }

        /// <summary>
        /// 读取统计文件。
        /// </summary>
        /// <param name="filePath">统计文件的路径。</param>
        /// <returns>确定是否需要重新保存数据库的布尔值。</returns>
        private bool ReadFile(string filePath)
        {
            

            bool returnValue = false;

            try
            {
                string databaseVersion = GetStatDatabaseVersion(filePath);

                if (databaseVersion == null)
                    return false; // 不存在分数数据库

             //   databaseVersion = "1.06";

                switch (databaseVersion)
                {
                    case "1.00":
                    case "1.01":
                        ReadDatabase(filePath, 0);
                        returnValue = true;
                        break;
                    case "1.02":
                        ReadDatabase(filePath, 2);
                        returnValue = true;
                        break;
                    case "1.03":
                        ReadDatabase(filePath, 3);
                        returnValue = true;
                        break;
                    case "1.04":
                        ReadDatabase(filePath, 4);
                        returnValue = true;
                        break;
                    case "1.05":
                        ReadDatabase(filePath, 5);
                        returnValue = true;
                        break;
                    case "1.06":
                        ReadDatabase(filePath, 6);
                        break;
                    default:
                        throw new InvalidDataException("Invalid version for " + filePath + ": " + databaseVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error reading statistics: " + ex.Message);
            }

            return returnValue;
        }

        private void ReadDatabase(string filePath, int version)
        {
            // TODO 使用 MatchStatistics 和 PlayerStatistics 类拆分此函数

            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                   
                    fs.Position = 4; // 跳过版本号
                    byte[] readBuffer = new byte[128];
                    fs.Read(readBuffer, 0, 4); // 版本号之后的4个字节表示游戏数量
                    int gameCount = BitConverter.ToInt32(readBuffer, 0);

                    for (int i = 0; i < gameCount; i++)
                    {
                        MatchStatistics ms = new MatchStatistics();

                        // 游戏信息的前4个字节是以秒为单位的时长
                        fs.Read(readBuffer, 0, 4);
                        int lengthInSeconds = BitConverter.ToInt32(readBuffer, 0);
                        ms.LengthInSeconds = lengthInSeconds;
                        // 接下来8个字节是游戏版本
                        fs.Read(readBuffer, 0, 8);
                        ms.GameVersion = System.Text.Encoding.ASCII.GetString(readBuffer, 0, 8);
                        // 接下来是日期和时间，也是8个字节
                        fs.Read(readBuffer, 0, 8);
                        long dateData = BitConverter.ToInt64(readBuffer, 0);
                        ms.DateAndTime = DateTime.FromBinary(dateData);
                        // 接下来1个字节是 SawCompletion
                        fs.Read(readBuffer, 0, 1);
                        ms.SawCompletion = Convert.ToBoolean(readBuffer[0]);
                        // 接下来1个字节是玩家数量
                        fs.Read(readBuffer, 0, 1);
                        int playerCount = readBuffer[0];
                        if (version > 0)
                        {
                            // 4个字节表示平均 FPS
                            fs.Read(readBuffer, 0, 4);
                            ms.AverageFPS = BitConverter.ToInt32(readBuffer, 0);
                        }

                        int mapNameLength = 64;

                        if (version > 3)
                        {
                            mapNameLength = 128;
                        }

                        // 地图名称，根据版本为64或128字节的 Unicode
                        fs.Read(readBuffer, 0, mapNameLength);
                        ms.MapName = Encoding.Unicode.GetString(readBuffer).Replace("\0", "");

                        // 游戏模式，64字节
                        fs.Read(readBuffer, 0, 64);
                        ms.GameMode = Encoding.Unicode.GetString(readBuffer, 0, 64).Replace("\0", "");

                        if (version > 2)
                        {
                            // 唯一游戏ID，4字节（int32）
                            fs.Read(readBuffer, 0, 4);
                            ms.GameID = BitConverter.ToInt32(readBuffer, 0);
                        }

                        if (version > 5)
                        {
                            fs.Read(readBuffer, 0, 1);
                            ms.IsValidForStar = Convert.ToBoolean(readBuffer[0]);
                        }

                        // 玩家信息紧跟在通用比赛信息之后
                        for (int j = 0; j < playerCount; j++)
                        {
                            PlayerStatistics ps = new PlayerStatistics();

                            if (version > 4)
                            {
                                // YR 中经济与建造统计共享
                                fs.Read(readBuffer, 0, 4);
                                ps.Economy = BitConverter.ToInt32(readBuffer, 0);
                            }
                            else
                            {
                                // 在旧版本中经济值在0到100之间，所以只占1个字节
                                fs.Read(readBuffer, 0, 1);
                                ps.Economy = readBuffer[0];
                            }

                            // IsAI 是布尔值，所以占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.IsAI = Convert.ToBoolean(readBuffer[0]);
                            // IsLocalPlayer 也是布尔值
                            fs.Read(readBuffer, 0, 1);
                            ps.IsLocalPlayer = Convert.ToBoolean(readBuffer[0]);
                            // 击杀数占4个字节
                            fs.Read(readBuffer, 0, 4);
                            ps.Kills = BitConverter.ToInt32(readBuffer, 0);
                            // 损失数也占4个字节
                            fs.Read(readBuffer, 0, 4);
                            ps.Losses = BitConverter.ToInt32(readBuffer, 0);
                            // 名称占32个字节
                            fs.Read(readBuffer, 0, 32);
                            ps.Name = System.Text.Encoding.Unicode.GetString(readBuffer, 0, 32);
                            ps.Name = ps.Name.Replace("\0", String.Empty);
                            // SawEnd 占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.SawEnd = Convert.ToBoolean(readBuffer[0]);
                            // 分数占4个字节
                            fs.Read(readBuffer, 0, 4);
                            ps.Score = BitConverter.ToInt32(readBuffer, 0);
                            // 阵营占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.Side = readBuffer[0];
                            // 队伍占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.Team = readBuffer[0];
                            if (version > 2)
                            {
                                // 颜色占1个字节
                                fs.Read(readBuffer, 0, 1);
                                ps.Color = readBuffer[0];
                            }
                            // WasSpectator 占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.WasSpectator = Convert.ToBoolean(readBuffer[0]);
                            // Won 占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.Won = Convert.ToBoolean(readBuffer[0]);
                            // AI 等级占1个字节
                            fs.Read(readBuffer, 0, 1);
                            ps.AILevel = readBuffer[0];

                            ms.AddPlayer(ps);

                            if (!ps.IsAI)
                                ms.NumberOfHumanPlayers++;
                        }

                        if (ms.Players.Find(p => p.IsLocalPlayer && !p.IsAI) == null)
                            continue;

                        
                        Statistics.Add(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Reading the statistics file failed! Message: " + ex.Message);
            }
        }

        public void PurgeStats()
        {
            int removedCount = 0;

            for (int i = 0; i < Statistics.Count; i++)
            {
                if (Statistics[i].LengthInSeconds < 60)
                {
                    Logger.Log("Removing match on " + Statistics[i].MapName + " because it's too short.");
                    Statistics.RemoveAt(i);
                    i--;
                    removedCount++;
                }
            }

            if (removedCount > 0)
                SaveDatabase();
        }

        public void ClearDatabase()
        {
            Statistics.Clear();
            CreateDummyFile();
        }

        public void AddMatchAndSaveDatabase(bool addMatch, MatchStatistics ms)
        {
            // 如果游戏只有一个玩家则跳过添加统计，但合作模式例外，因为它不会将预置的房屋识别为玩家
            if (ms.GetPlayerCount() <= 1 && !ms.MapIsCoop)
            {
                Logger.Log("Skipping adding match to statistics because game only had one player.");
                return;
            }

        

            if (addMatch)
            {
                Statistics.Add(ms);
                GameAdded?.Invoke(this, EventArgs.Empty);
            }

            FileInfo scoreFileInfo = SafePath.GetFile(ProgramConstants.GamePath, SCORE_FILE_PATH);

            if (!scoreFileInfo.Exists)
            {
                CreateDummyFile();
            }

            Logger.Log("Writing game info to statistics file.");

            using (FileStream fs = scoreFileInfo.Open(FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Position = 4; // 版本号之后的4个字节表示游戏数量
                fs.WriteInt(Statistics.Count);
                Logger.Log("11");
                fs.Position = fs.Length;
                ms.Write(fs);
            }

            Logger.Log("Finished writing statistics.");
        }

        private void CreateDummyFile()
        {
            Logger.Log("Creating empty statistics file.");

            using StreamWriter sw = new StreamWriter(SafePath.GetFile(ProgramConstants.GamePath, SCORE_FILE_PATH).Create());
            sw.Write(VERSION);
        }

        /// <summary>
        /// 删除文件系统上的统计文件并重新写入。
        /// </summary>
        public void SaveDatabase()
        {
            FileInfo scoreFileInfo = SafePath.GetFile(ProgramConstants.GamePath, SCORE_FILE_PATH);
            SafePath.DeleteFileIfExists(scoreFileInfo.FullName);
            CreateDummyFile();

            using (FileStream fs = scoreFileInfo.Open(FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Position = 4; // 版本号之后的4个字节表示游戏数量
                fs.WriteInt(Statistics.Count);

                foreach (MatchStatistics ms in Statistics)
                {
                    ms.Write(fs);
                }
            }
        }

        public bool HasBeatCoOpMap(string mapName, string gameMode)
        {
            List<MatchStatistics> matches = new List<MatchStatistics>();

            // 过滤掉不符合条件的比赛
            foreach (MatchStatistics ms in Statistics)
            {
                if (ms.SawCompletion &&
                    ms.MapName == mapName &&
                    ms.GameMode == gameMode)
                {
                    if (ms.Players[0].Won)
                        return true;
                }
            }

            return false;
        }

        public int GetCoopRankForDefaultMap(string mapName, int requiredPlayerCount)
        {
            List<MatchStatistics> matches = new List<MatchStatistics>();

            // 过滤掉不符合条件的比赛
            foreach (MatchStatistics ms in Statistics)
            {
                if (!ms.SawCompletion)
                    continue;

                if (!ms.IsValidForStar)
                    continue;

                if (ms.MapName != mapName)
                    continue;

                if (ms.Players.Count != requiredPlayerCount)
                    continue;

                if (ms.Players.Count(ps => !ps.IsAI && !ps.WasSpectator) > 1 &&
                    ms.Players.Find(ps => ps.IsAI) != null)
                    matches.Add(ms);
            }

            int rank = -1;

            foreach (MatchStatistics ms in matches)
            {
                rank = Math.Max(rank, GetRankForCoopMatch(ms));
            }

            return rank;
        }

        int GetRankForCoopMatch(MatchStatistics ms)
        {
            PlayerStatistics localPlayer = ms.Players.Find(p => p.IsLocalPlayer);

            if (localPlayer == null || !localPlayer.Won)
                return -1;

            if (ms.Players.Find(p => p.WasSpectator) != null)
                return -1; // 不允许有观战者的比赛

            if (ms.Players.Count(p => !p.IsAI && p.Team != localPlayer.Team) > 0)
                return -1; // 不允许有不同队伍的人类玩家的比赛

            if (ms.Players.Find(p => p.Team == 0) != null)
                return -1; // 有未结盟玩家的比赛被丢弃

            if (ms.Players.All(ps => ps.Team == localPlayer.Team))
                return -1; // 丢弃没有敌人的比赛

            int[] teamMemberCounts = new int[5];
            int lowestEnemyAILevel = 2;
            int highestAllyAILevel = 0;

            for (int i = 0; i < ms.Players.Count; i++)
            {
                PlayerStatistics ps = ms.GetPlayer(i);

                teamMemberCounts[ps.Team]++;

                if (!ps.IsAI)
                {
                    continue;
                }

                if (ps.Team > 0 && ps.Team == localPlayer.Team)
                {
                    if (ps.AILevel > highestAllyAILevel)
                        highestAllyAILevel = ps.AILevel;
                }
                else
                {
                    if (ps.AILevel < lowestEnemyAILevel)
                        lowestEnemyAILevel = ps.AILevel;
                }
            }

            if (lowestEnemyAILevel < highestAllyAILevel)
            {
                // 检查玩家的 AI 盟友是否没有更强
                return -1;
            }

            // 检查所有队伍的玩家数是否至少与本地玩家的队伍相同
            int allyCount = teamMemberCounts[localPlayer.Team];

            for (int i = 1; i < 5; i++)
            {
                if (i == localPlayer.Team)
                    continue;

                if (teamMemberCounts[i] > 0)
                {
                    if (teamMemberCounts[i] < allyCount)
                        return -1;
                }
            }

            return lowestEnemyAILevel;
        }

        public bool HasWonMapInPvP(string mapName, string gameMode, int requiredPlayerCount)
        {
            List<MatchStatistics> matches = new List<MatchStatistics>();

            foreach (MatchStatistics ms in Statistics)
            {
                if (!ms.SawCompletion)
                    continue;

                if (!ms.IsValidForStar)
                    continue;

                if (ms.MapName != mapName)
                    continue;

                if (ms.GameMode != gameMode)
                    continue;

                if (ms.Players.Count(ps => !ps.WasSpectator) != requiredPlayerCount)
                    continue;

                if (ms.Players.Find(ps => ps.IsAI) != null)
                    continue;

                PlayerStatistics localPlayer = ms.Players.Find(p => p.IsLocalPlayer);

                if (localPlayer == null)
                    continue;

                if (localPlayer.WasSpectator)
                    continue;

                if (!localPlayer.Won)
                    continue;

                int[] teamMemberCounts = new int[5];

                ms.Players.FindAll(ps => !ps.WasSpectator).ForEach(ps => teamMemberCounts[ps.Team]++);

                if (localPlayer.Team > 0)
                {
                    int lowestEnemyTeamMemberCount = int.MaxValue;

                    for (int i = 1; i < 5; i++)
                    {
                        if (i != localPlayer.Team && teamMemberCounts[i] > 0)
                        {
                            if (teamMemberCounts[i] < lowestEnemyTeamMemberCount)
                                lowestEnemyTeamMemberCount = teamMemberCounts[i];
                        }
                    }

                    if (lowestEnemyTeamMemberCount > teamMemberCounts[localPlayer.Team])
                        continue;

                    return true;
                }

                if (ms.Players.Count(ps => !ps.WasSpectator) > 1)
                    return true;
            }

            return false;
        }

        public int GetSkirmishRankForDefaultMap(string mapName, int requiredPlayerCount)
        {
            List<MatchStatistics> matches = new List<MatchStatistics>();

            // 过滤掉不符合条件的比赛
            foreach (MatchStatistics ms in Statistics)
            {


                if (ms.SawCompletion &&
                    ms.IsValidForStar &&
                    ms.MapName == mapName &&
                    ms.Players.Count == requiredPlayerCount &&
                    ms.Players.Count(p => !p.IsAI) == 1)
                    matches.Add(ms);

            }

            int rank = -1;

            foreach (MatchStatistics ms in matches)
            {

                // TODO 这段代码写得很丑陋，应该设计得更好

                PlayerStatistics localPlayer = ms.Players.Find(p => p.IsLocalPlayer);

                if (localPlayer == null || !localPlayer.Won)
                    continue;

                int[] teamMemberCounts = new int[5];
                int lowestEnemyAILevel = 2;
                int highestAllyAILevel = 0;


                for (int i = 0; i < ms.Players.Count; i++)
                {
                    PlayerStatistics ps = ms.GetPlayer(i);

                    teamMemberCounts[ps.Team]++;

                    if (ps.IsLocalPlayer)
                    {
                        continue;
                    }

                    if (ps.Team > 0 && ps.Team == localPlayer.Team)
                    {
                        if (ps.AILevel > highestAllyAILevel)
                            highestAllyAILevel = ps.AILevel;
                    }
                    else
                    {
                        if (ps.AILevel < lowestEnemyAILevel)
                            lowestEnemyAILevel = ps.AILevel;
                    }
                }

                //Logger.Log(lowestEnemyAILevel.ToString());
                //Logger.Log(highestAllyAILevel.ToString() );
                if (lowestEnemyAILevel < highestAllyAILevel)
                {
                    // 检查玩家的 AI 盟友是否没有更强
                    continue;
                }


                if (localPlayer.Team > 0)
                {
                    // 检查所有队伍的玩家数是否至少与人类玩家的队伍相同

                    int allyCount = teamMemberCounts[localPlayer.Team];
                    bool pass = true;

                    for (int i = 1; i < 5; i++)
                    {
                        if (i == localPlayer.Team)
                            continue;

                        if (teamMemberCounts[i] > 0)
                        {
                            if (teamMemberCounts[i] < allyCount)
                            {
                                // 敌方队伍的玩家数少于玩家的队伍
                                pass = false;
                                break;
                            }
                        }
                    }

                    if (!pass)
                        continue;

                    // 检查是否存在一个除了玩家所在队伍之外且规模至少相同的队伍
                    pass = false;
                    for (int i = 1; i < 5; i++)
                    {
                        if (i == localPlayer.Team)
                            continue;

                        if (teamMemberCounts[i] >= allyCount)
                        {
                            pass = true;
                            break;
                        }
                    }

                    if (!pass)
                        continue;
                }


                if (rank < lowestEnemyAILevel)
                {

                    rank = lowestEnemyAILevel;

                    if (rank == 2)
                        return rank; // 可能的最佳排名
                }
            }


            return rank;
        }

        public bool IsGameIdUnique(int gameId)
        {
            return Statistics.Find(m => m.GameID == gameId) == null;
        }

        public MatchStatistics GetMatchWithGameID(int gameId)
        {
            return Statistics.Find(m => m.GameID == gameId);
        }

    }
}
