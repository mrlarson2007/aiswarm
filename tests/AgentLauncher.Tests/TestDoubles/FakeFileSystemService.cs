using AgentLauncher.Services;

namespace AgentLauncher.Tests.TestDoubles;

public class FakeFileSystemService : IFileSystemService
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

    public bool DirectoryExists(string path) => _directories.Contains(Norm(path));
    public void CreateDirectory(string path) => _directories.Add(Norm(path));
    public bool FileExists(string path) => _files.Contains(Norm(path));

    public void AddDirectory(string path) => _directories.Add(Norm(path));
    public void AddFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) _directories.Add(Norm(dir));
        _files.Add(Norm(path));
    }

    private static string Norm(string p) => p.Replace('\\', '/');
}
