namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public async Task WriteAllTextAsync(
        string path, 
        string content) => await File.WriteAllTextAsync(path, content);
    public async Task AppendAllTextAsync(
        string path, 
        string content) => await File.AppendAllTextAsync(path, content);
}