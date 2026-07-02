using ClientCore;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DTAConfig
{
    /// <summary>
    /// DirectDraw包装器选项。
    /// </summary>
    class DirectDrawWrapper
    {
        /// <summary>
        /// 创建新的DirectDrawWrapper实例并从INI文件解析其配置。
        /// </summary>
        /// <param name="internalName">渲染器的内部名称。</param>
        /// <param name="iniFile">用于解析渲染器选项的文件。</param>
        public DirectDrawWrapper(string internalName, IniFile iniFile)
        {
            InternalName = internalName;
            Parse(iniFile.GetSection(InternalName));
        }

        public string InternalName { get; private set; }
        public string UIName { get; private set; }

        /// <summary>
        /// 如果非空，窗口模式将写入渲染器设置文件中此节的INI键，
        /// 而非常规游戏设置INI文件。
        /// </summary>
        public string WindowedModeSection { get; private set; }

        /// <summary>
        /// 如果非空，窗口模式将写入由
        /// <see cref="DirectDrawWrapper.WindowedModeSection"/> 定义的节中的此INI键，
        /// 而非常规设置INI文件。
        /// </summary>
        public string WindowedModeKey { get; private set; }

        /// <summary>
        /// 如果非空，控制游戏是否以无边框窗口模式运行的设置
        /// 将写入由
        /// <see cref="DirectDrawWrapper.WindowedModeSection"/> 定义的节中的此INI键，
        /// 而非常规设置INI文件。
        /// </summary>
        public string BorderlessWindowedModeKey { get; private set; }

        /// <summary>
        /// 如果设置，当值为"false"时启用无边框模式，
        /// 当值为"true"时禁用无边框模式。
        /// </summary>
        public bool IsBorderlessWindowedModeKeyReversed { get; private set; }

        public bool Hidden { get; private set; }

        /// <summary>
        /// Many ddraw wrappers need qres.dat to set the desktop to 16 bit mode
        /// </summary>
        public bool UseQres { get; private set; } = true;

        /// <summary>
        /// 如果设为false，使用此渲染器时客户端不会为游戏可执行文件设置单核亲和性。
        /// </summary>
        public bool SingleCoreAffinity { get; private set; } = true;

        /// <summary>
        /// The filename of the configuration INI of the renderer in the game directory.
        /// </summary>
        public string ConfigFileName { get; private set; }

        private string ddrawDLLPath;
        private string resConfigFileName;
        private List<string> filesToCopy = new List<string>();
        private List<OSVersion> disallowedOSList = new List<OSVersion>();

        /// <summary>
        /// 从INI节中读取此DirectDrawWrapper的属性。
        /// </summary>
        /// <param name="section">INI节。</param>
        private void Parse(IniSection section)
        {
            if (section == null)
            {
                Logger.Log("DirectDrawWrapper: Configuration for renderer '" + InternalName + "' not found!");
                return;
            }

            UIName = section.GetStringValue("UIName", "未命名渲染器");

            if (section.GetBooleanValue("IsDxWnd", false))
            {
                // 为了与先前客户端版本向后兼容
                WindowedModeSection = "DxWnd";
                WindowedModeKey = "RunInWindow";
                BorderlessWindowedModeKey = "NoWindowFrame";
            }

            WindowedModeSection = section.GetStringValue("WindowedModeSection", WindowedModeSection);
            WindowedModeKey = section.GetStringValue("WindowedModeKey", WindowedModeKey);
            BorderlessWindowedModeKey = section.GetStringValue("BorderlessWindowedModeKey", BorderlessWindowedModeKey);
            IsBorderlessWindowedModeKeyReversed = section.GetBooleanValue("IsBorderlessWindowedModeKeyReversed",
                IsBorderlessWindowedModeKeyReversed);

            if (BorderlessWindowedModeKey != null && WindowedModeSection == null)
            {
                throw new DirectDrawWrapperConfigurationException(
                    "BorderlessWindowedModeKey= is defined for renderer" +
                    $" {InternalName} but WindowedModeSection= is not!");
            }

            Hidden = section.GetBooleanValue("Hidden", false);
            UseQres = section.GetBooleanValue("UseQres", UseQres);
            SingleCoreAffinity = section.GetBooleanValue("SingleCoreAffinity", SingleCoreAffinity);
            ddrawDLLPath = section.GetStringValue("DLLName", string.Empty);
            ConfigFileName = section.GetStringValue("ConfigFileName", string.Empty);
            resConfigFileName = section.GetStringValue("ResConfigFileName", ConfigFileName);

            filesToCopy = section.GetStringValue("AdditionalFiles", string.Empty).Split(
                new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            string[] disallowedOSs = section.GetStringValue("DisallowedOperatingSystems", string.Empty).Split(
                new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string os in disallowedOSs)
            {
                OSVersion disallowedOS = (OSVersion)Enum.Parse(typeof(OSVersion), os.Trim());
                disallowedOSList.Add(disallowedOS);
            }

            if (!string.IsNullOrEmpty(ddrawDLLPath) &&
                !SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), ddrawDLLPath).Exists)
                Logger.Log("DirectDrawWrapper: File specified in DLLPath= for renderer '" + InternalName + "' does not exist!");

            if (!string.IsNullOrEmpty(resConfigFileName) &&
                !SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), resConfigFileName).Exists)
                Logger.Log("DirectDrawWrapper: File specified in ConfigFileName= for renderer '" + InternalName + "' does not exist!");

            foreach (var file in filesToCopy)
            {
                if (!SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), file).Exists)
                    Logger.Log("DirectDrawWrapper: Additional file '" + file + "' for renderer '" + InternalName + "' does not exist!");
            }
        }

        /// <summary>
        /// 如果此包装器与给定操作系统兼容则返回true，否则返回false。
        /// </summary>
        /// <param name="os">操作系统。</param>
        public bool IsCompatibleWithOS(OSVersion os)
        {
            return !disallowedOSList.Contains(os);
        }

        /// <summary>
        /// 将渲染器的文件应用到游戏目录。
        /// </summary>
        public void Apply()
        {
            if (!string.IsNullOrEmpty(ddrawDLLPath))
            {
                File.Copy(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), ddrawDLLPath), SafePath.CombineFilePath(ProgramConstants.GamePath, "ddraw.dll"), true);
            }
            else
                File.Delete(SafePath.CombineFilePath(ProgramConstants.GamePath, "ddraw.dll"));


            if (!string.IsNullOrEmpty(ConfigFileName) && !string.IsNullOrEmpty(resConfigFileName)
                && !SafePath.GetFile(ProgramConstants.GamePath, ConfigFileName).Exists) // 不覆盖已有设置
            {
                File.Copy(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), resConfigFileName), SafePath.CombineFilePath(ProgramConstants.GamePath, Path.GetFileName(ConfigFileName)));
            }

            foreach (var file in filesToCopy)
            {
                File.Copy(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), file), SafePath.CombineFilePath(ProgramConstants.GamePath, Path.GetFileName(file)), true);
            }
        }

        /// <summary>
        /// 从游戏目录中清除渲染器的文件。
        /// </summary>
        public void Clean()
        {
            if (!string.IsNullOrEmpty(ConfigFileName))
                SafePath.DeleteFileIfExists(ProgramConstants.GamePath, Path.GetFileName(ConfigFileName));

            foreach (var file in filesToCopy)
                SafePath.DeleteFileIfExists(ProgramConstants.GamePath, Path.GetFileName(file));
        }

        /// <summary>
        /// 检查此渲染器是否通过其自身配置INI文件而非游戏设置INI文件启用窗口模式。
        /// </summary>
        public bool UsesCustomWindowedOption()
        {
            return !string.IsNullOrEmpty(WindowedModeSection) &&
                !string.IsNullOrEmpty(WindowedModeKey);
        }
    }

    /// <summary>
    /// 当DirectDraw包装器配置包含无效或意外的设置/数据，
    /// 或缺少必需的设置/数据时抛出的异常。
    /// </summary>
    class DirectDrawWrapperConfigurationException : Exception
    {
        public DirectDrawWrapperConfigurationException(string message) : base(message)
        {
        }
    }
}
