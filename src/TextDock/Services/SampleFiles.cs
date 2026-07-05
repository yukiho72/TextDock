using System.IO;
using System.Text;

namespace TextDock.Services;

/// <summary>初回起動時に作成するサンプルファイル。言語設定に応じて日本語版/英語版を書き出す。</summary>
public static class SampleFiles
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void Create(string folder, string language)
    {
        var files = language == "ja" ? Japanese : English;
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(folder, name), content, Utf8NoBom);
    }

    private static readonly (string Name, string Content)[] Japanese =
    {
        ("_はじめに",
            "TextDock へようこそ！\n" +
            "このフォルダにあるテキストファイルが左のリストに表示されます\n" +
            "ファイルの各行が右のリストに表示され、ダブルクリックで対象アプリに貼り付けられます\n" +
            "ホットキー（既定: Ctrl+Space）でいつでも呼び出せます\n" +
            "右ペインの行はドラッグやCtrlクリックで複数選択して、まとめて貼り付けられます\n" +
            "メモはただのテキストファイルなので、他のエディタやツールと自由にやり取りできます\n" +
            "クリップボードの内容から新しいメモを作ることもできます（左ペインで右クリック）\n" +
            "下のフォルダボタンでメモフォルダを切り替えられます\n" +
            "このファイルは自由に編集・削除してかまいません\n"),

        ("_ショートカットキー",
            "Ctrl+Space : TextDock を表示 / 非表示（設定で変更可）\n" +
            "Esc : ウィンドウを閉じる\n" +
            "Ctrl+N : 新規メモを作成\n" +
            "F5 : 再読込\n" +
            "Ctrl+D : フォルダ切り替えメニューを開く\n" +
            "Ctrl+M : 貼り付けモードを変更\n" +
            "Enter（左ペイン）: 右ペインへ移動\n" +
            "Ctrl+E（左ペイン）: 選択中のメモを編集\n" +
            "F2（左ペイン）: 選択中のメモの名前を変更\n" +
            "Enter（右ペイン）: 選択行を貼り付け\n" +
            "Ctrl+C（右ペイン）: 選択行をコピー\n" +
            "Ctrl+V（右ペイン）: 選択行を貼り付け\n" +
            "Ctrl+S（編集画面）: 保存\n"),

        ("メール定型文",
            "お世話になっております。\n" +
            "いつもお世話になっております。\n" +
            "ご確認のほどよろしくお願いいたします。\n" +
            "お忙しいところ恐れ入りますが、よろしくお願いいたします。\n" +
            "引き続きよろしくお願いいたします。\n" +
            "取り急ぎご連絡まで。\n" +
            "ご不明な点がございましたらお気軽にお問い合わせください。\n"),

        ("PowerShellコマンド集",
            "Get-ChildItem | Sort-Object LastWriteTime -Descending | Select-Object -First 10\n" +
            "Get-Process | Sort-Object CPU -Descending | Select-Object -First 10\n" +
            "Get-Date -Format \"yyyy-MM-dd HH:mm\"\n" +
            "ipconfig /all\n" +
            "Test-NetConnection example.com\n" +
            "Get-History\n"),
    };

    private static readonly (string Name, string Content)[] English =
    {
        ("_Read me first",
            "Welcome to TextDock!\n" +
            "Text files in this folder appear in the left list\n" +
            "Each line of a file appears in the right list — double-click to paste it into the target app\n" +
            "Press the hotkey (default: Ctrl+Space) to open TextDock anytime\n" +
            "Select multiple lines in the right pane with drag or Ctrl+click to paste them together\n" +
            "Memos are plain text files, so you can freely exchange them with other editors and tools\n" +
            "You can also create a memo from the clipboard (right-click in the left pane)\n" +
            "Use the folder button at the bottom to switch memo folders\n" +
            "Feel free to edit or delete this file\n"),

        ("_Keyboard shortcuts",
            "Ctrl+Space : Show / hide TextDock (configurable)\n" +
            "Esc : Close the window\n" +
            "Ctrl+N : Create a new memo\n" +
            "F5 : Reload\n" +
            "Ctrl+D : Open the folder menu\n" +
            "Ctrl+M : Change paste mode\n" +
            "Enter (left pane) : Move to the right pane\n" +
            "Ctrl+E (left pane) : Edit the selected memo\n" +
            "F2 (left pane) : Rename the selected memo\n" +
            "Enter (right pane) : Paste selected lines\n" +
            "Ctrl+C (right pane) : Copy selected lines\n" +
            "Ctrl+V (right pane) : Paste selected lines\n" +
            "Ctrl+S (editor) : Save\n"),

        ("Email phrases",
            "Thank you for your email.\n" +
            "I hope this message finds you well.\n" +
            "Please let me know if you have any questions.\n" +
            "I look forward to hearing from you.\n" +
            "Thank you for your patience.\n" +
            "Best regards,\n"),

        ("PowerShell commands",
            "Get-ChildItem | Sort-Object LastWriteTime -Descending | Select-Object -First 10\n" +
            "Get-Process | Sort-Object CPU -Descending | Select-Object -First 10\n" +
            "Get-Date -Format \"yyyy-MM-dd HH:mm\"\n" +
            "ipconfig /all\n" +
            "Test-NetConnection example.com\n" +
            "Get-History\n"),
    };
}
