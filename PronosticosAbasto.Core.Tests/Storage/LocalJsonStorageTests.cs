using System.Text.Json;
using PronosticosAbasto.Core.Storage;

namespace PronosticosAbasto.Core.Tests.Storage;

public sealed class LocalJsonStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pa-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "data.json");

    [Fact]
    public void WriteAtomic_creates_the_directory_and_the_file()
    {
        LocalJsonStorage.WriteAtomic(FilePath, new Dictionary<string, int> { ["a"] = 1 }, new JsonSerializerOptions());

        Assert.True(File.Exists(FilePath));
        Assert.Equal(1, JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath))!["a"]);
    }

    [Fact]
    public void WriteAtomic_replaces_the_previous_content()
    {
        LocalJsonStorage.WriteAtomic(FilePath, new Dictionary<string, int> { ["a"] = 1 }, new JsonSerializerOptions());
        LocalJsonStorage.WriteAtomic(FilePath, new Dictionary<string, int> { ["b"] = 2 }, new JsonSerializerOptions());

        var content = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath))!;
        Assert.False(content.ContainsKey("a"));
        Assert.Equal(2, content["b"]);
    }

    [Fact]
    public void WriteAtomic_leaves_no_temporary_file_behind()
    {
        LocalJsonStorage.WriteAtomic(FilePath, new[] { 1, 2, 3 }, new JsonSerializerOptions());

        Assert.False(File.Exists(FilePath + ".tmp"));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void WriteAtomic_overwrites_a_temporary_file_left_by_a_previous_crash()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath + ".tmp", "{ basura a medio escribir");

        LocalJsonStorage.WriteAtomic(FilePath, new Dictionary<string, int> { ["a"] = 1 }, new JsonSerializerOptions());

        Assert.False(File.Exists(FilePath + ".tmp"));
        Assert.Equal(1, JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath))!["a"]);
    }

    [Fact]
    public void WriteAtomic_keeps_the_previous_file_intact_when_serialization_fails()
    {
        LocalJsonStorage.WriteAtomic(FilePath, new Dictionary<string, int> { ["bueno"] = 1 }, new JsonSerializerOptions());

        // Un ciclo de referencias hace fallar la serializacion a mitad de camino:
        // es lo mas cerca que se puede simular un corte durante la escritura.
        var loop = new Node();
        loop.Self = loop;

        Assert.ThrowsAny<JsonException>(() =>
            LocalJsonStorage.WriteAtomic(FilePath, loop, new JsonSerializerOptions { MaxDepth = 8 }));

        // El destino sigue siendo el JSON completo anterior, no uno truncado.
        var content = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath))!;
        Assert.Equal(1, content["bueno"]);
    }

    [Fact]
    public void PathFor_uses_a_single_local_data_folder()
    {
        var path = LocalJsonStorage.PathFor("archivo.json");

        Assert.Equal("archivo.json", Path.GetFileName(path));
        Assert.Equal(LocalJsonStorage.FolderName, Path.GetFileName(Path.GetDirectoryName(path)));
    }

    private sealed class Node
    {
        public Node? Self { get; set; }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
