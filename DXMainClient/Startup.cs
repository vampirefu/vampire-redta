using System;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using DTAClient.Domain;
using ClientCore;
using Rampastring.Tools;
using DTAClient.DXGUI;
using System.Security.Principal;
using System.DirectoryServices;
using System.Linq;
using DTAClient.Online;
using ClientCore.INIProcessing;
using System.Threading.Tasks;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClientCore.Settings;
using Microsoft.Xna.Framework.Graphics;

namespace DTAClient
{
    /// <summary>
    /// 处理客户端初始化的类。
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// 启动和初始化的主方法。
        /// </summary>
        public void Execute()
        {
            string themePath = ClientConfiguration.Instance.GetThemePath(UserINISettings.Instance.ClientTheme);
           // string language = ClientConfiguration.Instance.GetLanguagePath(UserINISettings.Instance.Language);
           
            if (themePath == null)
            {
                themePath = ClientConfiguration.Instance.GetThemeInfoFromIndex(0)[1];
            }

          //  if (language == null)
        //    {
           //     language = ClientConfiguration.Instance.GetLanguageInfoFromIndex(0)[1];
        //    }

            ProgramConstants.RESOURCES_DIR = SafePath.CombineDirectoryPath(ProgramConstants.BASE_RESOURCE_PATH, themePath);

            DirectoryInfo resourcesDirectory = SafePath.GetDirectory(ProgramConstants.GetResourcePath());

            if (!resourcesDirectory.Exists)
                throw new DirectoryNotFoundException("Theme directory not found!" + Environment.NewLine + ProgramConstants.RESOURCES_DIR);

            // 更新器已移除：跳过初始化

            Logger.Log("OSDescription: " + RuntimeInformation.OSDescription);
            Logger.Log("OSArchitecture: " + RuntimeInformation.OSArchitecture);
            Logger.Log("ProcessArchitecture: " + RuntimeInformation.ProcessArchitecture);
            Logger.Log("FrameworkDescription: " + RuntimeInformation.FrameworkDescription);
            Logger.Log("RuntimeIdentifier: " + RuntimeInformation.RuntimeIdentifier);
            Logger.Log("Selected OS profile: " + MainClientConstants.OSId);
            Logger.Log("Current culture: " + CultureInfo.CurrentCulture);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // CheckSystemSpecifications 中的查询需要大量时间，
                // 因此我们在单独的线程中执行以加快启动速度
                Thread thread = new Thread(CheckSystemSpecifications);
                thread.Start();
            }

            GenerateOnlineIdAsync();

            Task.Factory.StartNew(() => PruneFiles(SafePath.GetDirectory(ProgramConstants.GamePath, "debug"), DateTime.Now.AddDays(-7)));
            Task.Factory.StartNew(MigrateOldLogFiles);

            DirectoryInfo updaterFolder = SafePath.GetDirectory(ProgramConstants.GamePath, "Updater");

            if (updaterFolder.Exists)
            {
                Logger.Log("Attempting to delete temporary updater directory.");
                try
                {
                    updaterFolder.Delete(true);
                }
                catch
                {
                }
            }

            if (ClientConfiguration.Instance.CreateSavedGamesDirectory)
            {
                DirectoryInfo savedGamesFolder = SafePath.GetDirectory(ProgramConstants.GamePath, "Saved Games");

                if (!savedGamesFolder.Exists)
                {
                    Logger.Log("Saved Games directory does not exist - attempting to create one.");
                    try
                    {
                        savedGamesFolder.Create();
                    }
                    catch
                    {
                    }
                }
            }

            // 更新器已移除：无需清理自定义组件

            FinalSunSettings.WriteFinalSunIni();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WriteInstallPathToRegistry();

            ClientConfiguration.Instance.RefreshSettings();

            // 启动 INI 文件预处理器
            PreprocessorBackgroundTask.Instance.Run();

            GameClass gameClass = new GameClass();

