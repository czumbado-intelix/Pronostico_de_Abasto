using Windows.Storage;
using Windows.Storage.Pickers;

namespace PronosticosAbasto.Services;

/// <summary>
/// Pickers de Excel de la app.
/// </summary>
/// <remarks>
/// Los comandos que abren un picker solo se protegen con <c>IsBusy</c>, que aun
/// no esta activo mientras el dialogo esta abierto. Un doble clic llegaba a
/// abrir dos dialogos sobre la misma ventana, asi que el guard vive aqui: una
/// sola seleccion de archivo a la vez, sin importar el comando que la pida.
/// </remarks>
public sealed class WorkbookFileDialogService
{
    private bool _isPickerOpen;

    public async Task<StorageFile?> PickWorkbookAsync()
    {
        if (_isPickerOpen)
        {
            return null;
        }

        _isPickerOpen = true;
        try
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
        finally
        {
            _isPickerOpen = false;
        }
    }

    public async Task<StorageFile?> PickExportFileAsync(string suggestedFileName)
    {
        if (_isPickerOpen)
        {
            return null;
        }

        _isPickerOpen = true;
        try
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
        finally
        {
            _isPickerOpen = false;
        }
    }
}
