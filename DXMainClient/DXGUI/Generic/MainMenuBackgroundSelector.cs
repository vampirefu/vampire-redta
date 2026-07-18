using ClientCore;
using Rampastring.Tools;
using System;
using System.IO;

namespace DTAClient.DXGUI.Generic
{
    public static class MainMenuBackgroundSelector
    {
        public const string GifBackgroundPath = "MainMenu/mainmenubg.gif";
        public const string PngBackgroundPath = "MainMenu/mainmenubg.png";

        public static string Select(Func<string, bool> assetExists)
        {
            if (assetExists == null)
                throw new ArgumentNullException(nameof(assetExists));

            return assetExists(GifBackgroundPath) ? GifBackgroundPath : PngBackgroundPath;
        }

        /// <summary>
        /// 返回 GIF 背景的绝对路径。优先主题目录，其次基础资源目录。
        /// 不存在时返回 null。
        /// </summary>
        public static string GetGifBackgroundAbsolutePath()
        {
            FileInfo themeFile = SafePath.GetFile(ProgramConstants.GetResourcePath(), GifBackgroundPath);
            if (themeFile.Exists)
                return themeFile.FullName;

            FileInfo baseFile = SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), GifBackgroundPath);
            if (baseFile.Exists)
                return baseFile.FullName;

            return null;
        }
    }
}
