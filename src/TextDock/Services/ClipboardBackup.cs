using System.Collections.Specialized;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using TextDataFormat = System.Windows.TextDataFormat;

namespace TextDock.Services;

/// <summary>貼り付け時のクリップボード保護（仕様書14章）。テキスト・HTML・画像・ファイルコピー情報を退避・復元する。</summary>
public class ClipboardBackup
{
    private string? _text;
    private string? _html;
    private BitmapSource? _image;
    private StringCollection? _files;

    public static ClipboardBackup Capture()
    {
        var backup = new ClipboardBackup();
        try
        {
            if (Clipboard.ContainsText(TextDataFormat.Html))
                backup._html = Clipboard.GetText(TextDataFormat.Html);
            if (Clipboard.ContainsText())
                backup._text = Clipboard.GetText();
            if (Clipboard.ContainsImage())
                backup._image = Clipboard.GetImage();
            if (Clipboard.ContainsFileDropList())
                backup._files = Clipboard.GetFileDropList();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // 他プロセスがクリップボードをロック中。退避できた分のみ保持する
        }
        return backup;
    }

    public void Restore()
    {
        var data = new DataObject();
        var hasData = false;

        if (_html != null) { data.SetData(DataFormats.Html, _html); hasData = true; }
        if (_text != null) { data.SetData(DataFormats.UnicodeText, _text); hasData = true; }
        if (_image != null) { data.SetImage(_image); hasData = true; }
        if (_files != null) { data.SetFileDropList(_files); hasData = true; }

        if (!hasData)
            return;

        try
        {
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // 復元失敗は致命的ではないため無視する
        }
    }
}
