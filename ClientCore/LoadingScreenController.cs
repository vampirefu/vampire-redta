using System;
using Rampastring.Tools;

namespace ClientCore
{
    public static class LoadingScreenController
    {
        public static string GetLoadScreenName(string sideId)
        {
            //int resHeight = UserINISettings.Instance.IngameScreenHeight;

            //string loadingScreenName = ProgramConstants.BASE_RESOURCE_PATH + "l";

            //if (resHeight < 480)
            //    loadingScreenName += "400";
            //else if (resHeight < 600)
            //    loadingScreenName += "480";
            //else
            //    loadingScreenName += "600";

            //loadingScreenName = loadingScreenName + "s" + sideId;
            //Random random = new Random();
            //int randomInt = random.Next(1, 1 + ClientConfiguration.Instance.LoadingScreenCount);

            ////return loadingScreenName + Convert.ToString(randomInt) + ".pcx";
            return "c01a.pcx";
            //// return "Maps\\Campaign\\JYD\\ls800a01.shp";


            int resHeight = UserINISettings.Instance.IngameScreenHeight;
            int randomInt = new Random().Next(1, 1 + ClientConfiguration.Instance.LoadingScreenCount);
            string resolutionText;

            if (resHeight < 480)
                resolutionText = "400";
            else if (resHeight < 600)
                resolutionText = "480";
            else
                resolutionText = "600";

            return SafePath.CombineFilePath(
                ProgramConstants.BASE_RESOURCE_PATH,
                FormattableString.Invariant($"l{resolutionText}s{sideId}{randomInt}.pcx")).Replace('\\', '/');
        }
    }
}
