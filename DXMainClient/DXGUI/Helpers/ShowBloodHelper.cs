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
internal class ShowBloodHelper
{
    private const string BloodDisplay = "Phobos_ShowBlood.dll";
    private const string UnBloodDisplay = "Phobos_UnShow.dll";

    public static void ApplyBloodDisplay(GameLobbyCheckBox bloodDisplay)
    {
        if (bloodDisplay.Name != "chkBloodDisplay")
            return;

        string bloodDisplaySettingDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "INI", "Game Options", "BloodDisplay");

        string originPhobosPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "Phobos.dll");
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


        string newPhobosPath = Path.Combine(bloodDisplaySettingDir, bloodDisplay.Checked ? BloodDisplay : UnBloodDisplay);
        var file = new FileInfo(newPhobosPath);
        if (file.Exists)
            file.CopyTo(originPhobosPath);
    }
}
