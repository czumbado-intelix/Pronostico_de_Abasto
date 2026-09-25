namespace PronosticosAbasto.ViewModels;

/// <summary>A list row that can be copied to the clipboard as tab-separated text.</summary>
public interface IClipboardRow
{
    string ToClipboardText();
}
