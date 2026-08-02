using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StartUPs.Services;

/// <summary>The result of a single winget invocation.</summary>
public record WingetResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Exit code as hex, which is how winget documents its error codes.</summary>
    public string ExitCodeHex => $"0x{ExitCode:X8}";
}

/// <summary>A live snapshot of an in-flight download.</summary>
public record DownloadSample(long Downloaded, long Total, double BytesPerSecond)
{
    public double Percent => Total > 0 ? Math.Clamp(Downloaded * 100.0 / Total, 0, 100) : 0;
}

/// <summary>
/// Thin wrapper around the winget CLI. StartUPs never downloads installers itself -
/// winget fetches each app from the vendor's official URL and verifies its hash.
/// </summary>
public static class WingetService
{
    private static string? _cachedPath;

    /// <summary>
    /// Matches winget's progress readout, e.g. "1.90 MB / 12.40 MB".
    /// It is redrawn using carriage returns, so we split output on \r as well as \n.
    /// </summary>
    private static readonly Regex ProgressPattern = new(
        @"([\d.,]+)\s*(TB|GB|MB|KB|B)\s*/\s*([\d.,]+)\s*(TB|GB|MB|KB|B)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------ discovery

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

    // ------------------------------------------------------------------ commands

    /// <summary>True when the package is already present on this PC.</summary>
    public static async Task<bool> IsInstalledAsync(string wingetId, CancellationToken ct)
    {
        var result = await RunAsync(
            $"list --id {wingetId} --exact --disable-interactivity --accept-source-agreements",
            null, ct);

        // winget exits non-zero (no packages found) when it isn't installed.
        return result.Succeeded && result.Output.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Installs one package silently, reporting live download progress.</summary>
    public static Task<WingetResult> InstallAsync(
        string wingetId, IProgress<DownloadSample>? progress, CancellationToken ct)
        => RunAsync(
            $"install --id {wingetId} --exact --source winget --silent " +
            "--accept-package-agreements --accept-source-agreements --disable-interactivity",
            progress, ct);

    /// <summary>Removes one package silently.</summary>
    public static Task<WingetResult> UninstallAsync(string wingetId, CancellationToken ct)
        => RunAsync(
            $"uninstall --id {wingetId} --exact --silent " +
            "--accept-source-agreements --disable-interactivity",
            null, ct);

    /// <summary>
    /// Every package winget can see on this PC, by package id.
    ///
    /// This is one winget call for the whole machine. Asking per app with
    /// <see cref="IsInstalledAsync"/> would mean a process launch each, which is
    /// far too slow across a catalog this size. 'export' is used rather than
    /// 'list' because it emits JSON instead of a column-aligned table.
    /// </summary>
    public static async Task<HashSet<string>> GetInstalledAsync(CancellationToken ct)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(Path.GetTempPath(), $"StartUPs_installed_{Guid.NewGuid():N}.json");

        try
        {
            await RunAsync(
                $"export -o \"{path}\" --disable-interactivity --accept-source-agreements",
                null, ct);

            if (!File.Exists(path)) return found;

            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!document.RootElement.TryGetProperty("Sources", out var sources)) return found;

            foreach (var source in sources.EnumerateArray())
            {
                if (!source.TryGetProperty("Packages", out var packages)) continue;

                foreach (var package in packages.EnumerateArray())
                {
                    if (package.TryGetProperty("PackageIdentifier", out var id) &&
                        id.GetString() is { Length: > 0 } value)
                        found.Add(value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Detection is best effort. An empty set simply means nothing is
            // badged as installed - it must never stop the app from loading.
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }

        return found;
    }

    // ------------------------------------------------------------------ process

    private static async Task<WingetResult> RunAsync(
        string arguments, IProgress<DownloadSample>? progress, CancellationToken ct)
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

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new WingetResult(-1, $"Could not start winget: {ex.Message}");
        }

        var tracker = new SpeedTracker();

        // winget redraws its progress bar with \r, so read the stream by hand
        // rather than using ReadLine - otherwise nothing surfaces until it ends.
        var stdout = PumpAsync(process.StandardOutput, segment =>
        {
            lock (output) output.AppendLine(segment);
            if (progress is not null && TryParseProgress(segment, out long done, out long total))
                progress.Report(tracker.Sample(done, total));
        }, ct);

        var stderr = PumpAsync(process.StandardError, segment =>
        {
            lock (output) output.AppendLine(segment);
        }, ct);

        try
        {
            await Task.WhenAll(stdout, stderr);
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new WingetResult(process.ExitCode, output.ToString());
    }

    /// <summary>Reads a stream character by character, emitting a segment on every \r or \n.</summary>
    private static async Task PumpAsync(StreamReader reader, Action<string> onSegment, CancellationToken ct)
    {
        var buffer = new char[512];
        var current = new StringBuilder();

        while (true)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(buffer.AsMemory(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (read == 0) break;

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (c is '\r' or '\n')
                {
                    if (current.Length > 0)
                    {
                        onSegment(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        if (current.Length > 0)
            onSegment(current.ToString());
    }

    // ------------------------------------------------------------------ parsing

    private static bool TryParseProgress(string segment, out long downloaded, out long total)
    {
        downloaded = 0;
        total = 0;

        var match = ProgressPattern.Match(segment);
        if (!match.Success) return false;

        if (!TryParseSize(match.Groups[1].Value, match.Groups[2].Value, out downloaded)) return false;
        if (!TryParseSize(match.Groups[3].Value, match.Groups[4].Value, out total)) return false;

        return total > 0;
    }

    private static bool TryParseSize(string number, string unit, out long bytes)
    {
        bytes = 0;

        if (!double.TryParse(number.Replace(",", ""), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value))
            return false;

        double multiplier = unit.ToUpperInvariant() switch
        {
            "TB" => 1024d * 1024 * 1024 * 1024,
            "GB" => 1024d * 1024 * 1024,
            "MB" => 1024d * 1024,
            "KB" => 1024d,
            _ => 1d
        };

        bytes = (long)(value * multiplier);
        return true;
    }

    /// <summary>Turns raw byte counts into a smoothed transfer rate.</summary>
    private sealed class SpeedTracker
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastBytes;
        private TimeSpan _lastTime;
        private double _smoothed;

        public DownloadSample Sample(long downloaded, long total)
        {
            var now = _clock.Elapsed;
            double seconds = (now - _lastTime).TotalSeconds;

            // Only re-measure every ~200ms so the readout doesn't jitter wildly.
            if (seconds >= 0.2)
            {
                long delta = downloaded - _lastBytes;
                if (delta > 0)
                {
                    double instant = delta / seconds;
                    // Exponential moving average keeps the number readable.
                    _smoothed = _smoothed <= 0 ? instant : (_smoothed * 0.6) + (instant * 0.4);
                }

                _lastBytes = downloaded;
                _lastTime = now;
            }

            return new DownloadSample(downloaded, total, _smoothed);
        }
    }

    /// <summary>Formats a transfer rate for display, e.g. "12.4 MB/s".</summary>
    public static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "";

        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        int index = 0;
        double value = bytesPerSecond;

        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return value >= 100
            ? $"{value:0} {units[index]}"
            : $"{value:0.0} {units[index]}";
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
