using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ResourceWatcher;

public class ResourceWatcher : IDisposable, ITab
{
    public const int        DefaultMaxEntries = 1024;
    public const RecordType AllRecords        = RecordType.Request | RecordType.ResourceLoad | RecordType.FileLoad | RecordType.Destruction;

    private readonly Configuration           _config;
    private readonly ResourceService         _resources;
    private readonly ResourceLoader          _loader;
    private readonly ActorService            _actors;
    private readonly List<Record>            _records    = new();
    private readonly ConcurrentQueue<Record> _newRecords = new();
    private readonly ResourceWatcherTable    _table;
    private          string                  _logFilter = string.Empty;
    private          Regex?                  _logRegex;
    private          int                     _newMaxEntries;

    public unsafe ResourceWatcher(ActorService actors, Configuration config, ResourceService resources, ResourceLoader loader)
    {
        _actors                             =  actors;
        _config                             =  config;
        _resources                          =  resources;
        _loader                             =  loader;
        _table                              =  new ResourceWatcherTable(config, _records);
        _resources.ResourceRequested        += OnResourceRequested;
        _resources.ResourceHandleDestructor += OnResourceDestroyed;
        _loader.ResourceLoaded              += OnResourceLoaded;
        _loader.FileLoaded                  += OnFileLoaded;
        UpdateFilter(_config.ResourceLoggingFilter, false);
        _newMaxEntries = _config.MaxResourceWatcherRecords;
    }

    public unsafe void Dispose()
    {
        Clear();
        _records.TrimExcess();
        _resources.ResourceRequested        -= OnResourceRequested;
        _resources.ResourceHandleDestructor -= OnResourceDestroyed;
        _loader.ResourceLoaded              -= OnResourceLoaded;
        _loader.FileLoaded                  -= OnFileLoaded;
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
        var isEnabled = _config.EnableResourceWatcher;
        if (ImGui.Checkbox("Enable", ref isEnabled))
        {
            _config.EnableResourceWatcher = isEnabled;
            _config.Save();
        }

        ImGui.SameLine();
        DrawMaxEntries();
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            Clear();

        ImGui.SameLine();
        var onlyMatching = _config.OnlyAddMatchingResources;
        if (ImGui.Checkbox("Store Only Matching", ref onlyMatching))
        {
            _config.OnlyAddMatchingResources = onlyMatching;
            _config.Save();
        }

        ImGui.SameLine();
        var writeToLog = _config.EnableResourceLogging;
        if (ImGui.Checkbox("Write to Log", ref writeToLog))
        {
            _config.EnableResourceLogging = writeToLog;
            _config.Save();
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
            _config.ResourceLoggingFilter = newString;
            _config.Save();
        }
    }

    private bool FilterMatch(ByteString path, out string match)
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
        Utf8GamePath original,
        GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (_config.EnableResourceLogging && FilterMatch(original.Path, out var match))
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {match} was requested {(sync ? "synchronously." : "asynchronously.")}");

        if (!_config.EnableResourceWatcher)
            return;

        var record = Record.CreateRequest(original.Path, sync);
        if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, ResolveData data)
    {
        if (_config.EnableResourceLogging)
        {
            var log   = FilterMatch(path.Path, out var name);
            var name2 = string.Empty;
            if (manipulatedPath != null)
                log |= FilterMatch(manipulatedPath.Value.InternalName, out name2);

            if (log)
            {
                var pathString = manipulatedPath != null ? $"custom file {name2} instead of {name}" : name;
                Penumbra.Log.Information(
                    $"[ResourceLoader] [LOAD] [{handle->FileType}] Loaded {pathString} to 0x{(ulong)handle:X} using collection {data.ModCollection.AnonymizedName} for {Name(data, "no associated object.")} (Refcount {handle->RefCount}) ");
            }
        }

        if (!_config.EnableResourceWatcher)
            return;

        var record = manipulatedPath == null
            ? Record.CreateDefaultLoad(path.Path, handle, data.ModCollection, Name(data))
            : Record.CreateLoad(manipulatedPath.Value.InternalName, path.Path, handle, data.ModCollection, Name(data));
        if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnFileLoaded(ResourceHandle* resource, ByteString path, bool success, bool custom, ByteString _)
    {
        if (_config.EnableResourceLogging && FilterMatch(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [FILE] [{resource->FileType}] Loading {match} from {(custom ? "local files" : "SqPack")} into 0x{(ulong)resource:X} returned {success}.");

        if (!_config.EnableResourceWatcher)
            return;

        var record = Record.CreateFileLoad(path, resource, success, custom);
        if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    private unsafe void OnResourceDestroyed(ResourceHandle* resource)
    {
        if (_config.EnableResourceLogging && FilterMatch(resource->FileName(), out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DEST] [{resource->FileType}] Destroyed {match} at 0x{(ulong)resource:X}.");

        if (!_config.EnableResourceWatcher)
            return;

        var record = Record.CreateDestruction(resource);
        if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
            _newRecords.Enqueue(record);
    }

    public unsafe string Name(ResolveData resolve, string none = "")
    {
        if (resolve.AssociatedGameObject == IntPtr.Zero || !_actors.Valid)
            return none;

        try
        {
            var id = _actors.AwaitedService.FromObject((GameObject*)resolve.AssociatedGameObject, out _, false, true, true);
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
