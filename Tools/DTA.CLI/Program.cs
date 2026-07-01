using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
class PlayerSpec
{
    public string Name { get; set; }
    public int Side { get; set; }
    public int Color { get; set; }
    public int? Start { get; set; }

    public PlayerSpec(string name, int side, int color, int? start = null)
    {
        Name = name;
        Side = side;
        Color = color;
        Start = start;
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var cmd = args[0];
        try
        {
            switch (cmd)
            {
                case "map:generate-spawnmap":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: map:generate-spawnmap <mapPath> --game-path <dir> [--out <file>] [--dry-run]");
                        return 2;
                    }
                    string mapPathG = args[1];
                    string gamePath = string.Empty;
                    string outPathG = string.Empty;
                    bool dryRunG = false;
                    for (int i = 2; i < args.Length; i++)
                    {
                        if (args[i] == "--game-path")
                        {
                            if (i + 1 < args.Length)
                            {
                                gamePath = args[i + 1];
                                i++;
                            }
                        }
                        else if (args[i] == "--out" || args[i] == "-o")
                        {
                            if (i + 1 < args.Length)
                            {
                                outPathG = args[i + 1];
                                i++;
                            }
                        }
                        else if (args[i] == "--dry-run")
                        {
                            dryRunG = true;
                        }
                    }
                    return MapGenerateSpawnmap(mapPathG, gamePath, outPathG, dryRunG);

                case "map:generate-spawnini":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Usage: map:generate-spawnini <mapPath> --game-path <dir> --player \"name,side,color[,start]\" [--player ...] [--ai <n>] [--seed <n>] [--out <file>] [--dry-run]");
                        return 2;
                    }
                    string mapPathS = args[1];
                    string gamePathS = string.Empty;
                    string outPathS = string.Empty;
                    bool dryRunS = false;
                    int aiCount = 0;
                    int? seed = null;
                    var playerSpecs = new List<string>();

                    for (int i = 2; i < args.Length; i++)
                    {
                        if (args[i] == "--game-path")
                        {
                            if (i + 1 < args.Length)
                            {
                                gamePathS = args[i + 1];
                                i++;
                            }
                        }
                        else if (args[i] == "--out" || args[i] == "-o")
                        {
                            if (i + 1 < args.Length)
                            {
                                outPathS = args[i + 1];
                                i++;
                            }
                        }
                        else if (args[i] == "--dry-run")
                        {
                            dryRunS = true;
                        }
                        else if (args[i] == "--ai")
                        {
                            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var v))
                            {
                                aiCount = v;
                                i++;
                            }
                        }
                        else if (args[i] == "--seed")
                        {
                            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var s))
                            {
                                seed = s;
                                i++;
                            }
                        }
                        else if (args[i] == "--player")
                        {
                            if (i + 1 < args.Length)
                            {
                                playerSpecs.Add(args[i + 1]);
                                i++;
                            }
                        }
                    }

                    var players = ParsePlayers(playerSpecs);
                    return MapGenerateSpawnini(mapPathS, gamePathS, players, aiCount, seed, outPathS, dryRunS);

                default:
                    Console.Error.WriteLine($"Unknown command: {cmd}");
                    PrintUsage();
                    return 3;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 99;
        }
    }

static void PrintUsage()
{
    Console.WriteLine("DTA.CLI - usage:\n  map:generate-spawnmap <mapPath> --game-path <dir> [--out <file>] [--dry-run]\n  map:generate-spawnini <mapPath> --game-path <dir> --player \"name,side,color[,start]\" [--player ...] [--ai <n>] [--seed <n>] [--out <file>] [--dry-run]\n");
}
static List<PlayerSpec> ParsePlayers(List<string> specs)
{
    var list = new List<PlayerSpec>();
    if (specs == null)
        return list;

    foreach (var s in specs)
    {
        if (string.IsNullOrWhiteSpace(s))
            continue;

        var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            continue;

        string name = parts[0];
        int side = 0;
        int color = 0;
        int? start = null;

        int.TryParse(parts[1], out side);
        int.TryParse(parts[2], out color);
        if (parts.Length > 3 && int.TryParse(parts[3], out var st))
            start = st;

        list.Add(new PlayerSpec(name, side, color, start));
    }

    return list;
}

