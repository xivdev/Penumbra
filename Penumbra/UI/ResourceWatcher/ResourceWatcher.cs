using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ResourceWatcher;

public sealed class ResourceWatcher : IDisposable, ITab, IUiService
{
    public const int        DefaultMaxEntries = 1024;
    public const RecordType AllRecords        = RecordType.Request | RecordType.ResourceLoad | RecordType.FileLoad | RecordType.Destruction;

    private readonly Configuration            _config;
    private readonly EphemeralConfig          _ephemeral;
    private readonly ResourceService          _resources;
    private readonly ResourceLoader           _loader;
    private readonly ResourceHandleDestructor _destructor;
    private readonly ActorManager             _actors;
    private readonly List<Record>             _records    = [];
    private readonly ConcurrentQueue<Record>  _newRecords = [];
    private readonly ResourceWatcherTable     _table;
    private          string                   _logFilter = string.Empty;
    private          Regex?                   _logRegex;
    private          int                      _newMaxEntries;

    public unsafe ResourceWatcher(ActorManager actors, Configuration config, ResourceService resources, ResourceLoader loader,
        ResourceHandleDestructor destructor)
    {
        _actors                      =  actors;
        _config                      =  config;
        _ephemeral                   =  config.Ephemeral;
        _resources                   =  resources;
        _destructor                  =  destructor;
        _loader                      =  loader;
        _table                       =  new ResourceWatcherTable(config.Ephemeral, _records);
        _resources.ResourceRequested += OnResourceRequested;
        _destructor.Subscribe(OnResourceDestroyed, ResourceHandleDestructor.Priority.ResourceWatcher);
        _loader.ResourceLoaded   += OnResourceLoaded;
        _loader.ResourceComplete += OnResourceComplete;
        _loader.FileLoaded       += OnFileLoaded;
        _loader.PapRequested     += OnPapRequested;
        UpdateFilter(_ephemeral.ResourceLoggingFilter, false);
        _newMaxEntries = _config.MaxResourceWatcherRecords;
    }

    private void OnPapRequested(Utf8GamePath original, FullPath? _1, ResolveData _2)
    {
        if (_ephemeral.EnableResourceLogging && FilterMatch(original.Path, out var match))
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {match} was requested asynchronously.");

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = Record.CreateRequest(original.Path, false);
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    public unsafe void Dispose()
    {
        Clear();
        _records.TrimExcess();
        _resources.ResourceRequested -= OnResourceRequested;
        _destructor.Unsubscribe(OnResourceDestroyed);
        _loader.ResourceLoaded   -= OnResourceLoaded;
        _loader.ResourceComplete -= OnResourceComplete;
        _loader.FileLoaded       -= OnFileLoaded;
        _loader.PapRequested     -= OnPapRequested;
    }

    private void Clear()
    {
        _records.Clear();
        _newRecords.Clear();
        _table.Reset();
    }

    public ReadOnlySpan<byte> Label
        => "Resource Logger"u8;

    public void DrawContent()
    {
        UpdateRecords();

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetTextLineHeightWithSpacing() / 2);
        var isEnabled = _ephemeral.EnableResourceWatcher;
        if (ImGui.Checkbox("Enable", ref isEnabled))
        {
            _ephemeral.EnableResourceWatcher = isEnabled;
            _ephemeral.Save();
        }

        ImGui.SameLine();
        DrawMaxEntries();
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            Clear();

        ImGui.SameLine();
        var onlyMatching = _ephemeral.OnlyAddMatchingResources;
        if (ImGui.Checkbox("Store Only Matching", ref onlyMatching))
        {
            _ephemeral.OnlyAddMatchingResources = onlyMatching;
            _ephemeral.Save();
        }

        ImGui.SameLine();
        var writeToLog = _ephemeral.EnableResourceLogging;
        if (ImGui.Checkbox("Write to Log", ref writeToLog))
        {
            _ephemeral.EnableResourceLogging = writeToLog;
            _ephemeral.Save();
        }

        ImGui.SameLine();
        DrawFilterInput();

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetTextLineHeightWithSpacing() / 2);

