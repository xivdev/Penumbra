using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImSharp;
using ImSharp.Containers;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ResourceWatcher;

public sealed class ResourceWatcher : IDisposable, ITab<TabType>
{
    public const int DefaultMaxEntries = 500;

    private readonly FilterConfig             _config;
    private readonly ResourceService          _resources;
    private readonly ResourceLoader           _loader;
    private readonly ResourceHandleDestructor _destructor;
    private readonly ActorManager             _actors;
    private readonly ObservableList<Record>   _records    = [];
    private readonly ConcurrentQueue<Record>  _newRecords = [];
    private readonly ResourceWatcherTable     _table;
    private readonly RegexFilter              _filter = new();

    public unsafe ResourceWatcher(ActorManager actors, FilterConfig config, ResourceService resources, ResourceLoader loader,
        ResourceHandleDestructor destructor)
    {
        _config                      =  config;
        _actors                      =  actors;
        _resources                   =  resources;
        _destructor                  =  destructor;
        _loader                      =  loader;
        _table                       =  new ResourceWatcherTable(_config, _records);
        _resources.ResourceRequested += OnResourceRequested;
        _destructor.Subscribe(OnResourceDestroyed, ResourceHandleDestructor.Priority.ResourceWatcher);
        _loader.ResourceLoaded   += OnResourceLoaded;
        _loader.ResourceComplete += OnResourceComplete;
        _loader.FileLoaded       += OnFileLoaded;
        _loader.PapRequested     += OnPapRequested;
        _filter.Set(_config.ResourceLoggerLogFilter);
        _filter.FilterChanged += () => _config.ResourceLoggerLogFilter = _filter.Text;
    }

