using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AISwarm.Infrastructure.OpenCode;

public record OpenCodeModels(string Version, IReadOnlyList<string> Models);

public record OpenCodeResult(int ExitCode, string StandardOutput, string StandardError);

public record OpenCodeProcessRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string? StandardInput = null);

public interface IOpenCodeProcessRunner
{
    Task<OpenCodeResult> RunAsync(
        OpenCodeProcessRequest request,
        CancellationToken cancellationToken = default);
}

public interface IOpenCodeCli
{
    Task<OpenCodeModels> ListModelsAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<OpenCodeResult> RunAsync(
        string instructions,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}

public class OpenCodeCli : IOpenCodeCli
{
    private readonly IOpenCodeProcessRunner _processRunner;
    private readonly string _executable;

    public OpenCodeCli(string executable = "opencode")
        : this(new OpenCodeProcessRunner(), executable)
    {
    }

    public OpenCodeCli(
        IOpenCodeProcessRunner processRunner,
        string executable = "opencode")
    {
        _processRunner = processRunner;
        _executable = executable;
    }

    public async Task<OpenCodeModels> ListModelsAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var version = await DiscoverAsync(["--version"], workingDirectory, cancellationToken);
        if (string.IsNullOrWhiteSpace(version.StandardOutput))
            throw new InvalidOperationException("OpenCode returned an empty version.");

        var help = await DiscoverAsync(["run", "--help"], workingDirectory, cancellationToken);
        if (!Regex.IsMatch(help.StandardOutput, @"(?<![\w-])--model(?![\w-])") ||
            !Regex.IsMatch(help.StandardOutput, @"(?<![\w-])--format(?![\w-])"))
            throw new InvalidOperationException(
                "OpenCode run must support --model and --format.");

        var listing = await DiscoverAsync(["models"], workingDirectory, cancellationToken);
        var models = listing.StandardOutput.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        if (models.Length == 0 || models.Any(model => !IsValidModel(model)))
            throw new InvalidOperationException(
                "OpenCode returned an empty or malformed model listing; expected provider/model IDs.");

        return new OpenCodeModels(version.StandardOutput.Trim(), models);
    }

    public Task<OpenCodeResult> RunAsync(
        string instructions,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(model) || !IsValidModel(model))
            throw new ArgumentException("An explicit provider/model ID is required.", nameof(model));

        return _processRunner.RunAsync(
            new OpenCodeProcessRequest(
                _executable,
                ["run", "--model", model, "--format", "json"],
                workingDirectory,
                instructions),
            cancellationToken);
    }

    private static bool IsValidModel(string model)
    {
        var separator = model.IndexOf('/');
        return separator > 0 &&
            separator < model.Length - 1 &&
            !model.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
    }

    private async Task<OpenCodeResult> DiscoverAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            new OpenCodeProcessRequest(_executable, arguments, workingDirectory),
            cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"OpenCode '{string.Join(" ", arguments)}' failed with exit code {result.ExitCode}: " +
                (string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError));

        return result;
    }
}

public class OpenCodeProcessRunner : IOpenCodeProcessRunner
{
    private readonly int _maxCapturedCharacters;

    public OpenCodeProcessRunner(int maxCapturedCharacters = 1_048_576)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCapturedCharacters);
        _maxCapturedCharacters = maxCapturedCharacters;
    }

    public async Task<OpenCodeResult> RunAsync(
        OpenCodeProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(request.Executable)
            {
                UseShellExecute = false,
                WorkingDirectory = request.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        foreach (var argument in request.Arguments)
            process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Unable to start OpenCode executable '{request.Executable}'; " +
                "install OpenCode and ensure the executable is on PATH.",
                exception);
        }

        var standardOutput = CaptureAsync(process.StandardOutput, cancellationToken);
        var standardError = CaptureAsync(process.StandardError, cancellationToken);
        try
        {
            try
            {
                await process.StandardInput.WriteAsync(
                    (request.StandardInput ?? "").AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // A CLI can exit before consuming stdin; retain its exit status and diagnostics.
            }

            await process.WaitForExitAsync(cancellationToken);
            var captured = await Task.WhenAll(standardOutput, standardError)
                .WaitAsync(cancellationToken);
            return new OpenCodeResult(process.ExitCode, captured[0], captured[1]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The process may exit between checking its state and killing its tree.
            }

            process.StandardInput.BaseStream.Dispose();
            process.StandardOutput.Dispose();
            process.StandardError.Dispose();
            try
            {
                await Task.WhenAll(
                        process.WaitForExitAsync(CancellationToken.None),
                        standardOutput,
                        standardError)
                    .WaitAsync(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or IOException or
                    ObjectDisposedException or TimeoutException)
            {
                // Descendants can retain inherited pipes after the root process has exited.
            }

            throw;
        }
    }

    private async Task<string> CaptureAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var captured = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            var remaining = _maxCapturedCharacters - captured.Length;
            captured.Append(buffer, 0, Math.Min(count, remaining));
            truncated |= count > remaining;
        }

        if (truncated)
            captured.Append("\n[output truncated]");

        return captured.ToString();
    }
}
