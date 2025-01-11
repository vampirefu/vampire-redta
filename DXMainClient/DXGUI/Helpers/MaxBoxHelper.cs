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
public class MaxBoxHelper
{
    private const string PhobosLast = "Phobos_last.dll";
    private const string Phobos27 = "Phobos_27.dll";

    public static void ApplyMaxBox(GameLobbyCheckBox maxBox)
    {
        if (maxBox.Name != "chkMaxBox")
            return;

        string settingDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "INI", "Game Options", "MaxBox");

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

        string newPhobosPath = Path.Combine(settingDir, maxBox.Checked ? PhobosLast : Phobos27);
        var file = new FileInfo(newPhobosPath);
        if (file.Exists)
            file.CopyTo(originPhobosPath);
    }
}