            int currentWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            int currentHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            UserINISettings.Instance.ClientResolutionX = new IntSetting(UserINISettings.Instance.SettingsIni, UserINISettings.VIDEO, "ClientResolutionX", currentWidth);
            UserINISettings.Instance.ClientResolutionY = new IntSetting(UserINISettings.Instance.SettingsIni, UserINISettings.VIDEO, "ClientResolutionY", currentHeight);

            gameClass.Run();
        }

        /// <summary>
        /// 递归删除指定目录中在 <paramref name="pruneThresholdTime"/> 或之前创建的所有文件。
        /// 如果删除文件后目录为空，则目录本身也将被删除。
        /// </summary>
        /// <param name="directory">要清理文件的目录。</param>
        /// <param name="pruneThresholdTime">文件创建时间等于或早于此时间才会被清理。</param>
        private void PruneFiles(DirectoryInfo directory, DateTime pruneThresholdTime)
        {
            if (!directory.Exists)
                return;

            try
            {
                foreach (FileSystemInfo fsEntry in directory.EnumerateFileSystemInfos())
                {
                    if ((fsEntry.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                        PruneFiles(new DirectoryInfo(fsEntry.FullName), pruneThresholdTime);
                    else
                    {
                        try
                        {
                            FileInfo fileInfo = new FileInfo(fsEntry.FullName);
                            if (fileInfo.CreationTime <= pruneThresholdTime)
                                fileInfo.Delete();
                        }
                        catch (Exception e)
                        {
                            Logger.Log("PruneFiles: Could not delete file " + fsEntry.Name +
                                ". Error message: " + e.Message);
                            continue;
                        }
                    }
                }

                if (!directory.EnumerateFileSystemInfos().Any())
                    directory.Delete();
            }
            catch (Exception ex)
            {
                Logger.Log("PruneFiles: An error occurred while pruning files from " +
                   directory.Name + ". Message: " + ex.Message);
            }
        }

        /// <summary>
        /// 将日志文件从过时目录移动到当前使用的目录，并调整文件名以匹配当前使用的时间戳方案。
        /// </summary>
        private void MigrateOldLogFiles()
        {
            MigrateLogFiles(SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath, "ClientCrashLogs"), "ClientCrashLog*.txt");
            MigrateLogFiles(SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath, "GameCrashLogs"), "EXCEPT*.txt");
            MigrateLogFiles(SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath, "SyncErrorLogs"), "SYNC*.txt");
        }

        /// <summary>
        /// 将匹配给定搜索模式的日志文件从指定目录移动到另一个目录，并调整文件名时间戳。
        /// </summary>
        /// <param name="newDirectory">新的日志文件目录。</param>
        /// <param name="searchPattern">日志文件名必须匹配的搜索字符串。可包含通配符（* 和 ?），但不支持正则表达式。</param>
        private static void MigrateLogFiles(DirectoryInfo newDirectory, string searchPattern)
        {
            DirectoryInfo currentDirectory = SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath, "ErrorLogs");
            try
            {
                if (!currentDirectory.Exists)
                    return;

                if (!newDirectory.Exists)
                    newDirectory.Create();

                foreach (FileInfo file in currentDirectory.EnumerateFiles(searchPattern))
                {
                    string filenameTS = Path.GetFileNameWithoutExtension(file.Name);
                    string[] ts = filenameTS.Split(new string[] { "_" }, StringSplitOptions.RemoveEmptyEntries);

                    string timestamp = string.Empty;
                    string baseFilename = Path.GetFileNameWithoutExtension(ts[0]);

                    if (ts.Length >= 6)
                    {
                        timestamp = string.Format("_{0}_{1}_{2}_{3}_{4}",
                            ts[3], ts[2].PadLeft(2, '0'), ts[1].PadLeft(2, '0'), ts[4].PadLeft(2, '0'), ts[5].PadLeft(2, '0'));
                    }

                    string newFilename = SafePath.CombineFilePath(newDirectory.FullName, baseFilename, timestamp, file.Extension);
                    file.MoveTo(newFilename);
                }

                if (!currentDirectory.EnumerateFiles().Any())
                    currentDirectory.Delete();
            }
            catch (Exception ex)
            {
                Logger.Log("MigrateLogFiles: An error occured while moving log files from " +
                    currentDirectory.Name + " to " +
                    newDirectory.Name + ". Message: " + ex.Message);
            }
        }

        /// <summary>
        /// 将处理器、显卡和内存信息写入日志文件。
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void CheckSystemSpecifications()
        {
            string cpu = string.Empty;
            string videoController = string.Empty;
            string memory = string.Empty;

            ManagementObjectSearcher searcher;

            try
            {
                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");

                foreach (var proc in searcher.Get())
                {
                    cpu = cpu + proc["Name"].ToString().Trim() + " (" + proc["NumberOfCores"] + " cores) ";
                }

            }
            catch
            {
                cpu = "CPU info not found";
            }

            try
            {
                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

                foreach (ManagementObject mo in searcher.Get())
                {
                    var currentBitsPerPixel = mo.Properties["CurrentBitsPerPixel"];
                    var description = mo.Properties["Description"];
                    if (currentBitsPerPixel != null && description != null)
                    {
                        if (currentBitsPerPixel.Value != null)
                            videoController = videoController + "Video controller: " + description.Value.ToString().Trim() + " ";
                    }
                }
            }
            catch
            {
                cpu = "Video controller info not found";
            }

            try
            {
                searcher = new ManagementObjectSearcher("Select * From Win32_PhysicalMemory");
                ulong total = 0;

                foreach (ManagementObject ram in searcher.Get())
                {
                    total += Convert.ToUInt64(ram.GetPropertyValue("Capacity"));
                }

                if (total != 0)
                    memory = "Total physical memory: " + (total >= 1073741824 ? total / 1073741824 + "GB" : total / 1048576 + "MB");
            }
            catch
            {
                cpu = "Memory info not found";
            }

            Logger.Log(string.Format("Hardware info: {0} | {1} | {2}", cpu.Trim(), videoController.Trim(), memory));
        }

        /// <summary>
        /// 生成用于在线游戏的ID。
        /// </summary>
        private static async Task GenerateOnlineIdAsync()
        {
#pragma warning disable format
                try
                {
                    await Task.CompletedTask;
                    ManagementObjectCollection mbsList = null;
                    ManagementObjectSearcher mbs = new ManagementObjectSearcher("Select * From Win32_processor");
                    mbsList = mbs.Get();
                    string cpuid = "";

                    foreach (ManagementObject mo in mbsList)
                        cpuid = mo["ProcessorID"].ToString();

                    ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                    var moc = mos.Get();
                    string mbid = "";

                    foreach (ManagementObject mo in moc)
                        mbid = (string)mo["SerialNumber"];

                    string sid = new SecurityIdentifier((byte[])new DirectoryEntry(string.Format("WinNT://{0},Computer", Environment.MachineName)).Children.Cast<DirectoryEntry>().First().InvokeGet("objectSID"), 0).AccountDomainSid.Value;

                    Connection.SetId(cpuid + mbid + sid);
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + ClientConfiguration.Instance.InstallationPathRegKey);
                    key.SetValue("Ident", cpuid + mbid + sid);
                }
                catch (Exception)
                {
                    Random rn = new Random();

                    using RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + ClientConfiguration.Instance.InstallationPathRegKey);
                    string str = rn.Next(Int32.MaxValue - 1).ToString();

                    try
                    {
                        Object o = key.GetValue("Ident");
                        if (o == null)
                            key.SetValue("Ident", str);
                        else
                            str = o.ToString();
                    }
                    catch { }

                    Connection.SetId(str);
                }
#pragma warning restore format
        }

        /// <summary>
        /// 将游戏安装路径写入 Windows 注册表。
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void WriteInstallPathToRegistry()
        {
            if (!UserINISettings.Instance.WritePathToRegistry)
            {
                Logger.Log("Skipping writing installation path to the Windows Registry because of INI setting.");
                return;
            }

            Logger.Log("Writing installation path to the Windows registry.");

            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + ClientConfiguration.Instance.InstallationPathRegKey);
                key.SetValue("InstallPath", ProgramConstants.GamePath);
            }
            catch
            {
                Logger.Log("Failed to write installation path to the Windows registry");
            }
        }
    }
}