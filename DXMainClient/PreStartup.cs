using System;
#if WINFORMS
using System.Windows.Forms;
#endif
using System.Diagnostics;
using System.IO;
using DTAClient.Domain;
using Rampastring.Tools;
using ClientCore;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DTAClient
{
    /// <summary>
    /// 包含客户端启动参数。
    /// </summary>
    struct StartupParams
    {
        public StartupParams(bool noAudio, bool multipleInstanceMode,
            List<string> unknownParams)
        {
            NoAudio = noAudio;
            MultipleInstanceMode = multipleInstanceMode;
            UnknownStartupParams = unknownParams;
        }

        public bool NoAudio { get; }
        public bool MultipleInstanceMode { get; }
        public List<string> UnknownStartupParams { get; }
    }

    static class PreStartup
    {
        /// <summary>
        /// 初始化各种基本系统，如客户端日志器、
        /// 常量和通用异常处理器。
        /// 从INI文件读取用户设置，
        /// 检查必要权限，如果一切正常则启动客户端。
        /// </summary>
        /// <param name="parameters">客户端的启动参数。</param>
        public static void Initialize(StartupParams parameters)
        {
#if WINFORMS
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.ThreadException += (sender, args) => HandleException(sender, args.Exception);
#endif
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>HandleException(sender, (Exception)args.ExceptionObject);

            DirectoryInfo gameDirectory = SafePath.GetDirectory(ProgramConstants.GamePath);

            Environment.CurrentDirectory = gameDirectory.FullName;

            DirectoryInfo clientUserFilesDirectory = SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath);
            FileInfo clientLogFile = SafePath.GetFile(clientUserFilesDirectory.FullName, "client.log");
            ProgramConstants.LogFileName = clientLogFile.FullName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                CheckPermissions();

            Logger.Initialize(clientUserFilesDirectory.FullName, clientLogFile.Name);
            Logger.WriteLogFile = true;

            if (!clientUserFilesDirectory.Exists)
                clientUserFilesDirectory.Create();

            clientLogFile.Delete();

            MainClientConstants.Initialize();

            Logger.Log("***Logfile for " + MainClientConstants.GAME_NAME_LONG + " client***");
            Logger.Log("Client version: " + Assembly.GetAssembly(typeof(PreStartup)).GetName().Version);

            // 记录给定的启动参数信息
            if (parameters.NoAudio)
            {
                Logger.Log("Startup parameter: No audio");

                // TODO fix
                throw new NotImplementedException("-NOAUDIO 当前未实现,请在不使用该参数的情况下运行客户端.");
            }

            if (parameters.MultipleInstanceMode)
                Logger.Log("Startup parameter: Allow multiple client instances");

            parameters.UnknownStartupParams.ForEach(p => Logger.Log("Unknown startup parameter: " + p));

            Logger.Log("Loading settings.");

            UserINISettings.Initialize(ClientConfiguration.Instance.SettingsIniName);

            // 删除旧目标项目版本的过时文件

            gameDirectory.EnumerateFiles("mainclient.log").SingleOrDefault()?.Delete();
            gameDirectory.EnumerateFiles("aunchupdt.dat").SingleOrDefault()?.Delete();

            try
            {
                gameDirectory.EnumerateFiles("wsock32.dll").SingleOrDefault()?.Delete();
            }
            catch (Exception ex)
            {
                LogException(ex);

                string error = "删除wsock32.dll失败！请关闭所有" +
                    "可能使用该文件的应用程序，然后重新启动客户端。"
                    + Environment.NewLine + Environment.NewLine +
                    "消息: " + ex.Message;

                ProgramConstants.DisplayErrorAction(null, error, true);
            }

#if WINFORMS
            ApplicationConfiguration.Initialize();
#endif

            new Startup().Execute();
        }

        public static void LogException(Exception ex, bool innerException = false)
        {
            if (!innerException)
                Logger.Log("KABOOOOOOM!!! Info:");
            else
                Logger.Log("InnerException info:");

            Logger.Log("Type: " + ex.GetType());
            Logger.Log("Message: " + ex.Message);
            Logger.Log("Source: " + ex.Source);
            Logger.Log("TargetSite.Name: " + ex.TargetSite.Name);
            Logger.Log("Stacktrace: " + ex.StackTrace);

            if (ex.InnerException is not null)
                LogException(ex.InnerException, true);
        }

        static void HandleException(object sender, Exception ex)
        {
            LogException(ex);

            string errorLogPath = SafePath.CombineFilePath(ProgramConstants.ClientUserFilesPath, "ClientCrashLogs", FormattableString.Invariant($"ClientCrashLog{DateTime.Now.ToString("_yyyy_MM_dd_HH_mm")}.txt"));
            bool crashLogCopied = false;

            try
            {
                DirectoryInfo crashLogsDirectoryInfo = SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath, "ClientCrashLogs");

                if (!crashLogsDirectoryInfo.Exists)
                    crashLogsDirectoryInfo.Create();

                File.Copy(SafePath.CombineFilePath(ProgramConstants.ClientUserFilesPath, "client.log"), errorLogPath, true);
                crashLogCopied = true;
            }
            catch { }

            string error = string.Format("{0}已崩溃.错误信息:" + Environment.NewLine + Environment.NewLine +
                ex.Message + Environment.NewLine + Environment.NewLine + (crashLogCopied ?
                "崩溃日志已保存至以下文件:" + " " + Environment.NewLine + Environment.NewLine +
                errorLogPath + Environment.NewLine + Environment.NewLine : "") +
                (crashLogCopied ? "如果问题可重复,请联系{1}工作人员{2}并提供崩溃日志文件。" :
                "如果问题可重复,请联系{1}工作人员{2}。"),
                MainClientConstants.GAME_NAME_LONG,
                MainClientConstants.GAME_NAME_SHORT,
                MainClientConstants.SUPPORT_URL_SHORT);

            ProgramConstants.DisplayErrorAction("崩溃", error, true);
        }

        [SupportedOSPlatform("windows")]
        private static void CheckPermissions()
        {
            if (UserHasDirectoryAccessRights(ProgramConstants.GamePath, FileSystemRights.Modify))
                return;

            string error = string.Format(("您似乎正在从写保护目录运行{0}。" + Environment.NewLine + Environment.NewLine +
                "为了使{1}在写保护目录中正常运行,需要管理员权限。" + Environment.NewLine + Environment.NewLine +
                "您想以管理员权限重新启动客户端吗?" + Environment.NewLine + Environment.NewLine +
                "请同时确保您的安全软件没有阻止{1}。"), MainClientConstants.GAME_NAME_LONG, MainClientConstants.GAME_NAME_SHORT);

            ProgramConstants.DisplayErrorAction("需要管理员权限", error, false);

            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = SafePath.CombineFilePath(ProgramConstants.StartupExecutable),
                Verb = "runas",
                CreateNoWindow = true
            });
            Environment.Exit(1);
        }

        /// <summary>
        /// 检查客户端是否对目录拥有特定的文件系统权限。
        /// 参见 https://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder 上 ssds 的回答
        /// </summary>
        /// <param name="path">目录路径。</param>
        /// <param name="accessRights">文件系统权限。</param>
        [SupportedOSPlatform("windows")]
        private static bool UserHasDirectoryAccessRights(string path, FileSystemRights accessRights)
        {
            var currentUser = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(currentUser);

            // 如果用户没有以管理员权限在 Program Files 中运行客户端，则需要提示用户提升权限。
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                string progfiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progfilesx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (ProgramConstants.GamePath.Contains(progfiles) || ProgramConstants.GamePath.Contains(progfilesx86))
                    return false;
            }

            var isInRoleWithAccess = false;

            try
            {
                var di = new DirectoryInfo(path);
                var acl = di.GetAccessControl();
                var rules = acl.GetAccessRules(true, true, typeof(NTAccount));

                foreach (AuthorizationRule rule in rules)
                {
                    var fsAccessRule = rule as FileSystemAccessRule;
                    if (fsAccessRule == null)
                        continue;

                    if ((fsAccessRule.FileSystemRights & accessRights) > 0)
                    {
                        var ntAccount = rule.IdentityReference as NTAccount;
                        if (ntAccount == null)
                            continue;

                        if (principal.IsInRole(ntAccount.Value))
                        {
                            if (fsAccessRule.AccessControlType == AccessControlType.Deny)
                                return false;
                            isInRoleWithAccess = true;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            return isInRoleWithAccess;
        }
    }
}