using DokanMirrorManager.Models;
using System.Collections.ObjectModel;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Service for handling IPC queries about mount points from external processes.
/// </summary>
public interface IMountPointQueryService
{
    /// <summary>
    /// Handles a mount point query request received via WM_COPYDATA message.
    /// Collects current mount point information and sends it via Named Pipe.
    /// </summary>
    /// <param name="pipeName">Name of the Named Pipe to send response to.</param>
    /// <param name="mountItems">Collection of mount items to serialize.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Task representing the async operation.</returns>
    Task HandleMountPointQueryAsync(string pipeName, ObservableCollection<MountItem> mountItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a collection of MountItems to a serializable response object.
    /// </summary>
    /// <param name="mountItems">Collection of mount items.</param>
    /// <returns>Response object ready for JSON serialization.</returns>
    MountPointsResponse CreateResponse(ObservableCollection<MountItem> mountItems);
}
