namespace AISwarm.Infrastructure;

/// <summary>
/// Minimal abstraction for file system operations to facilitate testing code
/// paths that depend on directory or file existence without hitting disk.
/// </summary>
public interface IFileSystemService
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string path);
    Task WriteAllTextAsync(
        string path, 
        string content);
    Task AppendAllTextAsync(
        string path, 
        string content);
}