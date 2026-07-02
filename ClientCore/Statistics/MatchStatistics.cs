using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClientCore.Statistics.GameParsers;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    public class MatchStatistics
    {
        public MatchStatistics() { }

        public MatchStatistics(string gameVersion, int gameId, string mapName, string gameMode, int numHumans, bool mapIsCoop = false)
        {
            GameVersion = gameVersion;
            GameID = gameId;
            DateAndTime = DateTime.Now;
            MapName = mapName;
            GameMode = gameMode;
            NumberOfHumanPlayers = numHumans;
            MapIsCoop = mapIsCoop;
        }

        public List<PlayerStatistics> Players = new List<PlayerStatistics>();

        public int LengthInSeconds { get; set; }

        public DateTime DateAndTime { get; set; }

        public string GameVersion { get; set; }

        public string MapName { get; set; }

        public string GameMode { get; set; }

        public bool SawCompletion { get; set; }

        public int NumberOfHumanPlayers { get; set; }

        public int AverageFPS { get; set; }

        public int GameID { get; set; }

        public bool MapIsCoop { get; set; }

        public bool IsValidForStar { get; set; } = true;

        public void AddPlayer(string name, bool isLocal, bool isAI, bool isSpectator,
            int side, int team, int color, int aiLevel)
        {
            PlayerStatistics ps = new PlayerStatistics(name, isLocal, isAI, isSpectator, 
                side, team, color, aiLevel);
            Players.Add(ps);
        }

        public void AddPlayer(PlayerStatistics ps)
        {
            Players.Add(ps);
        }

        public void ParseStatistics(string gamePath, string gameName, bool isLoadedGame)
        {
            Logger.Log("Parsing game statistics.");

            LengthInSeconds = (int)(DateTime.Now - DateAndTime).TotalSeconds;

            var parser = new LogFileStatisticsParser(this, isLoadedGame);
            parser.ParseStats(gamePath, ClientConfiguration.Instance.StatisticsLogFileName);
        }

        public PlayerStatistics GetEmptyPlayerByName(string playerName)
        {
            foreach (PlayerStatistics ps in Players)
            {
                if (ps.Name == playerName && ps.Losses == 0 && ps.Score == 0)
                    return ps;
            }

            return null;
        }

        public PlayerStatistics GetFirstEmptyPlayer()
        {
            foreach (PlayerStatistics ps in Players)
            {
                if (ps.Losses == 0 && ps.Score == 0)
                    return ps;
            }

            return null;
        }

        public int GetPlayerCount()
        {
            return Players.Count;
        }

        public PlayerStatistics GetPlayer(int index)
        {
            return Players[index];
        }

        public void Write(Stream stream)
        {
            // 游戏时长
            stream.WriteInt(LengthInSeconds);

            // 游戏版本，8字节，ASCII
            stream.WriteString(GameVersion, 8, Encoding.ASCII);

            // 日期和时间，8字节
            stream.WriteLong(DateAndTime.ToBinary());
            // SawCompletion，1字节
            stream.WriteBool(SawCompletion);
            // 玩家数量，1字节
            stream.WriteByte(Convert.ToByte(GetPlayerCount()));
            // 平均 FPS，4字节
            stream.WriteInt(AverageFPS);
            // 地图名称，128字节（64字符），Unicode
            stream.WriteString(MapName, 128);
            // 游戏模式，64字节（32字符），Unicode
            stream.WriteString(GameMode, 64);
            // 唯一游戏ID，4字节
            stream.WriteInt(GameID);
            // 游戏选项是否满足获得星星的条件，1字节
            stream.WriteBool(IsValidForStar);

            // 写入玩家信息
            for (int i = 0; i < GetPlayerCount(); i++)
            {
                PlayerStatistics ps = GetPlayer(i);
                ps.Write(stream);
            }
        }
    }
}