static int MapGenerateSpawnmap(string mapPath, string gamePath, string outPath, bool dryRun)
{
    if (!File.Exists(mapPath))
    {
        Console.Error.WriteLine($"Map file not found: {mapPath}");
        return 1;
    }

    if (string.IsNullOrEmpty(gamePath) && string.IsNullOrEmpty(outPath))
    {
        Console.Error.WriteLine("Either --game-path or --out must be specified.");
        return 2;
    }

    string target = outPath;
    if (string.IsNullOrEmpty(target))
        target = Path.Combine(gamePath, "spawnmap.ini");

    var lines = File.ReadAllLines(mapPath).ToList();

    // Ensure [Basic] contains OriginalFilename
    int basicIndex = lines.FindIndex(l => l.Trim().Equals("[Basic]", StringComparison.OrdinalIgnoreCase));
    if (basicIndex >= 0)
    {
        int insertIndex = basicIndex + 1;
        int nextSection = lines.FindIndex(insertIndex, l => l.TrimStart().StartsWith("["));
        if (nextSection == -1) nextSection = lines.Count;

        bool found = false;
        for (int i = insertIndex; i < nextSection; i++)
        {
            if (lines[i].TrimStart().StartsWith("OriginalFilename", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"OriginalFilename={Path.GetFileName(mapPath)}";
                found = true;
                break;
            }
        }

        if (!found)
        {
            lines.Insert(insertIndex, $"OriginalFilename={Path.GetFileName(mapPath)}");
        }
    }
    else
    {
        // Add Basic section at the top
        lines.Insert(0, "");
        lines.Insert(0, $"OriginalFilename={Path.GetFileName(mapPath)}");
        lines.Insert(0, "[Basic]");
    }

    if (dryRun)
    {
        Console.WriteLine(string.Join(Environment.NewLine, lines));
        return 0;
    }

    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? Directory.GetCurrentDirectory());
        File.WriteAllLines(target, lines, Encoding.UTF8);
        Console.WriteLine($"Wrote spawnmap.ini to: {target}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error writing spawnmap.ini: {ex.Message}");
        return 3;
    }
}

static int MapGenerateSpawnini(string mapPath, string gamePath, List<PlayerSpec> players, int aiCount, int? seed, string outPath, bool dryRun)
{
    if (!File.Exists(mapPath))
    {
        Console.Error.WriteLine($"Map file not found: {mapPath}");
        return 1;
    }

    if (players == null || players.Count == 0)
    {
        Console.Error.WriteLine("At least one --player must be provided.");
        return 2;
    }

    if (string.IsNullOrEmpty(gamePath) && string.IsNullOrEmpty(outPath))
    {
        Console.Error.WriteLine("Either --game-path or --out must be specified.");
        return 2;
    }

    string target = outPath;
    if (string.IsNullOrEmpty(target))
        target = Path.Combine(gamePath, "spawn.ini");

    var sb = new StringBuilder();

    sb.AppendLine("[Settings]");
    var host = players[0];
    sb.AppendLine($"Name={host.Name}");
    sb.AppendLine($"Scenario=spawnmap.ini");
    sb.AppendLine($"UIMapName={Path.GetFileNameWithoutExtension(mapPath)}");
    sb.AppendLine($"PlayerCount={players.Count}");
    sb.AppendLine($"Side={host.Side}");
    sb.AppendLine($"Color={host.Color}");
    sb.AppendLine($"AIPlayers={aiCount}");
    int seedVal = seed ?? new Random().Next();
    sb.AppendLine($"Seed={seedVal}");
    sb.AppendLine();

    int otherId = 1;
    for (int i = 1; i < players.Count; i++)
    {
        var p = players[i];
        sb.AppendLine($"[Other{otherId}]");
        sb.AppendLine($"Name={p.Name}");
        sb.AppendLine($"Side={p.Side}");
        sb.AppendLine($"Color={p.Color}");
        sb.AppendLine();
        otherId++;
    }

    if (dryRun)
    {
        Console.WriteLine(sb.ToString());
        return 0;
    }

    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(target, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Wrote spawn.ini to: {target}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error writing spawn.ini: {ex.Message}");
        return 3;
    }
    }
}