using System.Text.Json.Serialization;

namespace DokanMirrorManager.Models;

/// <summary>
/// Response object containing all mount points for IPC communication.
/// </summary>
public class MountPointsResponse
{
    /// <summary>
    /// List of all mount points currently managed by Dokan Mirror Manager.
    /// </summary>
    [JsonPropertyName("mountPoints")]
    public List<MountPointInfo> MountPoints { get; set; } = new();

    /// <summary>
    /// Timestamp when this response was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// API version for compatibility checking.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Whether the query was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if success is false.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
