using System;

namespace DTAClient.Online.EventArguments
{
    public class FavoriteMapEventArgs : EventArgs
    {
        public readonly string MapName;

        public FavoriteMapEventArgs(string mapName)
        {
            MapName = mapName;
        }
    }
}