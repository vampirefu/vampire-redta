using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Png;
using MapGenerator.Core;

namespace MapPreviewTester
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string mapPath = null;
            if (args.Length > 0)
            {
                mapPath = args[0];
            }
            else
            {
                // Try to locate a map under DXMainClient/Resources/Maps
                string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
                string searchDir = Path.Combine(repoRoot, "DXMainClient", "Resources", "Maps");
                if (Directory.Exists(searchDir))
                {
                    var files = Directory.GetFiles(searchDir, "*.map", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        mapPath = files[0];
                    }
                }
            }

            if (string.IsNullOrEmpty(mapPath) || !File.Exists(mapPath))
            {
                Console.Error.WriteLine("Map file not found. Provide a path as the first argument or ensure DXMainClient/Resources/Maps contains .map files.");
                return 1;
            }

            Console.WriteLine($"Using map: {mapPath}");

            var image = MapPreviewExtractor.ExtractMapPreview(mapPath);
            if (image == null)
            {
                Console.WriteLine("No preview extracted from map.");
                return 0;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(mapPath) ?? Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(mapPath) + "_extracted.png");
            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                image.Save(fs, new PngEncoder());
            }

            Console.WriteLine($"Saved preview to: {outputPath}");
            return 0;
        }
    }
}
