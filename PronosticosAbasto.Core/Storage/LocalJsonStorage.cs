using System.IO;
using System.Text.Json;

namespace PronosticosAbasto.Core.Storage;

/// <summary>
/// Utilidades compartidas por los stores locales de
/// <c>%LOCALAPPDATA%\PronosticosAbasto</c>.
/// </summary>
/// <remarks>
/// <para>
/// La escritura es <b>atomica</b>: primero se serializa a un archivo temporal
/// en la misma carpeta y solo cuando termino bien se reemplaza el destino. Con
/// <c>File.WriteAllText</c> directo, un cierre o apagon a media escritura dejaba
/// el JSON truncado; el <c>Load()</c> de cada store atrapa el
/// <see cref="JsonException"/> y arranca vacio, asi que el operador perdia el
/// transito o la configuracion <i>sin ningun aviso</i>.
/// </para>
/// </remarks>
public static class LocalJsonStorage
{
    /// <summary>Carpeta unica de datos locales de la app.</summary>
    public const string FolderName = "PronosticosAbasto";

    /// <summary>Ruta completa de un archivo dentro de la carpeta de datos locales.</summary>
    public static string PathFor(string fileName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName,
            fileName);

    /// <summary>
    /// Serializa <paramref name="payload"/> y lo deja en <paramref name="filePath"/>
    /// sin exponer nunca un archivo a medio escribir.
    /// </summary>
    public static void WriteAtomic<T>(string filePath, T payload, JsonSerializerOptions options)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Mismo directorio que el destino: File.Move solo es atomico si origen y
        // destino estan en el mismo volumen.
        var temporaryPath = filePath + ".tmp";

        // Serializar primero y escribir despues: si la serializacion falla, el
        // archivo bueno anterior queda intacto.
        var json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(temporaryPath, json);

        // File.Move(overwrite: true) usa MoveFileEx con MOVEFILE_REPLACE_EXISTING,
        // atomico dentro del mismo volumen. No se usa File.Replace: en Windows
        // falla con "No se puede quitar el archivo para ser reemplazado" cuando
        // algo mas (antivirus, indexador) toca el destino, y ese IOException lo
        // tragaba el catch del store, dejando el guardado en nada sin avisar.
        File.Move(temporaryPath, filePath, overwrite: true);
    }
}
