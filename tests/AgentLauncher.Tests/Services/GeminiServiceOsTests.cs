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

    private class FakeTerminal : AgentLauncher.Services.Terminals.IInteractiveTerminalService
    {
        public (string shell, string args) VersionTuple = ("/bin/sh", "-c 'gemini --version'");
        public List<(string Args, string Cwd)> Launches = new();
        public bool LaunchTerminalInteractive(string command, string workingDirectory)
        {
            Launches.Add((command, workingDirectory));
            return true;
        }
        public (string shell, string args) BuildVersionCheck() => VersionTuple;
    }

    private GeminiService Create(FakeProcessLauncher proc, FakeTerminal term) => new(proc, term);

    [Fact]
    public async Task VersionCheck_Windows_UsesPwsh()
    {
        var proc = new FakeProcessLauncher();
    var term = new FakeTerminal { VersionTuple = ("pwsh.exe", "-Command \"gemini --version\"") };
    var svc = Create(proc, term);
        (await svc.IsGeminiCliAvailableAsync()).ShouldBeTrue();
        proc.Runs.ShouldContain(r => r.File.EndsWith("pwsh.exe"));
    }

    [Fact]
    public async Task VersionCheck_Linux_UsesSh()
    {
        var proc = new FakeProcessLauncher();
    var term = new FakeTerminal { VersionTuple = ("/bin/sh", "-c 'gemini --version'") };
    var svc = Create(proc, term);
        await svc.GetGeminiVersionAsync();
        proc.Runs.ShouldContain(r => r.File == "/bin/sh");
    }

    [Fact]
    public async Task Launch_Windows_UsesPwshInteractive()
    {
        var proc = new FakeProcessLauncher();
        var term = new AgentLauncher.Services.Terminals.WindowsTerminalService(proc);
        var svc = new GeminiService(proc, term);
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "test");
            await svc.LaunchInteractiveAsync(tmp, null, Path.GetDirectoryName(tmp));
            proc.Interactive.ShouldContain(i => i.File.EndsWith("pwsh.exe"));
        }
        finally { File.Delete(tmp); }
    }
}
