using System.IO;
using TextDock.Services;

namespace TextDock.Tests;

public class SampleFilesTests : IDisposable
{
    private readonly string _dir;

    public SampleFilesTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TextDockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("ja")]
    [InlineData("en")]
    public void サンプルファイルが4つ作成される(string language)
    {
        SampleFiles.Create(_dir, language);

        var files = Directory.GetFiles(_dir);
        Assert.Equal(4, files.Length);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.False(string.IsNullOrWhiteSpace(content));
            Assert.DoesNotContain("[App_", content);
        }
    }

    [Fact]
    public void 日本語版は先頭アンダースコアのガイドを含む()
    {
        SampleFiles.Create(_dir, "ja");
        Assert.True(File.Exists(Path.Combine(_dir, "_はじめに")));
        Assert.True(File.Exists(Path.Combine(_dir, "_ショートカットキー")));
    }

    [Fact]
    public void 英語版は英語のガイドを含む()
    {
        SampleFiles.Create(_dir, "en");
        Assert.True(File.Exists(Path.Combine(_dir, "_Read me first")));
        Assert.True(File.Exists(Path.Combine(_dir, "_Keyboard shortcuts")));
    }
}
