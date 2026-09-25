using Windows.Storage;
using Windows.Storage.Pickers;

namespace PronosticosAbasto.Services;

public sealed class WorkbookFileDialogService
{
    public async Task<StorageFile?> PickWorkbookAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".xlsx");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);

        return await picker.PickSingleFileAsync();
    }

    public async Task<StorageFile?> PickExportFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".xlsx",
        };
        picker.FileTypeChoices.Add("Libro de Excel", new List<string> { ".xlsx" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);

        return await picker.PickSaveFileAsync();
    }
}