    private void OnPapRequested(Utf8GamePath original, FullPath? _1, ResolveData _2)
    {
        if (_config.ResourceLoggerWriteToLog && Filter(original.Path, out var path))
        {
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {path} was requested asynchronously.");
            if (_1.HasValue)
                Penumbra.Log.Information(
                    $"[ResourceLoader] [LOAD] Resolved {_1.Value.FullName} for {path} from collection {_2.ModCollection} for object 0x{_2.AssociatedGameObject:X}.");
        }

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = _1.HasValue
            ? Record.CreateRequest(original.Path, false, _1.Value, _2)
            : Record.CreateRequest(original.Path, false);
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Filter(CiByteString path, out string ret)
    {
        ret = path.ToString();
        return _filter.WouldBeVisible(ret);
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
    }

    public ReadOnlySpan<byte> Label
        => "Resource Logger"u8;

    public TabType Identifier
        => TabType.ResourceWatcher;

    public void DrawContent()
    {
        UpdateRecords();

        Im.Cursor.Y += Im.Style.TextHeightWithSpacing / 2;
        if (Im.Checkbox("Enable"u8, _config.ResourceLoggerEnabled))
            _config.ResourceLoggerEnabled ^= true;

        Im.Line.Same();
        DrawMaxEntries();
        Im.Line.Same();
        if (Im.Button("Clear"u8))
            Clear();

        Im.Line.Same();
        if (Im.Checkbox("Store Only Matching"u8, _config.ResourceLoggerStoreOnlyMatching))
            _config.ResourceLoggerStoreOnlyMatching ^= true;

        Im.Line.Same();
        if (Im.Checkbox("Write to Log"u8, _config.ResourceLoggerWriteToLog))
            _config.ResourceLoggerWriteToLog ^= true;

        Im.Line.Same();
        DrawFilterInput();

        Im.Cursor.Y += Im.Style.TextHeightWithSpacing / 2;

        _table.Draw();
    }

    private void DrawFilterInput()
        => _filter.DrawFilter("If path matches this Regex..."u8, Im.ContentRegion.Available);

    private void DrawMaxEntries()
    {
        Im.Item.SetNextWidthScaled(80);
        if (ImEx.InputOnDeactivation.Scalar("Max. Entries"u8, _config.ResourceLoggerMaxEntries, out var newValue))
            _config.ResourceLoggerMaxEntries = Math.Max(16, newValue);

        if (Im.Item.RightClicked() && Im.Io.KeyControl)
            _config.ResourceLoggerMaxEntries = DefaultMaxEntries;

        if (_config.ResourceLoggerMaxEntries is not DefaultMaxEntries && Im.Item.Hovered())
            Im.Tooltip.Set("Control + Right-Click to reset to default 500."u8);

        if (_records.Count > _config.ResourceLoggerMaxEntries)
            _records.RemoveRange(0, _records.Count - _config.ResourceLoggerMaxEntries);
    }

    private void UpdateRecords()
    {
        var count = _newRecords.Count;
        if (count <= 0)
            return;

        while (_newRecords.TryDequeue(out var rec) && count-- > 0)
            _records.Add(rec);

        if (_records.Count > _config.ResourceLoggerMaxEntries)
            _records.RemoveRange(0, _records.Count - _config.ResourceLoggerMaxEntries);
    }


    private unsafe void OnResourceRequested(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path,
        Utf8GamePath original, GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (_config.ResourceLoggerWriteToLog && Filter(original.Path, out var match))
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {match} was requested {(sync ? "synchronously." : "asynchronously.")}");

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = Record.CreateRequest(original.Path, sync);
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
    }

    private unsafe void OnResourceLoaded(ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, ResolveData data)
    {
        if (_config.ResourceLoggerWriteToLog)
        {
            var log   = Filter(path.Path, out var name);
            var name2 = string.Empty;
            if (manipulatedPath is not null)
                log |= Filter(manipulatedPath.Value.InternalName, out name2);

            if (log)
            {
                var pathString = manipulatedPath is not null ? $"custom file {name2} instead of {name}" : name;
                Penumbra.Log.Information(
                    $"[ResourceLoader] [LOAD] [{handle->FileType}] Loaded {pathString} to 0x{(ulong)handle:X} using collection {data.ModCollection.Identity.AnonymizedName} for {Name(data, "no associated object.")} (Refcount {handle->RefCount}) ");
            }
        }

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = manipulatedPath == null
            ? Record.CreateDefaultLoad(path.Path, handle, data.ModCollection, Name(data))
            : Record.CreateLoad(manipulatedPath.Value, path.Path, handle, data.ModCollection, Name(data));
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
    }

    private unsafe void OnResourceComplete(ResourceHandle* resource, CiByteString path, Utf8GamePath original,
        ReadOnlySpan<byte> additionalData, bool isAsync)
    {
        if (!isAsync)
            return;

        if (_config.ResourceLoggerWriteToLog && Filter(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DONE] [{resource->FileType}] Finished loading {match} into 0x{(ulong)resource:X}, state {resource->LoadState}.");

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = Record.CreateResourceComplete(path, resource, original, additionalData);
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
    }

    private unsafe void OnFileLoaded(ResourceHandle* resource, CiByteString path, bool success, bool custom, ReadOnlySpan<byte> _)
    {
        if (_config.ResourceLoggerWriteToLog && Filter(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [FILE] [{resource->FileType}] Loading {match} from {(custom ? "local files" : "SqPack")} into 0x{(ulong)resource:X} returned {success}.");

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = Record.CreateFileLoad(path, resource, success, custom);
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
    }

    private unsafe void OnResourceDestroyed(in ResourceHandleDestructor.Arguments arguments)
    {
        if (_config.ResourceLoggerWriteToLog && Filter(arguments.ResourceHandle->FileName(), out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DEST] [{arguments.ResourceHandle->FileType}] Destroyed {match} at 0x{(ulong)arguments.ResourceHandle:X}.");

        if (!_config.ResourceLoggerEnabled)
            return;

        var record = Record.CreateDestruction(arguments.ResourceHandle);
        if (!_config.ResourceLoggerStoreOnlyMatching || _table.WouldBeVisible(record))
            Enqueue(record);
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

    private void Enqueue(Record record)
    {
        // Discard entries that exceed the number of records.
        while (_newRecords.Count >= _config.ResourceLoggerMaxEntries)
            _newRecords.TryDequeue(out _);
        _newRecords.Enqueue(record);
    }
}
