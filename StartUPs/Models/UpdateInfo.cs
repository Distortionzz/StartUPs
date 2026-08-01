namespace StartUPs.Models;

/// <summary>Why an update check finished the way it did.</summary>
public enum UpdateStatus
{
    /// <summary>Nothing checked yet this session.</summary>
    Unknown,

    /// <summary>A check is in flight.</summary>
    Checking,

    /// <summary>Running the newest published release.</summary>
    UpToDate,

    /// <summary>A newer release exists.</summary>
    UpdateAvailable,

    /// <summary>The check could not complete (offline, rate limited, private repo).</summary>
    Failed
}

/// <summary>The outcome of asking GitHub for the latest release.</summary>
public class UpdateInfo
{
    public UpdateStatus Status { get; init; }
    public Version CurrentVersion { get; init; } = new(0, 0, 0);
    public Version? LatestVersion { get; init; }

    /// <summary>Direct download URL for the released StartUPs.exe.</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Size of the release asset in bytes, used for the progress bar.</summary>
    public long DownloadSize { get; init; }

    /// <summary>
    /// SHA-256 that GitHub reports for the asset, without the "sha256:" prefix.
    /// The download is rejected if it does not match.
    /// </summary>
    public string ExpectedSha256 { get; init; } = "";

    /// <summary>The release notes body, shown in the panel.</summary>
    public string ReleaseNotes { get; init; } = "";

    /// <summary>Human-readable reason when <see cref="Status"/> is Failed.</summary>
    public string ErrorMessage { get; init; } = "";

    public bool CanDownload => Status == UpdateStatus.UpdateAvailable
                               && !string.IsNullOrEmpty(DownloadUrl);
}
