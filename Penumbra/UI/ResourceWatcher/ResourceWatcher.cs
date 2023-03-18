using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ResourceWatcher : IDisposable, ITab
{
    public const int DefaultMaxEntries = 1024;

    private readonly Configuration           _config;
    private readonly ResourceService         _resources;
    private readonly ResourceLoader          _loader;
    private readonly List<Record>            _records    = new();
    private readonly ConcurrentQueue<Record> _newRecords = new();
    private readonly Table                   _table;
    private          string                  _logFilter = string.Empty;
    private          Regex?                  _logRegex;
    private          int                     _newMaxEntries;

    public unsafe ResourceWatcher(Configuration config, ResourceService resources, ResourceLoader loader)
    {
        _config                             =  config;
        _resources                          =  resources;
        _loader                             =  loader;
        _table                              =  new Table(_records);
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
            Penumbra.Config.EnableResourceWatcher = isEnabled;
            Penumbra.Config.Save();
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
            Penumbra.Config.OnlyAddMatchingResources = onlyMatching;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        var writeToLog = _config.EnableResourceLogging;
        if (ImGui.Checkbox("Write to Log", ref writeToLog))
        {
            Penumbra.Config.EnableResourceLogging = writeToLog;
            Penumbra.Config.Save();
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
            Penumbra.Config.ResourceLoggingFilter = newString;
            Penumbra.Config.Save();
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
        if (_newMaxEntries != maxEntries)
        {
            _config.MaxResourceWatcherRecords = _newMaxEntries;
            Penumbra.Config.Save();
            if (_newMaxEntries > _records.Count)
                _records.RemoveRange(0, _records.Count - _newMaxEntries);
        }
    }

    private void UpdateRecords()
    {
        var count = _newRecords.Count;
        if (count > 0)
        {
            while (_newRecords.TryDequeue(out var rec) && count-- > 0)
                _records.Add(rec);

            if (_records.Count > _config.MaxResourceWatcherRecords)
                _records.RemoveRange(0, _records.Count - _config.MaxResourceWatcherRecords);

            _table.Reset();
        }
    }


    private unsafe void OnResourceRequested(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path,
        GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (_config.EnableResourceLogging && FilterMatch(path.Path, out var match))
            Penumbra.Log.Information($"[ResourceLoader] [REQ] {match} was requested {(sync ? "synchronously." : "asynchronously.")}");

        if (_config.EnableResourceWatcher)
        {
            var record = Record.CreateRequest(path.Path, sync);
            if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
                _newRecords.Enqueue(record);
        }
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
                    $"[ResourceLoader] [LOAD] [{handle->FileType}] Loaded {pathString} to 0x{(ulong)handle:X} using collection {data.ModCollection.AnonymizedName} for {data.AssociatedName()} (Refcount {handle->RefCount}) ");
            }
        }

        if (_config.EnableResourceWatcher)
        {
            var record = manipulatedPath == null
                ? Record.CreateDefaultLoad(path.Path, handle, data.ModCollection)
                : Record.CreateLoad(path.Path, manipulatedPath.Value.InternalName, handle,
                    data.ModCollection);
            if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
                _newRecords.Enqueue(record);
        }
    }

    private unsafe void OnFileLoaded(ResourceHandle* resource, ByteString path, bool success, bool custom, ByteString _)
    {
        if (_config.EnableResourceLogging && FilterMatch(path, out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [FILE] [{resource->FileType}] Loading {match} from {(custom ? "local files" : "SqPack")} into 0x{(ulong)resource:X} returned {success}.");

        if (_config.EnableResourceWatcher)
        {
            var record = Record.CreateFileLoad(path, resource, success, custom);
            if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
                _newRecords.Enqueue(record);
        }
    }

    private unsafe void OnResourceDestroyed(ResourceHandle* resource)
    {
        if (_config.EnableResourceLogging && FilterMatch(resource->FileName(), out var match))
            Penumbra.Log.Information(
                $"[ResourceLoader] [DEST] [{resource->FileType}] Destroyed {match} at 0x{(ulong)resource:X}.");

        if (_config.EnableResourceWatcher)
        {
            var record = Record.CreateDestruction(resource);
            if (!_config.OnlyAddMatchingResources || _table.WouldBeVisible(record))
                _newRecords.Enqueue(record);
        }
    }
}
