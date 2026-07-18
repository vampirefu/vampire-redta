using ClientCore;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using System;
using System.Collections.Generic;

namespace DTAClient.Domain.Multiplayer
{
    /// <summary>
    /// 多人游戏大厅的颜色。
    /// </summary>
    public class MultiplayerColor
    {
        public int GameColorIndex { get; private set; }
        public string Name { get; private set; }
        public Color XnaColor { get; private set; }

        private static List<MultiplayerColor> colorList;

        /// <summary>
        /// 从字符串数组中的数据创建新的多人游戏颜色。
        /// </summary>
        /// <param name="name">颜色名称。</param>
        /// <param name="data">输入数据。需要为R,G,B,(游戏颜色索引)格式。</param>
        /// <returns>从给定字符串数组创建的新多人游戏颜色。</returns>
        public static MultiplayerColor CreateFromStringArray(string name, string[] data)
        {
            return new MultiplayerColor()
            {
                Name = name,
                XnaColor = new Color(Math.Min(255, Int32.Parse(data[0])),
                Math.Min(255, Int32.Parse(data[1])),
                Math.Min(255, Int32.Parse(data[2])), 255),
                GameColorIndex = Int32.Parse(data[3])
            };
        }

        /// <summary>
        /// 返回可用的多人游戏颜色。
        /// </summary>
        public static List<MultiplayerColor> LoadColors()
        {
            if (colorList != null)
                return new List<MultiplayerColor>(colorList);

            IniFile gameOptionsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "GameOptions.ini"));

            List<MultiplayerColor> mpColors = new List<MultiplayerColor>();

            List<string> colorKeys = gameOptionsIni.GetSectionKeys("MPColors");

            if (colorKeys == null)
                throw new ClientConfigurationException("[MPColors] not found in GameOptions.ini!");

            foreach (string key in colorKeys)
            {
                string[] values = gameOptionsIni.GetStringValue("MPColors", key, "255,255,255,0").Split(',');

                try
                {
                    MultiplayerColor mpColor = MultiplayerColor.CreateFromStringArray(key, values);

                    mpColors.Add(mpColor);
                }
                catch
                {
                    throw new ClientConfigurationException("GameOptions.ini中指定了无效的MPColor: " + key);
                }
            }

            colorList = mpColors;
            return new List<MultiplayerColor>(colorList);
        }
    }
}
