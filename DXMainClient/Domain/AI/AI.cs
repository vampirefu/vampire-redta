using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using DTAClient.DXGUI.Helpers;
using Rampastring.Tools;

namespace DTAClient.Domain.AI;
public class AI
{
    /// <summary>
    /// 新AI路径
    /// </summary>
    public string Dir { get; set; }
    public string DisplayName { get; set; }
    public string Name { get; set; }

    public List<string> GetAllFiles()
    {
        var files = Directory.GetFiles(Dir, "*", SearchOption.AllDirectories).ToList();
        return files;
    }

    /// <summary>
    /// 备份原有AI并移动新AI
    /// </summary>
    public void Backup()
    {
        string detailBackupDir = SafePath.CombineDirectoryPath(AIConfig.AIBackupDir, Name);

        if (!Directory.Exists(detailBackupDir))
            Directory.CreateDirectory(detailBackupDir);

        if (!Directory.Exists(Dir))
            Directory.CreateDirectory(Dir);

        var files = GetAllFiles();
        List<string> replaceList = new List<string>();
        List<string> addList = new List<string>();
        foreach (var file in files)
        {
            string routePath = file.Replace(Dir, "");
            string targetPath = SafePath.CombineFilePath(ProgramConstants.GamePath, routePath);
            if (File.Exists(targetPath)) //移动至备份文件夹
            {
                string backupFilePath = SafePath.CombineFilePath(detailBackupDir, routePath);
                new FileInfo(targetPath).MoveTo(backupFilePath, true);
                replaceList.Add(backupFilePath);
                new FileInfo(file).CopyTo(targetPath, true);
            }
            else
            {
                new FileInfo(file).CopyTo(targetPath, true);
                addList.Add(targetPath);
            }
        }

        AIDto dto = new AIDto();
        dto.AddList = addList;
        dto.ReplaceList = replaceList;
        dto.AIName = Name;

        string dtoStr = JsonSerializeHelper.JsonSerialize(dto);
        string aiJsonPath = AIConfig.AIJsonPath;
        File.WriteAllText(aiJsonPath, dtoStr);//会覆写
    }

    /// <summary>
    /// 还原原有AI
    /// </summary>
    public void Recovery()
    {
        if (!File.Exists(AIConfig.AIJsonPath))
            return;

        string jsonStr = File.ReadAllText(AIConfig.AIJsonPath);
        AIDto dto = JsonSerializeHelper.JsonDeserialize<AIDto>(jsonStr);
        if (dto == null)
            return;

        string fileBackupDir = SafePath.CombineDirectoryPath(AIConfig.AIBackupDir, dto.AIName);
        if (!Directory.Exists(fileBackupDir))
            return;

        foreach (var item in dto.AddList)
        {
            if (File.Exists(item))
                File.Delete(item);
        }

        foreach (var item in dto.ReplaceList)
        {
            if (File.Exists(item))
            {
                string routePath = item.Replace(fileBackupDir, "");
                string recoveryFilePath = SafePath.CombineFilePath(ProgramConstants.GamePath, routePath);
                new FileInfo(item).CopyTo(recoveryFilePath, true);
            }
        }

        Directory.Delete(fileBackupDir, true);
    }

    public static List<AI> GetAIs()
    {
        var ais = new List<AI>();

        string iniDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "INI");
        var aiIni = new IniFile(SafePath.CombineFilePath(iniDir, "AI.ini"));
        string section = "AI";
        if (aiIni.GetSection(section) == null)
            return ais;

        foreach (KeyValuePair<string, string> keyValuePair in aiIni.GetSection(section).Keys)
        {
            if (!aiIni.GetBooleanValue(keyValuePair.Value, "Visible", true))
                continue;

            var ai = new AI();
            ai.Name = keyValuePair.Value;
            if (string.IsNullOrEmpty(aiIni.GetStringValue(keyValuePair.Value, "Dir", string.Empty)))
                ai.Dir = $"INI/Game Options/{section}/{keyValuePair.Value}";
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
