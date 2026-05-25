using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;

namespace Penumbra.Services;

public sealed class ArchiveExtractionNotification(MessageService messageService)
    : AmassingNotification<ArchiveExtractionNotification.ArchiveInfo>(messageService), IService
{
    public readonly record struct ArchiveInfo(string ArchiveName, int ModCount);

    private volatile bool    _isExtracting;
    private volatile int     _currentEntryIndex;
    private volatile int     _totalEntries;
    private volatile string? _currentEntryName;

    private bool _wasExtracting;

    public void AddArchive(string archiveName, int modCount)
        => AddObject(new ArchiveInfo(archiveName, modCount));

    public void SetProgress(int currentEntry, int totalEntries, string entryName)
    {
        _currentEntryIndex = currentEntry;
        _totalEntries      = totalEntries;
        _currentEntryName  = entryName;
        _isExtracting      = true;
    }

    public void ClearProgress()
    {
        _isExtracting      = false;
        _currentEntryIndex = 0;
        _totalEntries      = 0;
        _currentEntryName  = null;
    }

    public override NotificationType NotificationType
        => NotificationType.Info;

    public override string NotificationTitle
        => Count switch
        {
            1 => $"Extracting mods from {GatheredObjects[0].ArchiveName}",
            _ => $"Extracting mods from {Count} archives",
        };

    public override string NotificationMessage
        => _isExtracting
            ? $"Extracting '{_currentEntryName}'..."
            : "Waiting...";

    public override TimeSpan NotificationDuration
        => TimeSpan.MaxValue;

    public override void NotificationActions(INotificationDrawArgs args)
    {
        if (_isExtracting)
        {
            _wasExtracting = true;
            var total = _totalEntries;
            if (total > 0 && CurrentNotification is { } notification)
            {
                notification.Progress = _currentEntryIndex / (float)total;
                notification.Content  = $"Extracting '{_currentEntryName}' ({_currentEntryIndex + 1} of {total})...";
            }
        }
        else if (_wasExtracting)
        {
            _wasExtracting = false;
            if (CurrentNotification is { } notification)
            {
                notification.Progress       = 1.0f;
                notification.Content        = "Extraction complete.";
                notification.InitialDuration = TimeSpan.FromSeconds(15);
            }
        }
    }

    protected override StoredNotification CreateStored(in ArchiveInfo @object)
        => new Stored(this, @object);

    private sealed class Stored(ArchiveExtractionNotification parent, ArchiveInfo info)
        : StoredNotification(parent, info)
    {
        public override string   LogMessage    { get; } = $"Extracted {info.ModCount} mod(s) from {info.ArchiveName}.";
        public override StringU8 StoredMessage { get; } = new($"[{info.ArchiveName}] {info.ModCount} mod(s) extracted.");
        public override StringU8 StoredTooltip { get; } = new($"Archive: {info.ArchiveName}\nMods found: {info.ModCount}");
    }
}
