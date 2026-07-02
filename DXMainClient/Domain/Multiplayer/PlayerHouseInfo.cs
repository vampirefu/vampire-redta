using System;
using System.Collections.Generic;
using ClientCore;

namespace DTAClient.Domain.Multiplayer
{
    public class PlayerHouseInfo
    {
        public int SideIndex { get; set; }

        /// <summary>
        /// 在游戏规则文件中使用的阵营（或更准确地说，根据游戏不同称为House或Country）索引。
        /// </summary>
        public int InternalSideIndex
        {
            get
            {
                if (IsSpectator && !string.IsNullOrEmpty(ClientConfiguration.Instance.SpectatorInternalSideIndex))
                    return int.Parse(ClientConfiguration.Instance.SpectatorInternalSideIndex);
                
                if (!string.IsNullOrEmpty(ClientConfiguration.Instance.InternalSideIndices))
                    return Array.ConvertAll(ClientConfiguration.Instance.InternalSideIndices.Split(','), int.Parse)[SideIndex];

                return SideIndex;
            }
        }
        public int ColorIndex { get; set; }
        public int StartingWaypoint { get; set; }

        public int RealStartingWaypoint { get; set; }

        public bool IsSpectator { get; set; }

        /// <summary>
        /// 将玩家的阵营应用到信息中，必要时进行随机化。
        /// </summary>
        /// <param name="pInfo">玩家的PlayerInfo。</param>
        /// <param name="sideCount">游戏中的阵营数量。</param>
        /// <param name="random">随机数生成器。</param>
        /// <param name="disallowedSideArray">确定哪些阵营索引被游戏选项禁止的布尔数组。</param>
        public void RandomizeSide(PlayerInfo pInfo, int sideCount, Random random,
            bool[] disallowedSideArray, List<int[]> randomSelectors, int randomCount)
        {
            if (pInfo.SideId == 0 || pInfo.SideId == sideCount + randomCount)
            {
                // 玩家选择了随机或观战

                int sideId;

                do sideId = random.Next(0, sideCount);
                while (disallowedSideArray[sideId]);

                SideIndex = sideId;
            }
            else
            {
                // 使用自定义随机选择器。
                if (pInfo.SideId < randomCount)
                {
                    int[] randomsides = randomSelectors[pInfo.SideId - 1];
                    int count = randomsides.Length;
                    int sideId;
                    
                    do sideId = randomsides[random.Next(0, count)];
                    while (disallowedSideArray[sideId]);

                    SideIndex = sideId;
                }
                else SideIndex = pInfo.SideId - randomCount; // 玩家选择了一个阵营
            }
        }

        /// <summary>
        /// 将玩家的颜色应用到信息中，必要时进行随机化。如果颜色被随机化，则从可用颜色列表中移除。
        /// </summary>
        /// <param name="pInfo">玩家的PlayerInfo。</param>
        /// <param name="freeColors">可用（未使用）颜色列表。</param>
        /// <param name="mpColors">所有多人游戏颜色列表。</param>
        /// <param name="random">随机数生成器。</param>
        public void RandomizeColor(PlayerInfo pInfo, List<int> freeColors, 
            List<MultiplayerColor> mpColors, Random random)
        {
            if (pInfo.ColorId == 0)
            {
                // 玩家选择了随机颜色

                int randomizedColorIndex = random.Next(0, freeColors.Count);
                int actualColorId = freeColors[randomizedColorIndex];

                ColorIndex = mpColors[actualColorId].GameColorIndex;
                freeColors.RemoveAt(randomizedColorIndex);
            }
            else
            {
                ColorIndex = mpColors[pInfo.ColorId - 1].GameColorIndex;
                freeColors.Remove(pInfo.ColorId - 1);
            }
        }

        /// <summary>
        /// 将玩家的起始位置应用到信息中，必要时进行随机化。如果起始位置被随机化，则从可用起始位置列表中移除。
        /// </summary>
        /// <param name="pInfo">玩家的PlayerInfo。</param>
        /// <param name="freeStartingLocations">空闲起始位置列表。</param>
        /// <param name="random">随机数生成器。</param>
        /// <param name="takenStartingLocations">已被占用的起始位置列表。</param>
        /// <param name="overrideGameRandomLocations"></param>
        /// <returns>如果玩家的起始位置索引超过地图的起始路径点数量则返回true，否则返回false。</returns>
        public void RandomizeStart(
            PlayerInfo pInfo, 
            Random random,
            List<int> freeStartingLocations, 
            List<int> takenStartingLocations,
            bool overrideGameRandomLocations
        )
        {
            overrideGameRandomLocations |= ClientConfiguration.Instance.UseClientRandomStartLocations;
            if (IsSpectator)
            {
                StartingWaypoint = 90;
                return;
            }

            if (pInfo.StartingLocation == 0)
            {
                // 随机化起始位置

                if (!overrideGameRandomLocations)
                {
                    // 游戏使用自己的随机化逻辑，将随机玩家放在地图的另一侧。
                    // 玩家似乎更喜欢这种行为，所以使用-1将起始位置的随机化交给游戏本身
                    RealStartingWaypoint = -1;
                    StartingWaypoint = -1;
                    return;
                }

                // 让客户端选择起始位置。
                if (freeStartingLocations.Count == 0) // 没有可用的空闲起始位置
                {
                    RealStartingWaypoint = -1;
                    StartingWaypoint = -1;
                    return;
                }

                int waypointIndex = random.Next(0, freeStartingLocations.Count);
                RealStartingWaypoint = freeStartingLocations[waypointIndex];
                StartingWaypoint = RealStartingWaypoint;
                freeStartingLocations.Remove(StartingWaypoint);
                return;
            }

            // 使用玩家选择的起始位置
            RealStartingWaypoint = pInfo.StartingLocation - 1;

            if (takenStartingLocations.Contains(RealStartingWaypoint))
            {
                StartingWaypoint = -1; // 未知起始位置，与另一玩家重叠
                return;
            }

            takenStartingLocations.Add(RealStartingWaypoint);

            StartingWaypoint = RealStartingWaypoint;
        }
    }
}
