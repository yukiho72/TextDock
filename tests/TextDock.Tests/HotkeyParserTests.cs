using TextDock.Services;

namespace TextDock.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void CtrlSpaceをパースできる()
    {
        var ok = HotkeyManager.TryParse("Ctrl+Space", out var mods, out var vk);

        Assert.True(ok);
        Assert.Equal(HotkeyManager.MOD_CONTROL, mods);
        Assert.Equal(0x20u, vk);
    }

    [Fact]
    public void CtrlShiftF1をパースできる()
    {
        var ok = HotkeyManager.TryParse("Ctrl+Shift+F1", out var mods, out var vk);

        Assert.True(ok);
        Assert.Equal(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, mods);
        Assert.Equal(0x70u, vk);
    }

    [Fact]
    public void AltAをパースできる()
    {
        var ok = HotkeyManager.TryParse("Alt+A", out var mods, out var vk);

        Assert.True(ok);
        Assert.Equal(HotkeyManager.MOD_ALT, mods);
        Assert.Equal((uint)'A', vk);
    }

    [Fact]
    public void WinZをパースできる()
    {
        var ok = HotkeyManager.TryParse("Win+Z", out var mods, out var vk);

        Assert.True(ok);
        Assert.Equal(HotkeyManager.MOD_WIN, mods);
        Assert.Equal((uint)'Z', vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Foo+Bar")]
    [InlineData("Ctrl")]
    public void 不正な文字列はfalseを返す(string input)
    {
        Assert.False(HotkeyManager.TryParse(input, out _, out _));
    }
}
