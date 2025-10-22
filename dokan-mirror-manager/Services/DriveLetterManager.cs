using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using System.Collections.ObjectModel;
using System.IO;

namespace DokanMirrorManager.Services;

/// <summary>
/// Manages drive letter allocation and availability for mount items.
/// </summary>
public class DriveLetterManager : IDriveLetterManager
{
    private bool _isUpdating = false;

    /// <summary>
    /// Gets a list of available drive letters that can be used for mounting.
    /// </summary>
    /// <param name="items">All mount items in the system.</param>
    /// <param name="currentItem">The item to get available letters for (if null, returns globally available letters).</param>
    /// <returns>List of available drive letters in format "X:\".</returns>
    public List<string> GetAvailableDriveLetters(IEnumerable<MountItem> items, MountItem? currentItem)
    {
        try
        {
            var allLetters = Enumerable.Range('A', 26).Select(i => $"{(char)i}:\\").ToList();
            var usedLetters = DriveInfo.GetDrives().Select(d => d.Name).ToHashSet();

            if (currentItem == null)
            {
                // Global available letters (not considering any specific item)
                var mountedLetters = items
                    .Where(m => m.Status == MountStatus.Mounted && !string.IsNullOrEmpty(m.DestinationLetter))
                    .Select(m => m.DestinationLetter)
                    .ToHashSet();

                return allLetters
                    .Where(letter => !usedLetters.Contains(letter) && !mountedLetters.Contains(letter))
                    .ToList();
            }
            else
            {
                // Available letters for a specific item
                var otherUsedLetters = items
                    .Where(m => m != currentItem && !string.IsNullOrEmpty(m.DestinationLetter))
                    .Select(m => m.DestinationLetter)
                    .ToHashSet();

                var available = allLetters
                    .Where(letter => !usedLetters.Contains(letter) && !otherUsedLetters.Contains(letter))
                    .ToList();

                // If this item has a selected letter, make sure it's in the list
                if (!string.IsNullOrEmpty(currentItem.DestinationLetter) && !available.Contains(currentItem.DestinationLetter))
                {
                    if (currentItem.Status == MountStatus.Mounted || !usedLetters.Contains(currentItem.DestinationLetter))
                    {
                        available.Add(currentItem.DestinationLetter);
                        available.Sort();
                    }
                }

                return available;
            }
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Automatically selects an available drive letter for the given item.
    /// </summary>
    /// <param name="item">The mount item to select a drive letter for.</param>
    /// <param name="availableLetters">The list of available drive letters.</param>
    /// <returns>The selected drive letter, or null if none available.</returns>
    public string? AutoSelectDriveLetter(MountItem item, List<string> availableLetters)
    {
        // 이미 드라이브 레터가 있고 사용 가능하면 그대로 유지
        if (!string.IsNullOrEmpty(item.DestinationLetter) &&
            availableLetters.Contains(item.DestinationLetter))
        {
            return item.DestinationLetter;
        }

        // 사용 가능한 첫 번째 드라이브 레터 선택
        if (availableLetters.Count > 0)
        {
            return availableLetters[0];
        }

        return null;
    }

    /// <summary>
    /// Updates available drive letters for all mount items.
    /// </summary>
    /// <param name="items">All mount items to update.</param>
    public void UpdateAllDriveLetters(ObservableCollection<MountItem> items)
    {
        if (_isUpdating)
            return;

        try
        {
            _isUpdating = true;

            var allLetters = Enumerable.Range('A', 26).Select(i => $"{(char)i}:\\").ToList();
            var usedLetters = DriveInfo.GetDrives().Select(d => d.Name).ToHashSet();

            foreach (var item in items)
            {
                // Get letters used by OTHER items (both mounted and unmounted with selected letters)
                var otherUsedLetters = items
                    .Where(m => m != item && !string.IsNullOrEmpty(m.DestinationLetter))
                    .Select(m => m.DestinationLetter)
                    .ToHashSet();

                // Available letters = all letters - system used - other items' selected letters
                var available = allLetters
                    .Where(letter => !usedLetters.Contains(letter) && !otherUsedLetters.Contains(letter))
                    .ToList();

                // If this item has a selected letter, make sure it's in the list
                if (!string.IsNullOrEmpty(item.DestinationLetter) && !available.Contains(item.DestinationLetter))
                {
                    if (item.Status == MountStatus.Mounted || !usedLetters.Contains(item.DestinationLetter))
                    {
                        available.Add(item.DestinationLetter);
                        available.Sort();
                    }
                }

                item.AvailableDriveLetters = available;
            }
        }
        finally
        {
            _isUpdating = false;

            // Check if any item's selected drive letter is no longer available
            foreach (var item in items)
            {
                if (item.Status == MountStatus.Unmounted || item.Status == MountStatus.Error)
                {
                    var availableLetters = GetAvailableDriveLetters(items, item);
                    var selectedLetter = AutoSelectDriveLetter(item, availableLetters);
                    if (selectedLetter != null && item.DestinationLetter != selectedLetter)
                    {
                        item.DestinationLetter = selectedLetter;
                    }
                }
            }
        }
    }
}
