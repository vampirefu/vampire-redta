using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using Rampastring.Tools;

namespace DTAClient.Domain;
public class AI
{
    public string Dir { get; set; }
    public string DisplayName { get; set; }
    public string Name { get; set; }

    public static List<AI> GetAIs()
    {
        List<AI> ais = new List<AI>();

        string iniDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "INI");
        IniFile aiIni = new IniFile(SafePath.CombineFilePath(iniDir, "AI.ini"));
        string section = "AI";
        if (aiIni.GetSection(section) == null)
            return ais;

        foreach (KeyValuePair<string, string> keyValuePair in aiIni.GetSection(section).Keys)
        {
            if (!aiIni.GetBooleanValue(keyValuePair.Value, "Visible", true))
                continue;

            AI ai = new AI();
            ai.Name = keyValuePair.Value;
            if (string.IsNullOrEmpty(aiIni.GetStringValue(keyValuePair.Value, "Dir", string.Empty)))
            {
                ai.Dir = $"INI/Game Options/{section}/{keyValuePair.Value}";
            }
            else
            {
                ai.Dir = aiIni.GetStringValue(keyValuePair.Value, "Dir", string.Empty);
            }
            ai.DisplayName = aiIni.GetStringValue(keyValuePair.Value, "Text", keyValuePair.Value);

            ais.Add(ai);
        }


        return ais;
    }
}
