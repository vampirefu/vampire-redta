using System;
using System.IO;

namespace ClientCore.Statistics
{
    public class PlayerStatistics
    {
        public PlayerStatistics() { }

        public PlayerStatistics(string name, bool isLocal, bool isAi, bool isSpectator, 
            int side, int team, int color, int aiLevel)
        {
            Name = name;
            IsLocalPlayer = isLocal;
            IsAI = isAi;
            WasSpectator = isSpectator;
            Side = side;
            Team = team;
            Color = color;
            AILevel = aiLevel;
        }

        public string Name { get; set; }
        public int Kills { get; set; }
        public int Losses {get; set;}
        public int Economy { get; set; }
        public int Score { get; set; }
        public int Side { get; set; }
        public int Team { get; set; }
        public int AILevel { get; set; }
        public bool SawEnd { get; set; }
        public bool WasSpectator { get; set; }
        public bool Won { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsAI { get; set; }
        public int Color { get; set; } = 255;

        public void Write(Stream stream)
        {
            stream.WriteInt(Economy);
            // IsAI 占1个字节
            stream.WriteBool(IsAI);
            // IsLocalPlayer 占1个字节
            stream.WriteBool(IsLocalPlayer);
            // 击杀数占4个字节
            stream.Write(BitConverter.GetBytes(Kills), 0, 4);
            // 损失数占4个字节
            stream.Write(BitConverter.GetBytes(Losses), 0, 4);
            // 名称占32个字节
            stream.WriteString(Name, 32);
            // SawEnd 占1个字节
            stream.WriteBool(SawEnd);
            // 分数占4个字节
            stream.WriteInt(Score);
            // 阵营占1个字节
            stream.WriteByte(Convert.ToByte(Side));
            // 队伍占1个字节
            stream.WriteByte(Convert.ToByte(Team));
            // 颜色占1个字节
            stream.WriteByte(Convert.ToByte(Color));
            // WasSpectator 占1个字节
            stream.WriteBool(WasSpectator);
            // Won 占1个字节
            stream.WriteBool(Won);
            // AI 等级占1个字节
            stream.WriteByte(Convert.ToByte(AILevel));
        }
    }
}
