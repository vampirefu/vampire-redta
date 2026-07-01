using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MapGenerator.Core
{
    public class MapThumbnailRenderer
    {
        public string MapRendererPath { get; set; }
        public string OutputPath { get; set; }
        public string GameResourcePath { get; set; }

        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        public MapThumbnailRenderer(string rendererPath, string gameResourcePath)
        {
            MapRendererPath = rendererPath;
            OutputPath = AppDomain.CurrentDomain.BaseDirectory;
            GameResourcePath = gameResourcePath;
        }

        protected virtual void OnProgressChanged(int progress, string message)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(progress, message));
        }

        public void SetOutputPath(string path)
        {
            OutputPath = path;
        }

        public void SetGameResourcePath(string path)
        {
            GameResourcePath = path;
        }

        public void RenderThumbnail(string mapFilePath, string outputName = "preview")
        {
            if (!File.Exists(mapFilePath))
            {
                throw new FileNotFoundException($"地图文件不存在: {mapFilePath}");
            }

            if (!Directory.Exists(MapRendererPath))
            {
                throw new DirectoryNotFoundException($"地图渲染器路径不存在: {MapRendererPath}");
            }

            string rendererExe = Path.Combine(MapRendererPath, "CNCMaps.Renderer.exe");
            if (!File.Exists(rendererExe))
            {
                throw new FileNotFoundException($"CNCMaps.Renderer.exe 不存在: {rendererExe}");
            }

            OnProgressChanged(0, "正在渲染缩略图...");

            string strCmdText = BuildCommand(mapFilePath, outputName);

            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = strCmdText;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    if (e.Data.Contains("Parsing"))
                    {
                        OnProgressChanged(20, "正在解析地图...");
                    }
                    else if (e.Data.Contains("Reading map"))
                    {
                        OnProgressChanged(40, "正在读取地图...");
                    }
                    else if (e.Data.Contains("Initializing"))
                    {
                        OnProgressChanged(60, "正在初始化渲染器...");
                    }
                    else if (e.Data.Contains("Rendering"))
                    {
                        OnProgressChanged(80, "正在渲染...");
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            int exitCode = process.ExitCode;
            string outputText = output.ToString();
            string errorText = error.ToString();

            process.Close();

            if (exitCode != 0)
            {
                string errorMessage = $"渲染失败，退出代码: {exitCode}";
                if (!string.IsNullOrEmpty(errorText))
                {
                    errorMessage += $"\n错误信息: {errorText}";
                }

                if (!string.IsNullOrEmpty(outputText))
                {
                    errorMessage += $"\n输出信息: {outputText}";
                }

                throw new Exception(errorMessage);
            }

            OnProgressChanged(90, "正在整理文件...");
            MoveThumbnailFile(outputName);
            OnProgressChanged(100, "完成!");
        }

        private void MoveThumbnailFile(string outputName)
        {
            string thumbnailPath = Path.Combine(OutputPath, $"thumb_{outputName}.png");
            string finalPath = Path.Combine(OutputPath, $"{outputName}.png");

            if (File.Exists(thumbnailPath))
            {
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }

                File.Move(thumbnailPath, finalPath);
            }
        }

        private string BuildCommand(string mapFilePath, string outputName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"/c cd /d \"{MapRendererPath}\" &&");
            sb.Append($" CNCMaps.Renderer.exe -i \"{mapFilePath}\" -o {outputName} -m \"{GameResourcePath.TrimEnd('\\')}\" -Y -z +(1280,0) --thumb-png --bkp ");

            return sb.ToString();
        }
    }
}
