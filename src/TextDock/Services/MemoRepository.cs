using System.IO;
using System.Text;

namespace TextDock.Services;

public class MemoValidationException : Exception
{
    public MemoValidationException(string message) : base(message) { }
}

public class MemoRepository
{
    private static readonly char[] InvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private const int BinaryCheckBytes = 8000;

    private readonly string _folder;

    public MemoRepository(string folder)
    {
        _folder = folder;
    }

    public List<string> ListMemos()
    {
        return Directory.GetFiles(_folder)
            .Where(IsTextFile)
            .Select(Path.GetFileName)
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<string> LoadLines(string name, int maxLines)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(PathOf(name), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while (lines.Count < maxLines && (line = reader.ReadLine()) != null)
            lines.Add(line);
        return lines;
    }

    public string LoadText(string name)
    {
        using var reader = new StreamReader(PathOf(name), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public void SaveText(string name, string text)
    {
        File.WriteAllText(PathOf(name), text.Replace("\r\n", "\n"), Utf8NoBom);
    }

    public void Create(string name, string text)
    {
        ValidateName(name);
        if (File.Exists(PathOf(name)))
            throw new MemoValidationException($"同名のファイルが既に存在します: {name}");
        SaveText(name, text);
    }

    public void Rename(string oldName, string newName)
    {
        ValidateName(newName);
        if (File.Exists(PathOf(newName)))
            throw new MemoValidationException($"同名のファイルが既に存在します: {newName}");
        File.Move(PathOf(oldName), PathOf(newName));
    }

    public void Delete(string name)
    {
        File.Delete(PathOf(name));
    }

    public bool Exists(string name) => File.Exists(PathOf(name));

    public void AppendText(string name, string text)
    {
        var path = PathOf(name);
        var existing = File.ReadAllText(path);
        var normalized = text.Replace("\r\n", "\n");
        if (existing.Length > 0 && !existing.EndsWith('\n'))
            normalized = "\n" + normalized;
        File.AppendAllText(path, normalized, Utf8NoBom);
    }

    public string NextPasteName()
    {
        var files = Directory.GetFiles(_folder)
            .Select(Path.GetFileName)
            .Where(n => n != null && n.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToList();

        if (files.Count == 0)
            return "PASTE_0001";

        long max = 0;
        foreach (var f in files)
        {
            var numPart = f.Substring("PASTE_".Length);
            if (long.TryParse(numPart.Replace("_", ""), out var n) && n > max)
                max = n;
        }

        var next = max + 1;
        if (next <= 9999)
            return $"PASTE_{next:D4}";
        return $"PASTE_{next / 10000:D4}_{next % 10000:D4}";
    }

    private string PathOf(string name) => Path.Combine(_folder, name);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new MemoValidationException("ファイル名を入力してください。");
        if (name.IndexOfAny(InvalidChars) >= 0)
            throw new MemoValidationException("ファイル名に使用できない文字が含まれています: \\ / : * ? \" < > |");
    }

    private static bool IsTextFile(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[BinaryCheckBytes];
        var read = stream.Read(buffer, 0, buffer.Length);
        for (var i = 0; i < read; i++)
            if (buffer[i] == 0x00)
                return false;
        return true;
    }
}
