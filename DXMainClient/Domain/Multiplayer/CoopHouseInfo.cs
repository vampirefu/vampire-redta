namespace DTAClient.Domain.Multiplayer
{
    /// <summary>
    /// 保存合作地图中敌方阵营的信息。
    /// </summary>
    public struct CoopHouseInfo
    {
        public CoopHouseInfo(int side, int color, int startingLocation)
        {
            Side = side;
            Color = color;
            StartingLocation = startingLocation;
        }

        /// <summary>
        /// 敌方阵营的阵营索引。
        /// </summary>
        public int Side;

        /// <summary>
        /// 敌方阵营的颜色索引。
        /// </summary>
        public int Color;

        /// <summary>
        /// 敌方阵营的起始位置路径点。
        /// </summary>
        public int StartingLocation;
    }
}
