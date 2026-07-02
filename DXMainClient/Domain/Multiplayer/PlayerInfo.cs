using Rampastring.Tools;
using System;

namespace DTAClient.Domain.Multiplayer
{
    /// <summary>
    /// 游戏大厅中的玩家。
    /// </summary>
    public class PlayerInfo
    {
        public PlayerInfo() { }

        public PlayerInfo(string name)
        {
            Name = name;
        }

        public PlayerInfo(string name, int sideId, int startingLocation, int colorId, int teamId)
        {
            Name = name;
            SideId = sideId;
            StartingLocation = startingLocation;
            ColorId = colorId;
            TeamId = teamId;
        }

        public string Name { get; set; }
        public int SideId { get; set; }
        public int StartingLocation { get; set; }
        public int ColorId { get; set; }
        public int TeamId { get; set; }
        public bool Ready { get; set; }
        public bool AutoReady { get; set; }
        public bool IsAI { get; set; }

        public bool IsInGame { get; set; }
        public virtual string IPAddress { get; set; } = "0.0.0.0";
        public int Port { get; set; }
        public bool Verified { get; set; }

        public int Index { get; set; }

        public int Ping { get; set; } = -1;

        /// <summary>
        /// AI玩家在客户端内部的难度等级。
        /// 逻辑递增标度，如原版泰伯利亚之日UI。
        /// 2=困难，1=中等，0=简单。
        /// </summary>
        public int AILevel { get; set; }

        /// <summary>
        /// AI在spawn.ini中[HouseHandicaps]节的AI等级。
        /// 2=简单，1=中等，0=困难。
        /// </summary>
        public int HouseHandicapAILevel
        {
            get { return Math.Abs(AILevel - 2); }
        }

        public override string ToString()
        {
            var sb = new ExtendedStringBuilder(true, ',');
            sb.Append(Name);
            sb.Append(SideId);
            sb.Append(StartingLocation);
            sb.Append(ColorId);
            sb.Append(TeamId);
            sb.Append(AILevel);
            sb.Append(IsAI.ToString());
            sb.Append(Index);
            return sb.ToString();
        }

        /// <summary>
        /// 从匹配ToString()方法格式的字符串创建PlayerInfo实例。
        /// </summary>
        /// <param name="str">字符串。</param>
        /// <returns>PlayerInfo实例，如果字符串格式无效则返回null。</returns>
        public static PlayerInfo FromString(string str)
        {
            var values = str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (values.Length != 8)
                return null;

            var pInfo = new PlayerInfo();

            pInfo.Name = values[0];
            pInfo.SideId = Conversions.IntFromString(values[1], 0);
            pInfo.StartingLocation = Conversions.IntFromString(values[2], 0);
            pInfo.ColorId = Conversions.IntFromString(values[3], 0);
            pInfo.TeamId = Conversions.IntFromString(values[4], 0);
            pInfo.AILevel = Conversions.IntFromString(values[5], 0);
            pInfo.IsAI = Conversions.BooleanFromString(values[6], true);
            pInfo.Index = Conversions.IntFromString(values[7], 0);

            return pInfo;
        }
    }
}
