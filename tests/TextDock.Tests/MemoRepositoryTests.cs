using System.IO;
using System.Text;
using TextDock.Services;

namespace TextDock.Tests;

public class MemoRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly MemoRepository _repo;

    public MemoRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TextDockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _repo = new MemoRepository(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteFile(string name, string content) =>
        File.WriteAllText(Path.Combine(_dir, name), content, new UTF8Encoding(false));

    [Fact]
    public void 一覧はファイル名昇順で大文字小文字を区別しない()
    {
        WriteFile("banana", "b");
        WriteFile("Apple.txt", "a");
        WriteFile("cherry.md", "c");

        var names = _repo.ListMemos();

        Assert.Equal(new[] { "Apple.txt", "banana", "cherry.md" }, names);
    }

    [Fact]
    public void バイナリファイルは一覧から除外される()
    {
        WriteFile("text.txt", "hello");
        File.WriteAllBytes(Path.Combine(_dir, "binary.png"), new byte[] { 0x89, 0x50, 0x00, 0x47 });

        var names = _repo.ListMemos();

        Assert.Equal(new[] { "text.txt" }, names);
    }

    [Fact]
    public void 行読み込みはmaxLinesで打ち切られる()
    {
        WriteFile("many", string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line{i}")));

        var lines = _repo.LoadLines("many", maxLines: 100);

        Assert.Equal(100, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line100", lines[99]);
    }

    [Fact]
    public void BOM付きUTF8はBOMが除去されて読み込まれる()
    {
        File.WriteAllText(Path.Combine(_dir, "bom"), "first\nsecond", new UTF8Encoding(true));

        var lines = _repo.LoadLines("bom", maxLines: 100);

        Assert.Equal("first", lines[0]);
    }

    [Fact]
    public void CRLF改行も読み込める()
    {
        WriteFile("crlf", "a\r\nb\r\nc");

        var lines = _repo.LoadLines("crlf", maxLines: 100);

        Assert.Equal(new[] { "a", "b", "c" }, lines);
    }

    [Fact]
    public void 空行も1エントリとして読み込まれる()
    {
        WriteFile("blank", "a\n\nc");

        var lines = _repo.LoadLines("blank", maxLines: 100);

        Assert.Equal(new[] { "a", "", "c" }, lines);
    }

    [Fact]
    public void 保存はUTF8BOMなしLF改行で書き込まれる()
    {
        _repo.SaveText("out", "a\r\nb\r\nc");

        var bytes = File.ReadAllBytes(Path.Combine(_dir, "out"));

        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal("a\nb\nc", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void 新規作成で同名が存在する場合は例外を投げる()
    {
        WriteFile("dup", "x");

        Assert.Throws<MemoValidationException>(() => _repo.Create("dup", "y"));
    }

    [Fact]
    public void 禁止文字を含むファイル名は例外を投げる()
    {
        Assert.Throws<MemoValidationException>(() => _repo.Create("a/b", "x"));
        Assert.Throws<MemoValidationException>(() => _repo.Create("a:b", "x"));
        Assert.Throws<MemoValidationException>(() => _repo.Create("", "x"));
    }

    [Fact]
    public void 名前変更で同名が存在する場合は例外を投げる()
    {
        WriteFile("from", "x");
        WriteFile("to", "y");

        Assert.Throws<MemoValidationException>(() => _repo.Rename("from", "to"));
    }

    [Fact]
    public void 名前変更が成功する()
    {
        WriteFile("old", "content");

        _repo.Rename("old", "new");

        Assert.False(File.Exists(Path.Combine(_dir, "old")));
        Assert.Equal("content", File.ReadAllText(Path.Combine(_dir, "new")));
    }

    [Fact]
    public void 削除でファイルが消える()
    {
        WriteFile("doomed", "x");

        _repo.Delete("doomed");

        Assert.False(File.Exists(Path.Combine(_dir, "doomed")));
    }

    [Fact]
    public void 拡張子は自動付与されない()
    {
        _repo.Create("noext", "x");

        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "noext" }, files);
    }
}
