using ClientCore;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace DTAConfig.Settings
{
    sealed class FileSourceDestinationInfo
    {
        private readonly string destinationPath;
        private readonly string sourcePath;

        public string SourcePath => SafePath.CombineFilePath(ProgramConstants.GamePath, sourcePath);

        public string DestinationPath => SafePath.CombineFilePath(ProgramConstants.GamePath, destinationPath);
        /// <summary>
        /// 如果 <see cref="FileOperationOptions"/> 设置为
        /// <see cref="FileOperationOptions.KeepChanges"/>，
        /// 用户编辑的文件保存到的路径。
        /// </summary>
        public string CachedPath => Path.Combine(ProgramConstants.ClientUserFilesPath, "SettingsCache", sourcePath);

        public FileOperationOptions FileOperationOptions { get; }

        public FileSourceDestinationInfo(string source, string destination, FileOperationOptions options)
        {
            sourcePath = source;
            destinationPath = destination;
            FileOperationOptions = options;
        }

        /// <summary>
        /// 从给定字符串构造 <see cref="FileSourceDestinationInfo"/> 的新实例。
        /// </summary>
        /// <param name="value">要解析的字符串。</param>
        public FileSourceDestinationInfo(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 2)
                throw new ArgumentException($"{nameof(FileSourceDestinationInfo)}: " +
                    $"解析值中指定的参数过少", nameof(value));

            FileOperationOptions options = default(FileOperationOptions);
            if (parts.Length >= 3)
                Enum.TryParse(parts[2], out options);

            sourcePath = parts[0];
            destinationPath = parts[1];
            FileOperationOptions = options;
        }

        /// <summary>
        /// 将INI节中特定键列表值解析为
        /// <see cref="FileSourceDestinationInfo"/> 对象列表的方法。
        /// </summary>
        /// <param name="section">要从中解析键值的INI节。</param>
        /// <param name="iniKeyPrefix">从键列表解析值时追加索引的字符串。</param>
        /// <returns>包含所有正确定义的 <see cref="FileSourceDestinationInfo"/> 的 <see cref="List{FileSourceDestinationInfo}"/>。</returns>
        public static List<FileSourceDestinationInfo> ParseFSDInfoList(IniSection section, string iniKeyPrefix)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            List<FileSourceDestinationInfo> result = new List<FileSourceDestinationInfo>();
            string fileInfo;

            for (int i = 0;
                !string.IsNullOrWhiteSpace(
                    fileInfo = section.GetStringValue($"{iniKeyPrefix}{i}", string.Empty));
                i++)
            {
                result.Add(new FileSourceDestinationInfo(fileInfo));
            }

            return result;
        }

        /// <summary>
        /// 根据 <see cref="FileOperationOptions"/> 执行从
        /// <see cref="SourcePath"/> 到 <see cref="DestinationPath"/> 的文件操作。
        /// </summary>
        public void Apply()
        {
            switch (FileOperationOptions)
            {
                case FileOperationOptions.OverwriteOnMismatch:
                    string sourceHash = Utilities.CalculateSHA1ForFile(SourcePath);
                    string destinationHash = Utilities.CalculateSHA1ForFile(DestinationPath);

                    if (sourceHash != destinationHash)
                        File.Copy(SourcePath, DestinationPath, true);

                    break;

                case FileOperationOptions.DontOverwrite:
                    if (!File.Exists(DestinationPath))
                        File.Copy(SourcePath, DestinationPath, false);

                    break;

                case FileOperationOptions.KeepChanges:
                    if (!File.Exists(DestinationPath))
                    {
                        if (File.Exists(CachedPath))
                            File.Copy(CachedPath, DestinationPath, false);
                        else
                            File.Copy(SourcePath, DestinationPath, false);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(CachedPath));
                    File.Copy(DestinationPath, CachedPath, true);

                    break;

                case FileOperationOptions.AlwaysOverwrite:
                    File.Copy(SourcePath, DestinationPath, true);
                    break;

                default:
                    throw new InvalidOperationException($"{nameof(FileSourceDestinationInfo)}: " +
                        $"Invalid {nameof(FileOperationOptions)} value of {FileOperationOptions}");
            }
        }

        /// <summary>
        /// 根据 <see cref="FileOperationOptions"/> 执行文件操作，
        /// 撤销 <see cref="Apply"/> 对 <see cref="DestinationPath"/> 所做的更改。
        /// </summary>
        public void Revert()
        {
            switch (FileOperationOptions)
            {
                case FileOperationOptions.KeepChanges:
                    if (File.Exists(DestinationPath))
                    {
                        //SafePath.GetDirectory(CachedPath).Create();
                        var dir = Path.GetDirectoryName(CachedPath);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.Copy(DestinationPath, CachedPath, true);
                        File.Delete(DestinationPath);
                    }
                    break;

                case FileOperationOptions.OverwriteOnMismatch:
                case FileOperationOptions.DontOverwrite:
                case FileOperationOptions.AlwaysOverwrite:
                    File.Delete(DestinationPath);
                    break;

                default:
                    throw new InvalidOperationException($"{nameof(FileSourceDestinationInfo)}: " +
                        $"Invalid {nameof(FileOperationOptions)} value of {FileOperationOptions}");
            }
        }
    }

    /// <summary>
    /// 定义使用 <see cref="FileSourceDestinationInfo"/> 执行文件操作的预期行为。
    /// </summary>
    public enum FileOperationOptions
    {
        AlwaysOverwrite,
        OverwriteOnMismatch,
        DontOverwrite,
        KeepChanges
    }
}
