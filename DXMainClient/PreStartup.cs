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
    /// Contains client startup parameters.
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
        /// Initializes various basic systems like the client's logger, 
        /// constants, and the general exception handler.
        /// Reads the user's settings from an INI file, 
        /// checks for necessary permissions and starts the client if
        /// everything goes as it should.
        /// </summary>
        /// <param name="parameters">The client's startup parameters.</param>
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

            // Log information about given startup params
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

            // Delete obsolete files from old target project versions

            gameDirectory.EnumerateFiles("mainclient.log").SingleOrDefault()?.Delete();
            gameDirectory.EnumerateFiles("aunchupdt.dat").SingleOrDefault()?.Delete();

            try
            {
                gameDirectory.EnumerateFiles("wsock32.dll").SingleOrDefault()?.Delete();
            }
            catch (Exception ex)
            {
                LogException(ex);

                string error = "Deleting wsock32.dll failed! Please close any " +
                    "applications that could be using the file, and then start the client again."
                    + Environment.NewLine + Environment.NewLine +
                    "Message: " + ex.Message;

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
        /// Checks whether the client has specific file system rights to a directory.
        /// See ssds's answer at https://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder
        /// </summary>
        /// <param name="path">The path to the directory.</param>
        /// <param name="accessRights">The file system rights.</param>
        [SupportedOSPlatform("windows")]
        private static bool UserHasDirectoryAccessRights(string path, FileSystemRights accessRights)
        {
            var currentUser = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(currentUser);

            // If the user is not running the client with administrator privileges in Program Files, they need to be prompted to do so.
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