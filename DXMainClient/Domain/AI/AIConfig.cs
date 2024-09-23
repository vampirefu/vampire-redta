using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using Rampastring.Tools;

namespace DTAClient.Domain.AI;
internal class AIConfig
{
    /// <summary>
    /// AI备份根目录
    /// </summary>
    public static string AIBackupDir = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "AI_Backup");

    /// <summary>
    /// AI Json记录文件
    /// </summary>
    public static string AIJsonPath = SafePath.CombineFilePath(AIConfig.AIBackupDir, "AIRecord.Json");
}
