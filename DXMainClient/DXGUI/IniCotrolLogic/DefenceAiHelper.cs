using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using Rampastring.Tools;
using SharpDX.Direct2D1;

namespace DTAClient.DXGUI.IniCotrolLogic;
internal class DefenceAiHelper
{
    private const string AITaskSection = "TaskForces";
    private const string AIActionsSection = "Actions";
    private const string AITriggerSection = "AITriggerTypesEnable";

    public static bool IsShowCKH(string mapPath)
    {
        IniFile ini = new IniFile(mapPath);
        List<string> keys = ini.GetSectionKeys(AITaskSection);

        if (keys?.Count > 0)
        {
            keys = ini.GetSectionKeys(AIActionsSection);
            if (keys == null)
                return false;

            Regex regex = new Regex(@"14,0,\d+,0,0,0,0,A");
            foreach (var key in keys)
            {
                string value = ini.GetStringValue(AIActionsSection, key, "");
                if (regex.IsMatch(value))
                    return true;
            }
        }
        return false;
    }

    public static void SetAITriggerEnable(string mapPath, GameLobbyCheckBox defenceAiTrigger)
    {
        if (defenceAiTrigger.Name != "DefenceAiTrigger")
            return;

        if (!File.Exists(mapPath))
            return;

        IniFile ini = new IniFile(mapPath);
        List<string> keys = ini.GetSectionKeys(AITaskSection);
        foreach (string key in keys)
        {
            ini.SetBooleanValue(AITriggerSection, key, !defenceAiTrigger.Checked);
        }
    }
}
