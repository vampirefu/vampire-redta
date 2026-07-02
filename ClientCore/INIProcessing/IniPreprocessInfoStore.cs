using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace ClientCore.INIProcessing
{
    public class PreprocessedIniInfo
    {
        public PreprocessedIniInfo(string fileName, string originalHash, string processedHash)
        {
            FileName = fileName;
            OriginalFileHash = originalHash;
            ProcessedFileHash = processedHash;
        }

        public PreprocessedIniInfo(string[] info)
        {
            FileName = info[0];
            OriginalFileHash = info[1];
            ProcessedFileHash = info[2];
        }

        public string FileName { get; }
        public string OriginalFileHash { get; set; }
        public string ProcessedFileHash { get; set; }
    }

    /// <summary>
    /// 处理有关哪些 INI 文件已被客户端预处理的信息。
    /// </summary>
    public class IniPreprocessInfoStore
    {
        private const string StoreIniName = "ProcessedIniInfo.ini";
        private const string ProcessedINIsSection = "ProcessedINIs";

        public List<PreprocessedIniInfo> PreprocessedIniInfos { get; } = new List<PreprocessedIniInfo>();

        /// <summary>
        /// 加载已预处理的 INI 信息。
        /// </summary>
        public void Load()
        {
            FileInfo processedIniInfoFile = SafePath.GetFile(ProgramConstants.ClientUserFilesPath, "ProcessedIniInfo.ini");

            if (!processedIniInfoFile.Exists)
                return;

            var iniFile = new IniFile(processedIniInfoFile.FullName);
            var keys = iniFile.GetSectionKeys(ProcessedINIsSection);
            foreach (string key in keys)
            {
                string[] values = iniFile.GetStringValue(ProcessedINIsSection, key, string.Empty).Split(
                    new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (values.Length != 3)
                {
                    Logger.Log("Failed to parse preprocessed INI info, key " + key);
                    continue;
                }

                // 如果 INI 文件已不存在，保留其记录就没有意义
                if (!SafePath.GetFile(ProgramConstants.GamePath, "INI", values[0]).Exists)
                    continue;

                PreprocessedIniInfos.Add(new PreprocessedIniInfo(values));
            }
        }

        /// <summary>
        /// 检查一个（可能已处理的）INI 文件是否为最新，
        /// 或者是否需要（重新）处理。
        /// </summary>
        /// <param name="fileName">INI 文件在其目录中的名称。请勿提供完整文件路径。</param>
        /// <returns>如果 INI 文件为最新则返回 true，如果需要处理则返回 false。</returns>
        public bool IsIniUpToDate(string fileName)
        {
            PreprocessedIniInfo info = PreprocessedIniInfos.Find(i => i.FileName == fileName);

            if (info == null)
                return false;

            string processedFileHash = Utilities.CalculateSHA1ForFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", fileName));
            if (processedFileHash != info.ProcessedFileHash)
                return false;

            string originalFileHash = Utilities.CalculateSHA1ForFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "INI", "Base", fileName));
            if (originalFileHash != info.OriginalFileHash)
                return false;

            return true;
        }

        public void UpsertRecord(string fileName, string originalFileHash, string processedFileHash)
        {
            var existing = PreprocessedIniInfos.Find(i => i.FileName == fileName);
            if (existing == null)
            {
                PreprocessedIniInfos.Add(new PreprocessedIniInfo(fileName, originalFileHash, processedFileHash));
            }
            else
            {
                existing.OriginalFileHash = originalFileHash;
                existing.ProcessedFileHash = processedFileHash;
            }
        }

        public void Write()
        {
            FileInfo processedIniInfoFile = SafePath.GetFile(ProgramConstants.ClientUserFilesPath, "ProcessedIniInfo.ini");

            if (processedIniInfoFile.Exists)
                processedIniInfoFile.Delete();

            IniFile iniFile = new IniFile(processedIniInfoFile.FullName);
            for (int i = 0; i < PreprocessedIniInfos.Count; i++)
            {
                PreprocessedIniInfo info = PreprocessedIniInfos[i];

                iniFile.SetStringValue(ProcessedINIsSection, i.ToString(),
                    string.Join(",", info.FileName, info.OriginalFileHash, info.ProcessedFileHash));
            }
            iniFile.WriteIniFile();
        }
    }
}
