using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using StartUPs.Models;

namespace StartUPs.Services;

/// <summary>
/// Checks GitHub for a newer release, downloads it, and swaps it in.
///
/// This is the only part of StartUPs that opens a network connection of its own.
/// It talks to api.github.com and objects.githubusercontent.com, and only when
/// the user presses Check or Download - never automatically on startup.
/// </summary>
public static class UpdateService
{
    private const string Owner = "Distortionzz";
    private const string Repo = "StartUPs";
    private const string AssetName = "StartUPs.exe";

    /// <summary>GitHub rejects API requests that do not identify themselves.</summary>
    private const string UserAgent = "StartUPs-Updater";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    /// <summary>The version of the running executable.</summary>
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    // ------------------------------------------------------------------ check

    public static async Task<UpdateInfo> CheckAsync(CancellationToken ct)
    {
        var current = CurrentVersion;

        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var response = await Http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failure(current,
                    "No public release was found. If the repository is private, " +
                    "its releases are not reachable without signing in.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return Failure(current,
                    "GitHub rate limit reached. Please try again in a little while.");
            }

            if (!response.IsSuccessStatusCode)
                return Failure(current, $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.");

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            if (!TryParseVersion(tag, out var latest))
                return Failure(current, $"Could not read a version number from the release tag '{tag}'.");

            var notes = root.TryGetProperty("body", out var bodyElement)
                ? bodyElement.GetString() ?? ""
                : "";

            string? downloadUrl = null;
            long size = 0;
            string digest = "";

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase)) continue;

                    downloadUrl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                    size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                    // GitHub reports this as "sha256:<hex>"
                    if (asset.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String)
                        digest = (dg.GetString() ?? "").Replace("sha256:", "", StringComparison.OrdinalIgnoreCase);

                    break;
                }
            }

            bool newer = latest > current;

            return new UpdateInfo
            {
                Status = newer ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate,
                CurrentVersion = current,
                LatestVersion = latest,
                DownloadUrl = downloadUrl,
                DownloadSize = size,
                ExpectedSha256 = digest,
                ReleaseNotes = notes.Trim()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Failure(current, $"Could not reach GitHub. Check your internet connection. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return Failure(current, ex.Message);
        }
    }

    private static UpdateInfo Failure(Version current, string message) => new()
    {
        Status = UpdateStatus.Failed,
        CurrentVersion = current,
        ErrorMessage = message
    };

    /// <summary>Turns a release tag such as "v1.2.0" into a Version.</summary>
    private static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var cleaned = tag.TrimStart('v', 'V').Trim();

        // Version.TryParse wants at least major.minor
        if (!cleaned.Contains('.')) cleaned += ".0";

        return Version.TryParse(cleaned, out version!);
    }

    // ------------------------------------------------------------------ download

    /// <summary>
    /// Downloads the new executable to a temporary file and returns its path.
    /// When GitHub supplied a digest, the file is hashed and rejected on mismatch.
    /// </summary>
    public static async Task<string> DownloadAsync(
        string url, string expectedSha256, IProgress<(long Received, long Total)>? progress, CancellationToken ct)
    {
        var destination = Path.Combine(Path.GetTempPath(), $"StartUPs_update_{Guid.NewGuid():N}.exe");

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? 0;
        long received = 0;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = File.Create(destination))
        {
            var buffer = new byte[81920];
            int read;

            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                progress?.Report((received, total));
            }
        }

        // The file must be closed before it can be hashed.
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actual = await ComputeSha256Async(destination, ct);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(destination);
                throw new InvalidOperationException(
                    "The downloaded file did not match the checksum GitHub published for this release, " +
                    "so it was discarded. Download the release manually instead.");
            }
        }

        return destination;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }

    // ------------------------------------------------------------------ install

    /// <summary>
    /// Windows will not let a running executable be overwritten, so a small script
    /// takes over: it retries the copy until this process exits, then relaunches.
    /// </summary>
    public static void ApplyUpdateAndRestart(string downloadedExePath)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable's path.");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"StartUPs_apply_{Guid.NewGuid():N}.cmd");

        var script = $"""
            @echo off
            setlocal
            set "SRC={downloadedExePath}"
            set "DST={currentExe}"
            set /a TRIES=0

            :retry
            set /a TRIES+=1
            if %TRIES% GTR 60 goto giveup
            timeout /t 1 /nobreak >nul
            copy /y "%SRC%" "%DST%" >nul 2>&1
            if errorlevel 1 goto retry

            del /q "%SRC%" >nul 2>&1
            start "" "%DST%"
            goto cleanup

            :giveup
            del /q "%SRC%" >nul 2>&1

            :cleanup
            del /q "%~f0" >nul 2>&1
            """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath()
        });
    }

    /// <summary>Formats a byte count for the download readout.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "";
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return value >= 100 ? $"{value:0} {units[index]}" : $"{value:0.0} {units[index]}";
    }
}
