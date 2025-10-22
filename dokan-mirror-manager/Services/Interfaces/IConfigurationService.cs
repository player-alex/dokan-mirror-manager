using DokanMirrorManager.Models;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Service for managing application configuration persistence
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Loads mount configuration from persistent storage
    /// </summary>
    /// <returns>List of mount items loaded from configuration</returns>
    Task<List<MountItem>> LoadConfigurationAsync();

    /// <summary>
    /// Saves mount configuration to persistent storage
    /// </summary>
    /// <param name="items">Mount items to save</param>
    Task SaveConfigurationAsync(IEnumerable<MountItem> items);
}
