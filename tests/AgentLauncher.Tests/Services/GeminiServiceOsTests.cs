using AgentLauncher.Services;
using AgentLauncher.Services.External;
using Moq;
using Shouldly;

namespace AgentLauncher.Tests.Services;

public class GeminiServiceOsTests
{
    private class FakeProcessLauncher : IProcessLauncher
    {
        public List<(string File, string Args, string Cwd, int? Timeout, bool Capture)> Runs = new();
        public List<(string File, string Args, string Cwd)> Interactive = new();
        public Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, int? timeoutMs = null, bool captureOutput = true)
        {
            Runs.Add((fileName, arguments, workingDirectory, timeoutMs, captureOutput));
            return Task.FromResult(new ProcessResult(true, "Gemini CLI version 1.0", string.Empty, 0));
        }
        public bool StartInteractive(string fileName, string arguments, string workingDirectory)
        {
            Interactive.Add((fileName, arguments, workingDirectory));
            return true;
        }
    }

    private class FakeOsService : IOperatingSystemService
    {
        public bool Windows { get; set; }
        public bool Mac { get; set; }
        public bool Linux { get; set; }
        public bool IsWindows() => Windows;
        public bool IsMacOS() => Mac;
        public bool IsLinux() => Linux;
    }

    private GeminiService Create(FakeProcessLauncher proc, FakeOsService os) => new(proc, os);

    [Fact]
    public async Task VersionCheck_Windows_UsesPwsh()
    {
        var proc = new FakeProcessLauncher();
        var os = new FakeOsService { Windows = true };
        var svc = Create(proc, os);
        (await svc.IsGeminiCliAvailableAsync()).ShouldBeTrue();
        proc.Runs.ShouldContain(r => r.File.EndsWith("pwsh.exe"));
    }

    [Fact]
    public async Task VersionCheck_Linux_UsesSh()
    {
        var proc = new FakeProcessLauncher();
        var os = new FakeOsService { Linux = true };
        var svc = Create(proc, os);
        await svc.GetGeminiVersionAsync();
        proc.Runs.ShouldContain(r => r.File == "/bin/sh");
    }

    [Fact]
    public void Launch_Windows_UsesPwshInteractive()
    {
        var proc = new FakeProcessLauncher();
        var os = new FakeOsService { Windows = true };
        var svc = Create(proc, os);
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "test");
            svc.LaunchInteractiveAsync(tmp, null, Path.GetDirectoryName(tmp)).GetAwaiter().GetResult();
            proc.Interactive.ShouldContain(i => i.File.EndsWith("pwsh.exe"));
        }
        finally { File.Delete(tmp); }
    }
}
