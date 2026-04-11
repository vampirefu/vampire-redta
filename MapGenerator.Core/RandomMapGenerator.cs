using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MapGenerator.Core
{
    public class RandomMapGenerator
    {
        public string RandomMapGeneratorPath { get; set; }
        public string MapRendererPath { get; set; }
        public string OutputPath { get; set; }
        public string GameBasePath { get; set; }
        public string GameResourcePath { get; set; }

        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        public RandomMapGenerator(string toolPath, string gameResourcePath)
        {
            RandomMapGeneratorPath = toolPath;
            MapRendererPath = Path.Combine(toolPath, "Map Renderer");
            OutputPath = AppDomain.CurrentDomain.BaseDirectory;
            GameBasePath = Directory.GetParent(toolPath)?.FullName ?? toolPath;
            GameBasePath = Directory.GetParent(GameBasePath)?.FullName ?? GameBasePath;
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

        public void GenerateRandomMap(RandomMapOptions options)
        {
            ValidatePaths();

            OnProgressChanged(0, "正在生成随机地图...");

            string strCmdText = BuildCommand(options);

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
                    if (e.Data.Contains("Generating random map"))
                    {
                        OnProgressChanged(20, "正在生成地图数据...");
                    }
                    else if (e.Data.Contains("Parsing"))
                    {
                        OnProgressChanged(50, "正在解析地图...");
                    }
                    else if (e.Data.Contains("Reading map"))
                    {
                        OnProgressChanged(60, "正在读取地图...");
                    }
                    else if (e.Data.Contains("Initializing"))
                    {
                        OnProgressChanged(70, "正在初始化渲染器...");
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
                string errorMessage = $"命令执行失败，退出代码: {exitCode}";
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

            OnProgressChanged(90, "正在复制文件...");
            CopyGeneratedFiles();
            OnProgressChanged(100, "完成!");
        }

        private void ValidatePaths()
        {
            if (!Directory.Exists(RandomMapGeneratorPath))
            {
                throw new DirectoryNotFoundException(
                    $"随机地图生成器路径不存在: {RandomMapGeneratorPath}");
            }

            if (!Directory.Exists(MapRendererPath))
            {
                throw new DirectoryNotFoundException($"地图渲染器路径不存在: {MapRendererPath}");
            }
        }

        private void CopyGeneratedFiles()
        {
            string sourceMapPath = Path.Combine(GameBasePath, "Maps", "Custom", "随机地图.map");
            string sourceThumbnailPath = Path.Combine(GameBasePath, "Maps", "Custom", "thumb_随机地图.png");

            if (!File.Exists(sourceMapPath))
            {
                throw new FileNotFoundException($"地图文件生成失败: {sourceMapPath}");
            }

            string destMapDir = Path.Combine(OutputPath, "Maps", "Custom");
            if (!Directory.Exists(destMapDir))
            {
                Directory.CreateDirectory(destMapDir);
            }

            string destMapPath = Path.Combine(destMapDir, "随机地图.map");
            string destThumbnailPath = Path.Combine(destMapDir, "随机地图.png");

            if (File.Exists(destMapPath))
            {
                File.Delete(destMapPath);
            }

            File.Copy(sourceMapPath, destMapPath, true);

            if (File.Exists(sourceThumbnailPath))
            {
                if (File.Exists(destThumbnailPath))
                {
                    File.Delete(destThumbnailPath);
                }

                File.Copy(sourceThumbnailPath, destThumbnailPath, true);
            }
        }

        private string BuildCommand(RandomMapOptions options)
        {
            Random r = new Random();
            string climate = options.Climate;
            if (climate == "Random")
            {
                string[] climates = { "TEMPERATE", "TEMPERATE_Islands", "NEWURBAN", "DESERT" };
                climate = climates[r.Next(climates.Length)];
            }

            int sizex = 35 * (options.Size + 1) + r.Next(30, 50);
            int sizey = 35 * (options.Size + 1) + r.Next(30, 50);

            string[] people = GetPeople(options.PlayerCount);
            string damage = options.RandomBuildingDamage ? "-d" : string.Empty;

            string mapFilePath = Path.Combine(GameBasePath, "Maps", "Custom", "随机地图.map");

            StringBuilder sb = new StringBuilder();
            sb.Append($"/c cd /d \"{RandomMapGeneratorPath}\" &&");
            sb.Append($" RandomMapGenerator.exe -w {sizex} -h {sizey} --nwp {people[0]} --sep {people[1]} --nep {people[2]} --swp {people[3]} --sp {people[4]} --wp {people[5]} --ep {people[6]} --np {people[7]} {damage} --type {climate} -g standard &&");
            sb.Append($" cd \"{MapRendererPath}\" && CNCMaps.Renderer.exe -i \"{mapFilePath}\" -o 随机地图 -m \"{GameResourcePath.TrimEnd('\\')}\" -Y -z +(1280,0) --thumb-png --bkp ");

            return sb.ToString();
        }

        private static string[] GetPeople(int playerCount)
        {
            int[] p = { 0, 0, 0, 0, 0, 0, 0, 0 };
            Random r = new Random();
            int current = playerCount;

            while (current > 0)
            {
                p[r.Next(8)]++;
                current--;
            }

            return Array.ConvertAll(p, x => x.ToString());
        }
    }

    public class RandomMapOptions
    {
        public string Climate { get; set; } = "Random";
        public int PlayerCount { get; set; } = 2;
        public int Size { get; set; } = 1;
        public bool RandomBuildingDamage { get; set; } = false;
    }

    public class ProgressEventArgs : EventArgs
    {
        public int Progress { get; }
        public string Message { get; }

        public ProgressEventArgs(int progress, string message)
        {
            Progress = progress;
            Message = message;
        }
    }
}
