using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DXMainClient.Tests.DXGUI.Multiplayer.GameLobby;

public class GameLobbyBaseSourceTests
{
    [Fact]
    public void ReorderingMapPreviewBoxDoesNotInitializeItAgain()
    {
        string source = File.ReadAllText(GetSourcePath());

        Assert.DoesNotMatch(
            new Regex(@"RemoveChild\s*\(\s*MapPreviewBox\s*\)\s*;\s*AddChild\s*\(\s*MapPreviewBox\s*\)\s*;", RegexOptions.Singleline),
            source);
    }

    private static string GetSourcePath([CallerFilePath] string testFilePath = "")
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testFilePath)!,
            "..", "..", "..", ".."));

        return Path.Combine(projectRoot, "DXMainClient", "DXGUI", "Multiplayer", "GameLobby", "GameLobbyBase.cs");
    }
}
