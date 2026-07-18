using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ClientCore;
using ClientCore.INIProcessing;
using Rampastring.Tools;
using Rampastring.XNAUI;

namespace ClientGUI
{
    public static class GameProcessLogic
    {
        private const bool FIXED_RENDERER_USES_QRES = false;
        private const bool FIXED_RENDERER_SINGLE_CORE_AFFINITY = true;

        public static event Action GameProcessStarted;

        public static event Action GameProcessStarting;

        public static event Action GameProcessExited;

        public static void StartGameProcess(WindowManager windowManager)
        {
            Logger.Log("About to launch main game executable.");

            int waitTimes = 0;
            while (PreprocessorBackgroundTask.Instance.IsRunning)
            {
                Thread.Sleep(1000);
                waitTimes++;
                if (waitTimes > 10)
                {
                    XNAMessageBox.Show(
                        windowManager,
                        "INI预处理未完成",
                        "INI预处理未完成。请尝试重新启动游戏。如果问题持续存在，请联系游戏或模组作者获取支持。");
                    return;
                }
            }

            OSVersion osVersion = ClientConfiguration.Instance.GetOperatingSystemVersion();

            string gameExecutableName;
            string additionalExecutableName = string.Empty;

            if (osVersion == OSVersion.UNIX)
            {
                gameExecutableName = ClientConfiguration.Instance.UnixGameExecutableName;
            }
            else
            {
                string launcherExecutableName = ClientConfiguration.Instance.GameLauncherExecutableName;
                if (string.IsNullOrEmpty(launcherExecutableName))
                {
                    gameExecutableName = ClientConfiguration.Instance.GetGameExecutableName();
                }
                else
                {
                    gameExecutableName = launcherExecutableName;
                    additionalExecutableName = "\"" + ClientConfiguration.Instance.GetGameExecutableName() + "\" ";
                }
            }

            string extraCommandLine = ClientConfiguration.Instance.ExtraExeCommandLineParameters;

            SafePath.DeleteFileIfExists(ProgramConstants.GamePath, "DTA.LOG");
            SafePath.DeleteFileIfExists(ProgramConstants.GamePath, "TI.LOG");
            SafePath.DeleteFileIfExists(ProgramConstants.GamePath, "TS.LOG");

            GameProcessStarting?.Invoke();

            if (UserINISettings.Instance.WindowedMode &&
                FIXED_RENDERER_USES_QRES &&
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger.Log("Windowed mode is enabled - using QRes.");
                Process qResProcess = new Process();
                qResProcess.StartInfo.FileName = ProgramConstants.QRES_EXECUTABLE;

                if (!string.IsNullOrEmpty(extraCommandLine))
                {
                    qResProcess.StartInfo.Arguments =
                        "c=16 /R " +
                        "\"" + SafePath.CombineFilePath(ProgramConstants.GamePath, gameExecutableName) + "\" " +
                        additionalExecutableName +
                        "-SPAWN " +
                        extraCommandLine;
                }
                else
                {
                    qResProcess.StartInfo.Arguments =
                        "c=16 /R " +
                        "\"" + SafePath.CombineFilePath(ProgramConstants.GamePath, gameExecutableName) + "\" " +
                        additionalExecutableName +
                        "-SPAWN";
                }

                qResProcess.EnableRaisingEvents = true;
                qResProcess.Exited += Process_Exited;
                Logger.Log("Launch executable: " + qResProcess.StartInfo.FileName);
                Logger.Log("Launch arguments: " + qResProcess.StartInfo.Arguments);

                try
                {
                    qResProcess.Start();
                }
                catch (Exception ex)
                {
                    Logger.Log("Error launching QRes: " + ex.Message);
                    XNAMessageBox.Show(
                        windowManager,
                        "游戏启动错误",
                        "启动 " + ProgramConstants.QRES_EXECUTABLE + " 时出错。请检查杀毒软件是否阻止了客户端，或尝试以管理员身份运行。" +
                        Environment.NewLine +
                        Environment.NewLine +
                        "返回的错误: " +
                        ex.Message);
                    Process_Exited(qResProcess, EventArgs.Empty);
                    return;
                }

                if (Environment.ProcessorCount > 1 && FIXED_RENDERER_SINGLE_CORE_AFFINITY)
                {
                    qResProcess.ProcessorAffinity = (IntPtr)2;
                }
            }
            else
            {
                string arguments;
                if (!string.IsNullOrWhiteSpace(extraCommandLine))
                {
                    arguments = " " + additionalExecutableName + "-SPAWN " + extraCommandLine;
                }
                else
                {
                    arguments = additionalExecutableName + "-SPAWN";
                }

                FileInfo gameFileInfo = SafePath.GetFile(ProgramConstants.GamePath, gameExecutableName);
                var gameProcess = Process.Start(new ProcessStartInfo(gameFileInfo.FullName, arguments));

                gameProcess.EnableRaisingEvents = true;
                gameProcess.Exited += Process_Exited;

                Logger.Log("Launch executable: " + gameProcess.StartInfo.FileName);
                Logger.Log("Launch arguments: " + gameProcess.StartInfo.Arguments);

                try
                {
                    gameProcess.Start();
                    Logger.Log("GameProcessLogic: Process started.");
                }
                catch (Exception ex)
                {
                    Logger.Log("Error launching " + gameFileInfo.Name + ": " + ex.Message);
                    XNAMessageBox.Show(
                        windowManager,
                        "游戏启动错误",
                        "启动 " + gameFileInfo.Name + " 时出错。请检查杀毒软件是否阻止了客户端，或尝试以管理员身份运行。" +
                        Environment.NewLine +
                        Environment.NewLine +
                        "返回的错误: " +
                        ex.Message);
                    Process_Exited(gameProcess, EventArgs.Empty);
                    return;
                }

                if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) &&
                    Environment.ProcessorCount > 1 &&
                    FIXED_RENDERER_SINGLE_CORE_AFFINITY)
                {
                    gameProcess.ProcessorAffinity = 2;
                }
            }

            GameProcessStarted?.Invoke();
            Logger.Log("Waiting for qres.dat or " + gameExecutableName + " to exit.");
        }

        static void Process_Exited(object sender, EventArgs e)
        {
            Logger.Log("GameProcessLogic: Process exited.");
            Process proc = (Process)sender;
            proc.Exited -= Process_Exited;
            proc.Dispose();
            GameProcessExited?.Invoke();
        }
    }
}
