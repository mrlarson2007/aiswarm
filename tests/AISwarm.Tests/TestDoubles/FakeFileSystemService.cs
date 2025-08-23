using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
/// Fake file system service for testing - stores files and directories in memory
/// </summary>
public class FakeFileSystemService : IFileSystemService
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileContents = new(StringComparer.OrdinalIgnoreCase);

    public bool DirectoryExists(string path) => _directories.Contains(Norm(path));
    public void CreateDirectory(string path) => _directories.Add(Norm(path));
    public bool FileExists(string path) => _files.Contains(Norm(path));

    public async Task<string> ReadAllTextAsync(string path)
    {
        var normalizedPath = Norm(path);
        if (_fileContents.TryGetValue(normalizedPath, out var content))
        {
            await Task.CompletedTask;
            return content;
        }
        throw new FileNotFoundException($"File not found: {path}");
    }

    public async Task WriteAllTextAsync(
        string path,
        string content)
    {
        var normalizedPath = Norm(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _directories.Add(Norm(dir));
        _files.Add(normalizedPath);
        _fileContents[normalizedPath] = content;
        await Task.CompletedTask;
    }

    public async Task AppendAllTextAsync(
        string path,
        string content)
    {
        var normalizedPath = Norm(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _directories.Add(Norm(dir));

        if (_fileContents.TryGetValue(normalizedPath, out var existingContent))
        {
            _fileContents[normalizedPath] = existingContent + content;
        }
        else
        {
            _files.Add(normalizedPath);
            _fileContents[normalizedPath] = content;
        }
        await Task.CompletedTask;
    }

    public void AddDirectory(string path) => _directories.Add(Norm(path));
    public void AddFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _directories.Add(Norm(dir));
        _files.Add(Norm(path));
        _fileContents.Add(path, string.Empty);
    }

    public string? GetFileContent(string path) => _fileContents.TryGetValue(Norm(path), out var content) ? content : null;

    private static string Norm(string p) => p.Replace('\\', '/');
}
