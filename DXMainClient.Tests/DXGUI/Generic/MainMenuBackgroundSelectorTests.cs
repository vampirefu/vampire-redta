using DTAClient.DXGUI.Generic;
using Xunit;

namespace DXMainClient.Tests.DXGUI.Generic;

public class MainMenuBackgroundSelectorTests
{
    [Fact]
    public void SelectsGifWhenMainMenuGifExists()
    {
        string selectedPath = MainMenuBackgroundSelector.Select(path => path == "MainMenu/mainmenubg.gif");

        Assert.Equal("MainMenu/mainmenubg.gif", selectedPath);
    }

    [Fact]
    public void FallsBackToPngWhenMainMenuGifDoesNotExist()
    {
        string selectedPath = MainMenuBackgroundSelector.Select(_ => false);

        Assert.Equal("MainMenu/mainmenubg.png", selectedPath);
    }
}
