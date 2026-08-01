using System.Diagnostics;
using System.IO;
using System.Text;

namespace StartUPs.Services;

/// <summary>The result of a single winget invocation.</summary>
public record WingetResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Exit code as hex, which is how winget documents its error codes.</summary>
    public string ExitCodeHex => $"0x{ExitCode:X8}";
}

/// <summary>
/// Thin wrapper around the winget CLI. StartUPs never downloads installers itself -
/// winget fetches each app from the vendor's official URL and verifies its hash.
/// </summary>
public static class WingetService
{
    private static string? _cachedPath;

    /// <summary>
    /// Finds winget.exe. The usual PATH alias can fail in an elevated process, so
    /// fall back to the real executable inside the WindowsApps package folder.
    /// </summary>
    public static string? ResolveWingetPath()
    {
        if (_cachedPath is not null) return _cachedPath;

        // 1. The App Execution Alias on PATH.
        var localAlias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(localAlias))
            return _cachedPath = localAlias;

        // 2. The actual package payload (readable because we run elevated).
        try
        {
            var windowsApps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");

            if (Directory.Exists(windowsApps))
            {
                var match = Directory
                    .EnumerateDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe")
                    .OrderByDescending(d => d)
                    .Select(d => Path.Combine(d, "winget.exe"))
                    .FirstOrDefault(File.Exists);

                if (match is not null)
                    return _cachedPath = match;
            }
        }
        catch
        {
            // Access denied or a locked-down machine - fall through.
        }

        // 3. Let the OS resolve it and hope PATH works.
        return _cachedPath = "winget.exe";
    }

    public static bool IsAvailable()
    {
        var path = ResolveWingetPath();
        return path is not null && (path == "winget.exe" || File.Exists(path));
    }

    /// <summary>True when the package is already present on this PC.</summary>
    public static async Task<bool> IsInstalledAsync(string wingetId, CancellationToken ct)
    {
        var result = await RunAsync(
            $"list --id {wingetId} --exact --disable-interactivity --accept-source-agreements", ct);

        // winget exits non-zero (no packages found) when it isn't installed.
        return result.Succeeded && result.Output.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Installs one package silently.</summary>
    public static Task<WingetResult> InstallAsync(string wingetId, CancellationToken ct)
        => RunAsync(
            $"install --id {wingetId} --exact --source winget --silent " +
            "--accept-package-agreements --accept-source-agreements --disable-interactivity", ct);

    // ------------------------------------------------------------------ process

    private static async Task<WingetResult> RunAsync(string arguments, CancellationToken ct)
    {
        var exe = ResolveWingetPath() ?? "winget.exe";

        var startInfo = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new WingetResult(-1, $"Could not start winget: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new WingetResult(process.ExitCode, output.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone.
        }
    }
}
