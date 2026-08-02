namespace StartUPs.Models;

/// <summary>Where an app sits in the install queue.</summary>
public enum InstallState
{
    /// <summary>Not part of a run. No status shown on the card.</summary>
    None,

    /// <summary>Queued, waiting its turn.</summary>
    Pending,

    /// <summary>Asking winget whether it is already on this PC.</summary>
    Checking,

    /// <summary>winget is installing it right now.</summary>
    Installing,

    /// <summary>Installed successfully during this run.</summary>
    Installed,

    /// <summary>Was already on the PC, so it was skipped.</summary>
    AlreadyInstalled,

    /// <summary>winget returned a non-zero exit code.</summary>
    Failed,

    /// <summary>The user cancelled before this app was reached.</summary>
    Cancelled,

    /// <summary>winget is removing it right now.</summary>
    Uninstalling,

    /// <summary>Removed successfully during this run.</summary>
    Uninstalled
}
