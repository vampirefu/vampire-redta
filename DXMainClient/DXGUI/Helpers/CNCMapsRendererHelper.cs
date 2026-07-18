using System;
using System.Diagnostics;
using System.IO;
using ClientCore;
using Rampastring.Tools;
using MapGenerator.Core;

namespace DTAClient.DXGUI.Helpers;

internal class CNCMapsRendererHelper
{
    private const int Resolution = 760;

    public static string CreatePreviewPng(string mapPath)
    {
        if (string.IsNullOrEmpty(mapPath))
            return string.Empty;

        string mapName = Path.GetFileNameWithoutExtension(mapPath);
        string mapDir = Path.GetDirectoryName(mapPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        string rendererPath = Path.Combine(ProgramConstants.GamePath, "Resources", "RandomMapGenerator_RA2", "Map Renderer");

        try
        {
            MapThumbnailRenderer renderer = new MapThumbnailRenderer(rendererPath, ProgramConstants.GamePath);
            renderer.SetOutputPath(mapDir);
            renderer.RenderThumbnail(mapPath, mapName);

            string newMapPreviewPath = SafePath.CombineFilePath(mapDir, $"{mapName}.png");
            if (File.Exists(newMapPreviewPath))
                return newMapPreviewPath;

            // 回退：如果渲染器生成了 thumb_ 文件
            string thumbPath = SafePath.CombineFilePath(mapDir, $"thumb_{mapName}.png");
            if (File.Exists(thumbPath))
            {
                string finalPath = SafePath.CombineFilePath(mapDir, $"{mapName}.png");
                try
                {
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);
                    File.Move(thumbPath, finalPath);
                    return finalPath;
                }
                catch
                {
                    // 忽略移动错误
                }
            }
        }
        catch (Exception)
        {
            // 吞掉异常并返回空值
        }

        return string.Empty;
    }
}
