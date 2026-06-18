using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using Rampastring.Tools;

namespace DTAClient.DXGUI.Helpers;

internal class ShowKratosHelper
{
    private const string kratosDll = "Kratos.dll";

    public static void ApplyKratosDisplay(GameLobbyCheckBox kratosDisplay)
    {
        if (kratosDisplay.Name != "chkBloodDisplay")
            return;

        string bloodDisplaySettingDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "INI", "Game Options", "Kratos");

        string originPhobosPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "Kratos.dll");
        if (File.Exists(originPhobosPath))
        {
            try
            {
                File.Delete(originPhobosPath);
            }
            catch (Exception)
            {
            }
        }

        if (kratosDisplay.Checked)
        {
            string newPhobosPath = Path.Combine(bloodDisplaySettingDir, kratosDll);
            var file = new FileInfo(newPhobosPath);
            if (file.Exists)
                file.CopyTo(originPhobosPath);
        }
    }
}