        _table.Draw(ImGui.GetTextLineHeightWithSpacing());
    }

    private void DrawFilterInput()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var       tmp          = _logFilter;
        var       invalidRegex = _logRegex == null && _logFilter.Length > 0;
        using var color        = ImRaii.PushColor(ImGuiCol.Border, Colors.RegexWarningBorder, invalidRegex);
        using var style        = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, UiHelpers.Scale, invalidRegex);
        if (ImGui.InputTextWithHint("##logFilter", "If path matches this Regex...", ref tmp, 256))
            UpdateFilter(tmp, true);
    }

    private void UpdateFilter(string newString, bool config)
    {
        if (newString == _logFilter)
            return;

        _logFilter = newString;
        try
        {
            _logRegex = new Regex(_logFilter, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch
        {
            _logRegex = null;
        }

        if (config)
        {
            _ephemeral.ResourceLoggingFilter = newString;
            _ephemeral.Save();
        }
    }

    private bool FilterMatch(CiByteString path, out string match)
    {
        match = path.ToString();
        return _logFilter.Length == 0 || (_logRegex?.IsMatch(match) ?? false) || match.Contains(_logFilter, StringComparison.OrdinalIgnoreCase);
    }


    private void DrawMaxEntries()
    {
        ImGui.SetNextItemWidth(80 * UiHelpers.Scale);
        ImGui.InputInt("Max. Entries", ref _newMaxEntries, 0, 0);
        var change = ImGui.IsItemDeactivatedAfterEdit();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
        {
            change         = true;
            _newMaxEntries = DefaultMaxEntries;
        }

        var maxEntries = _config.MaxResourceWatcherRecords;
        if (maxEntries != DefaultMaxEntries && ImGui.IsItemHovered())
            ImGui.SetTooltip($"CTRL + Right-Click to reset to default {DefaultMaxEntries}.");

        if (!change)
            return;

        _newMaxEntries = Math.Max(16, _newMaxEntries);
        if (_newMaxEntries == maxEntries)
            return;

        _config.MaxResourceWatcherRecords = _newMaxEntries;
        _config.Save();
        if (_newMaxEntries > _records.Count)
            _records.RemoveRange(0, _records.Count - _newMaxEntries);
    }

    private void UpdateRecords()
    {
        var count = _newRecords.Count;
        if (count <= 0)
            return;

        while (_newRecords.TryDequeue(out var rec) && count-- > 0)
            _records.Add(rec);

        if (_records.Count > _config.MaxResourceWatcherRecords)
            _records.RemoveRange(0, _records.Count - _config.MaxResourceWatcherRecords);

        _table.Reset();
    }


    private unsafe void OnResourceRequested(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path,
        Utf8GamePath original, GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (_ephemeral.EnableResourceLogging && FilterMatch(original.Path, out var match))
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {match} was requested {(sync ? "synchronously." : "asynchronously.")}");

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = Record.CreateRequest(original.Path, sync);
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, ResolveData data)
    {
        if (_ephemeral.EnableResourceLogging)
        {
            var log   = FilterMatch(path.Path, out var name);
            var name2 = string.Empty;
            if (manipulatedPath != null)
                log |= FilterMatch(manipulatedPath.Value.InternalName, out name2);

            if (log)
            {
                var pathString = manipulatedPath != null ? $"custom file {name2} instead of {name}" : name;
                Penumbra.Log.Information(
                    $"[ResourceLoader] [LOAD] [{handle->FileType}] Loaded {pathString} to 0x{(ulong)handle:X} using collection {data.ModCollection.Identity.AnonymizedName} for {Name(data, "no associated object.")} (Refcount {handle->RefCount}) ");
            }
        }

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = manipulatedPath == null
            ? Record.CreateDefaultLoad(path.Path, handle, data.ModCollection, Name(data))
            : Record.CreateLoad(manipulatedPath.Value, path.Path, handle, data.ModCollection, Name(data));
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnResourceComplete(ResourceHandle* resource, CiByteString path, Utf8GamePath original, ReadOnlySpan<byte> additionalData, bool isAsync)
    {
        if (!isAsync)
            return;

        if (_ephemeral.EnableResourceLogging && FilterMatch(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DONE] [{resource->FileType}] Finished loading {match} into 0x{(ulong)resource:X}, state {resource->LoadState}.");

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = Record.CreateResourceComplete(path, resource, original, additionalData);
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnFileLoaded(ResourceHandle* resource, CiByteString path, bool success, bool custom, ReadOnlySpan<byte> _)
    {
        if (_ephemeral.EnableResourceLogging && FilterMatch(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [FILE] [{resource->FileType}] Loading {match} from {(custom ? "local files" : "SqPack")} into 0x{(ulong)resource:X} returned {success}.");

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = Record.CreateFileLoad(path, resource, success, custom);
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnResourceDestroyed(ResourceHandle* resource)
    {
        if (_ephemeral.EnableResourceLogging && FilterMatch(resource->FileName(), out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DEST] [{resource->FileType}] Destroyed {match} at 0x{(ulong)resource:X}.");

        if (!_ephemeral.EnableResourceWatcher)
            return;

        var record = Record.CreateDestruction(resource);
        if (!_ephemeral.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    public unsafe string Name(ResolveData resolve, string none = "")
    {
        if (resolve.AssociatedGameObject == nint.Zero || !_actors.Awaiter.IsCompletedSuccessfully)
            return none;

        try
        {
            var id = _actors.FromObject((GameObject*)resolve.AssociatedGameObject, out _, false, true, true);
            if (id.IsValid)
            {
                if (id.Type is not (IdentifierType.Player or IdentifierType.Owned))
                    return id.ToString();

                var parts = id.ToString().Split(' ', 3);
                return string.Join(" ",
                    parts.Length != 3 ? parts.Select(n => $"{n[0]}.") : parts[..2].Select(n => $"{n[0]}.").Append(parts[2]));
            }
        }
        catch
        {
            // ignored
        }

        return $"0x{resolve.AssociatedGameObject:X}";
    }
}
