using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using Rampastring.Tools;

namespace DTAClient.DXGUI.Helpers;
internal class CNCMapsRendererHelper
{
    private const int Resolution = 560;

    public static string CreatePreviewPng(string mapPath)
    {
        string mapName = Path.GetFileNameWithoutExtension(mapPath);

        //string cmdStr = $"/c cd /d \"{ProgramConstants.GamePath}Resources\\RandomMapGenerator_RA2\\Map Renderer\" && CNCMaps.Renderer.exe -i \"{mapPath}\" -o {mapName} -m \"{ProgramConstants.GamePath.TrimEnd('\\')}\" -Y -z +(1280,0) --thumb-png --bkp ";
        string cmdStr = $"/c cd /d \"{ProgramConstants.GamePath}Resources\\RandomMapGenerator_RA2\\Map Renderer\" && CNCMaps.Renderer.exe -i \"{mapPath}\" -o {mapName} -m \"{ProgramConstants.GamePath.TrimEnd('\\')}\" -Y -z +({Resolution},0) --thumb-png --bkp ";

        Process process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = cmdStr;
        process.StartInfo.UseShellExecute = false;   //是否使用操作系统shell启动 
        process.StartInfo.CreateNoWindow = true;   //是否在新窗口中启动该进程的值 (不显示程序窗口)
        process.Start();
        process.WaitForExit();  //等待程序执行完退出进程
        process.Close();

        string mapDir = Path.GetDirectoryName(mapPath);
        FileInfo fileInfo = new FileInfo(SafePath.CombineFilePath(mapDir, $"thumb_{mapName}.png"));
        string newMapPreviewPath = "";
        if (fileInfo.Exists)
        {
            newMapPreviewPath = SafePath.CombineFilePath(mapDir, $"{mapName}.png");
            try
            {
                fileInfo.MoveTo(newMapPreviewPath);
            }
            catch (Exception)
            {

            }
        }

        return newMapPreviewPath;
    }
}
