using Caliburn.Micro;
using DokanNet;

namespace DokanMirrorManager.Models;

public enum MountStatus
{
    Unmounted,
    Mounting,
    Mounted,
    Error
}

public class MountItem : PropertyChangedBase
{
    private string _sourcePath = string.Empty;
    private string _originalSourcePath = string.Empty; // Original path with environment variables
    private string _destinationLetter = string.Empty;
    private bool _isReadOnly;
    private bool _autoMount;
    private MountStatus _status = MountStatus.Unmounted;
    private string _errorMessage = string.Empty;

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            NotifyOfPropertyChange(() => SourcePath);
        }
    }

    // Original source path (may contain environment variables like %USERPROFILE%)
    public string OriginalSourcePath
    {
        get => _originalSourcePath;
        set => _originalSourcePath = value;
    }

    public string DestinationLetter
    {
        get => _destinationLetter;
        set
        {
            _destinationLetter = value;
            NotifyOfPropertyChange(() => DestinationLetter);
            NotifyOfPropertyChange(() => CanMount);
        }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            NotifyOfPropertyChange(() => IsReadOnly);
        }
    }

    public bool AutoMount
    {
        get => _autoMount;
        set
        {
            _autoMount = value;
            NotifyOfPropertyChange(() => AutoMount);
        }
    }

    public MountStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            NotifyOfPropertyChange(() => Status);
            NotifyOfPropertyChange(() => StatusText);
            NotifyOfPropertyChange(() => IsMounted);
            NotifyOfPropertyChange(() => CanMount);
            NotifyOfPropertyChange(() => CanUnmount);
            NotifyOfPropertyChange(() => CanEditDestination);
        }
    }

    public string StatusText => Status switch
    {
        MountStatus.Unmounted => "Unmounted",
        MountStatus.Mounting => "Mounting...",
        MountStatus.Mounted => "Mounted",
        MountStatus.Error => $"Error: {ErrorMessage}",
        _ => "Unknown"
    };

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            NotifyOfPropertyChange(() => ErrorMessage);
            NotifyOfPropertyChange(() => StatusText);
        }
    }

    public bool IsMounted => Status == MountStatus.Mounted;
    public bool CanMount => !string.IsNullOrEmpty(DestinationLetter) && (Status == MountStatus.Unmounted || Status == MountStatus.Error);
    public bool CanUnmount => Status == MountStatus.Mounted;
    public bool CanEditDestination => Status == MountStatus.Unmounted || Status == MountStatus.Error;

    public object? DokanInstance { get; set; }
    public Mirror? Mirror { get; set; }

    // Available drive letters for this item (includes its own selected letter)
    private List<string> _availableDriveLetters = new();
    public List<string> AvailableDriveLetters
    {
        get => _availableDriveLetters;
        set
        {
            _availableDriveLetters = value;
            NotifyOfPropertyChange(() => AvailableDriveLetters);
        }
    }
}
