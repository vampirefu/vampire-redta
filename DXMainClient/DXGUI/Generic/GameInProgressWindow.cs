using Rampastring.XNAUI.XNAControls;
using Rampastring.Tools;
using System;
using ClientCore;
using Rampastring.XNAUI;
using ClientGUI;
using System.IO;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace DTAClient.DXGUI
{
    /// <summary>
    /// 在游戏进行时在客户端中显示对话框。
    /// 同时在游戏进行时启用节能（降低 FPS），
    /// 并在游戏启动和退出时执行各种操作。
    /// </summary>
    public class GameInProgressWindow : XNAPanel
    {
        private const double POWER_SAVING_FPS = 5.0;

        public GameInProgressWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        private bool initialized = false;
        private bool nativeCursorUsed = false;

        private List<string> debugSnapshotDirectories;
        private DateTime debugLogLastWriteTime;

        public override void Initialize()
        {

          
            if (initialized)
                throw new InvalidOperationException("GameInProgressWindow cannot be initialized twice!");

            initialized = true;

            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            DrawBorders = false;
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);

            XNAWindow window = new XNAWindow(WindowManager);

            window.Name = "GameInProgressWindow";
            window.BackgroundTexture = AssetLoader.LoadTexture("gameinprogresswindowbg.png");
            window.ClientRectangle = new Rectangle(0, 0, 200, 100);


            //开始游戏
            XNALabel explanation = new XNALabel(WindowManager);
            explanation.Text = "游戏正在运行.";

            AddChild(window);

            window.AddChild(explanation);

            base.Initialize();

            GameProcessLogic.GameProcessStarted += SharedUILogic_GameProcessStarted;
            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;

            explanation.CenterOnParent();

            window.CenterOnParent();

            Game.TargetElapsedTime = TimeSpan.FromMilliseconds(1000.0 / UserINISettings.Instance.ClientFPS);

            Visible = false;
            Enabled = false;

            try
            {
                FileInfo debugLogFileInfo = SafePath.GetFile(ProgramConstants.GamePath, "debug", "debug.log");

                if (debugLogFileInfo.Exists)
                    debugLogLastWriteTime = debugLogFileInfo.LastWriteTime;
            }
            catch { }
        }

        private void SharedUILogic_GameProcessStarted()
        {

            debugSnapshotDirectories = GetAllDebugSnapshotDirectories();

            Visible = true;
            Enabled = true;
            WindowManager.Cursor.Visible = false;
            nativeCursorUsed = Game.IsMouseVisible;
            Game.IsMouseVisible = false;
            ProgramConstants.IsInGame = true;
            Game.TargetElapsedTime = TimeSpan.FromMilliseconds(1000.0 / POWER_SAVING_FPS);

            if (UserINISettings.Instance.MinimizeWindowsOnGameStart)
                WindowManager.MinimizeWindow();


        }

        private void SharedUILogic_GameProcessExited()
        {
            AddCallback(new Action(HandleGameProcessExited), null);
        }

        private void HandleGameProcessExited()
        {
            Visible = false;
            Enabled = false;
            if (nativeCursorUsed)
                Game.IsMouseVisible = true;
            else
                WindowManager.Cursor.Visible = true;
            ProgramConstants.IsInGame = false;
            Game.TargetElapsedTime = TimeSpan.FromMilliseconds(1000.0 / UserINISettings.Instance.ClientFPS);

            if (UserINISettings.Instance.MinimizeWindowsOnGameStart)
                WindowManager.MaximizeWindow();

            UserINISettings.Instance.ReloadSettings();

            if (UserINISettings.Instance.BorderlessWindowedClient)
            {
                // Hack：重新设置图形模式
                // 如果我们处于全屏模式且游戏内分辨率低于用户桌面分辨率，
                // Windows 会调整我们的窗口大小。
                // 游戏退出后，Windows 无法正确地将窗口恢复为
                // 覆盖整个屏幕，这会导致图形被拉伸，
                // 并且由于窗口管理器仍然认为使用的是原始分辨率，
                // 输入处理也会出问题。
                // 重新设置图形模式可以修复此问题。
                GameClass.SetGraphicsMode(WindowManager);
            }

            DateTime dtn = DateTime.Now;

            Task.Factory.StartNew(ProcessScreenshots);

            // TODO: Ares 调试日志处理应该在 Ares DLL 本身中解决。
            // 目前以下内容在此处理：
            // 1. 在崩溃和不同步时将 syringe.log 复制到调试快照目录。
            // 2. 在不同步时将 SYNCX.txt 从游戏目录移动到调试快照目录。
            // 3. 在不同步时创建调试快照目录并将 debug.log 复制到其中，即使没有创建完整崩溃转储。
            // 4. 处理崩溃时创建的空快照目录（如果调试日志被禁用）。

            string snapshotDirectory = GetNewestDebugSnapshotDirectory();
            bool snapshotCreated = snapshotDirectory != null;

            snapshotDirectory = snapshotDirectory ?? SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "debug", FormattableString.Invariant($"snapshot-{dtn.ToString("yyyyMMdd-HHmmss")}"));

            bool debugLogModified = false;
            FileInfo debugLogFileInfo = SafePath.GetFile(ProgramConstants.GamePath, "debug", "debug.log");
            DateTime lastWriteTime = new DateTime();

            if (debugLogFileInfo.Exists)
                lastWriteTime = debugLogFileInfo.LastAccessTime;

            if (!lastWriteTime.Equals(debugLogLastWriteTime))
            {
                debugLogModified = true;
                debugLogLastWriteTime = lastWriteTime;
            }

            if (CopySyncErrorLogs(snapshotDirectory, null) || snapshotCreated)
            {
                FileInfo snapShotDebugLogFileInfo = SafePath.GetFile(snapshotDirectory, "debug.log");

                if (debugLogFileInfo.Exists && !snapShotDebugLogFileInfo.Exists && debugLogModified)
                    File.Copy(debugLogFileInfo.FullName, snapShotDebugLogFileInfo.FullName);

                CopyErrorLog(snapshotDirectory, "syringe.log", null);
            }
        }

        /// <summary>
        /// 尝试将通用错误日志从游戏目录复制到另一个目录。
        /// </summary>
        /// <param name="directory">要复制错误日志到的目录。</param>
        /// <param name="filename">错误日志的文件名。</param>
        /// <param name="dateTime">要应用到文件名的时间戳。设为 null 则不应用时间戳。</param>
        /// <returns>如果错误日志已复制则为 true，否则为 false。</returns>
        private bool CopyErrorLog(string directory, string filename, DateTime? dateTime)
        {
            bool copied = false;

            try
            {
                FileInfo errorLogFileInfo = SafePath.GetFile(ProgramConstants.GamePath, filename);

                if (errorLogFileInfo.Exists)
                {
                    DirectoryInfo errorLogDirectoryInfo = SafePath.GetDirectory(directory);

                    if (!errorLogDirectoryInfo.Exists)
                        errorLogDirectoryInfo.Create();

                    Logger.Log("The game crashed! Copying " + filename + " file.");

                    string timeStamp = dateTime.HasValue ? dateTime.Value.ToString("_yyyy_MM_dd_HH_mm") : "";

                    string filenameCopy = Path.GetFileNameWithoutExtension(filename) +
                        timeStamp + Path.GetExtension(filename);

                    File.Copy(errorLogFileInfo.FullName, SafePath.CombineFilePath(directory, filenameCopy));
                    copied = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("An error occured while checking for " + filename + " file. Message: " + ex.Message);
            }
            return copied;
        }

        /// <summary>
        /// 尝试将同步错误日志从游戏目录复制到另一个目录。
        /// </summary>
        /// <param name="directory">要复制同步错误日志到的目录。</param>
        /// <param name="dateTime">要应用到文件名的时间戳。设为 null 则不应用时间戳。</param>
        /// <returns>如果复制了任何同步日志则为 true，否则为 false。</returns>
        private bool CopySyncErrorLogs(string directory, DateTime? dateTime)
        {
            bool copied = false;

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    string filename = "SYNC" + i + ".TXT";
                    FileInfo syncErrorLogFileInfo = SafePath.GetFile(ProgramConstants.GamePath, filename);

                    if (syncErrorLogFileInfo.Exists)
                    {
                        DirectoryInfo syncErrorLogDirectoryInfo = SafePath.GetDirectory(directory);

                        if (!syncErrorLogDirectoryInfo.Exists)
                            syncErrorLogDirectoryInfo.Create();

                        Logger.Log("There was a sync error! Copying file " + filename);

                        string timeStamp = dateTime.HasValue ? dateTime.Value.ToString("_yyyy_MM_dd_HH_mm") : "";

                        string filenameCopy = Path.GetFileNameWithoutExtension(filename) +
                            timeStamp + Path.GetExtension(filename);

                        File.Copy(syncErrorLogFileInfo.FullName, SafePath.CombineFilePath(directory, filenameCopy));
                        copied = true;
                        syncErrorLogFileInfo.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("An error occured while checking for SYNCX.TXT files. Message: " + ex.Message);
            }
            return copied;
        }

        /// <summary>
        /// 返回在 Ares 调试日志目录中找到的第一个在上次游戏启动后创建且不为空的调试快照目录。
        /// 此外，遇到的任何空快照目录都会被删除。
        /// </summary>
        /// <returns>调试快照目录的完整路径。如果未找到，则返回 null。</returns>
        private string GetNewestDebugSnapshotDirectory()
        {
            string snapshotDirectory = null;

            if (debugSnapshotDirectories != null)
            {
                var newDirectories = GetAllDebugSnapshotDirectories().Except(debugSnapshotDirectories);

                foreach (string directory in newDirectories)
                {
                    if (Directory.EnumerateFileSystemEntries(directory).Any())
                        snapshotDirectory = directory;
                    else
                    {
                        try
                        {
                            Directory.Delete(directory);
                        }
                        catch { }
                    }
                }
            }

            return snapshotDirectory;
        }

        /// <summary>
        /// 返回 Ares 调试日志目录中所有调试快照目录的列表。
        /// </summary>
        /// <returns>Ares 调试日志目录中所有调试快照目录的列表。如果未找到或遇到错误则返回空列表。</returns>
        private List<string> GetAllDebugSnapshotDirectories()
        {
            var directories = new List<string>();

            try
            {
                directories.AddRange(Directory.GetDirectories(SafePath.CombineDirectoryPath(ProgramConstants.GamePath, "debug"), "snapshot-*"));
            }
            catch { }

            return directories;
        }

        /// <summary>
        /// 将 BMP 截图转换为 PNG 并从游戏目录复制到 Screenshots 子目录。
        /// </summary>
        private void ProcessScreenshots()
        {
            IEnumerable<FileInfo> files = SafePath.GetDirectory(ProgramConstants.GamePath).EnumerateFiles("SCRN*.bmp");
            DirectoryInfo screenshotsDirectory = SafePath.GetDirectory(ProgramConstants.GamePath, "Screenshots");

            if (!screenshotsDirectory.Exists)
            {
                try
                {
                    screenshotsDirectory.Create();
                }
                catch (Exception ex)
                {
                    Logger.Log("ProcessScreenshots: An error occured trying to create Screenshots directory. Message: " + ex.Message);
                    return;
                }
            }

            foreach (FileInfo file in files)
            {
                try
                {
                    using FileStream stream = file.OpenRead();
                    using var image = Image.Load(stream);
                    FileInfo newFile = SafePath.GetFile(screenshotsDirectory.FullName, FormattableString.Invariant($"{Path.GetFileNameWithoutExtension(file.FullName)}.png"));
                    using FileStream newFileStream = newFile.OpenWrite();

                    image.SaveAsPng(newFileStream);
                }
                catch (Exception ex)
                {
                    Logger.Log("ProcessScreenshots: Error occured when trying to save " + Path.GetFileNameWithoutExtension(file.FullName) + ".png. Message: " + ex.Message);
                    continue;
                }

                Logger.Log("ProcessScreenshots: " + Path.GetFileNameWithoutExtension(file.FullName) + ".png has been saved to Screenshots directory.");
                file.Delete();
            }
        }
    }
}