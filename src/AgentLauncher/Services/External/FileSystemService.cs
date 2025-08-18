namespace AgentLauncher.Services.External;

/// <inheritdoc />
public class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
}
