using DokanMirrorManager.Models;
using System.Collections.ObjectModel;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Manages drive letter allocation and availability for mount items.
/// </summary>
public interface IDriveLetterManager
{
    /// <summary>
    /// Gets a list of available drive letters that can be used for mounting.
    /// </summary>
    /// <param name="items">All mount items in the system.</param>
    /// <param name="currentItem">The item to get available letters for (if null, returns globally available letters).</param>
    /// <returns>List of available drive letters in format "X:\".</returns>
    List<string> GetAvailableDriveLetters(IEnumerable<MountItem> items, MountItem? currentItem);

    /// <summary>
    /// Automatically selects an available drive letter for the given item.
    /// </summary>
    /// <param name="item">The mount item to select a drive letter for.</param>
    /// <param name="availableLetters">The list of available drive letters.</param>
    /// <returns>The selected drive letter, or null if none available.</returns>
    string? AutoSelectDriveLetter(MountItem item, List<string> availableLetters);

    /// <summary>
    /// Updates available drive letters for all mount items.
    /// </summary>
    /// <param name="items">All mount items to update.</param>
    void UpdateAllDriveLetters(ObservableCollection<MountItem> items);
}
