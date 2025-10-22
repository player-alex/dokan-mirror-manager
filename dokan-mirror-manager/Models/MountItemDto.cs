namespace DokanMirrorManager.Models;

/// <summary>
/// Data transfer object for serializing/deserializing mount configuration
/// </summary>
public class MountItemDto
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationLetter { get; set; } = string.Empty;
    public bool AutoMount { get; set; }
    public bool IsReadOnly { get; set; }
}
