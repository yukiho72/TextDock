namespace TextDock.Models;

public class AppSettings
{
    public string MemoFolder { get; set; } = "";
    public string Hotkey { get; set; } = "Ctrl+Space";
    public string Theme { get; set; } = "Dark";
    public string FontName { get; set; } = "Meiryo UI";
    public int FontSize { get; set; } = 12;
    public string ColorBackground { get; set; } = "#1E1E1E";
    public string ColorText { get; set; } = "#D4D4D4";
    public string ColorSelection { get; set; } = "#264F78";
    public string DefaultPasteMethod { get; set; } = "Clipboard";
    public bool ClipboardProtection { get; set; } = true;
    public bool CloseAfterPaste { get; set; } = true;
    public int MaxLines { get; set; } = 10000;
    public int ClipboardDelay { get; set; } = 100;
    public int WmcharDelay { get; set; } = 20;
    public int SendinputDelay { get; set; } = 20;
    public Dictionary<string, string> AppPasteSettings { get; set; } = new();
    public List<string> RecentFolders { get; set; } = new();
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 500;
    public int EditorWidth { get; set; } = 600;
    public int EditorHeight { get; set; } = 400;
    public double PaneRatio { get; set; } = 0.35;
    public bool MemoSearchPartial { get; set; } = false;
    public bool LineSearchPartial { get; set; } = true;
    public bool AppendNewlineOnPaste { get; set; } = false;
    public bool AppendNewlineOnPowerShell { get; set; } = false;
    public string Language { get; set; } = "";
}
