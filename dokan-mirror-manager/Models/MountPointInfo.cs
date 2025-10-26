using System.Text.Json.Serialization;

namespace DokanMirrorManager.Models;

/// <summary>
/// Represents information about a single mount point for IPC communication.
/// </summary>
public class MountPointInfo
{
    /// <summary>
    /// Source path that is being mirrored.
    /// </summary>
    [JsonPropertyName("srcPath")]
    public string SrcPath { get; set; } = string.Empty;

    /// <summary>
    /// Destination drive letter (e.g., "M:\").
    /// </summary>
    [JsonPropertyName("dstPath")]
    public string DstPath { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the drive.
    /// </summary>
    [JsonPropertyName("driveName")]
    public string DriveName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the mount point (e.g., "Mounted", "Unmounted", "Mounting", "Error").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the mount is read-only.
    /// </summary>
    [JsonPropertyName("isReadOnly")]
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Whether auto-mount is enabled for this mount point.
    /// </summary>
    [JsonPropertyName("autoMount")]
    public bool AutoMount { get; set; }

    /// <summary>
    /// Error message if status is "Error", otherwise empty.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;
}
