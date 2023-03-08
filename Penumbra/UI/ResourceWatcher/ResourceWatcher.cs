using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ResourceWatcher : IDisposable, ITab
{
    public const int DefaultMaxEntries = 1024;

    private readonly ResourceLoader            _loader;
    private readonly List< Record >            _records    = new();
    private readonly ConcurrentQueue< Record > _newRecords = new();
    private readonly Table                     _table;
    private          bool                      _writeToLog;
    private          bool                      _isEnabled;
    private          string                    _logFilter = string.Empty;
    private          Regex?                    _logRegex;
    private          int                       _maxEntries;
    private          int                       _newMaxEntries;

    public unsafe ResourceWatcher( ResourceLoader loader )
    {
        _loader                   =  loader;
        _table                    =  new Table( _records );
        _loader.ResourceRequested += OnResourceRequested;
        _loader.ResourceLoaded    += OnResourceLoaded;
        _loader.FileLoaded        += OnFileLoaded;
        UpdateFilter( Penumbra.Config.ResourceLoggingFilter, false );
        _writeToLog    = Penumbra.Config.EnableResourceLogging;
        _isEnabled     = Penumbra.Config.EnableResourceWatcher;
        _maxEntries    = Penumbra.Config.MaxResourceWatcherRecords;
        _newMaxEntries = _maxEntries;
    }

    public unsafe void Dispose()
    {
        Clear();
        _records.TrimExcess();
        _loader.ResourceRequested -= OnResourceRequested;
        _loader.ResourceLoaded    -= OnResourceLoaded;
        _loader.FileLoaded        -= OnFileLoaded;
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

        ImGui.SetCursorPosY( ImGui.GetCursorPosY() + ImGui.GetTextLineHeightWithSpacing() / 2 );
        if( ImGui.Checkbox( "Enable", ref _isEnabled ) )
        {
            Penumbra.Config.EnableResourceWatcher = _isEnabled;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        DrawMaxEntries();
        ImGui.SameLine();
        if( ImGui.Button( "Clear" ) )
        {
            Clear();
        }

        ImGui.SameLine();
        if( ImGui.Checkbox( "Write to Log", ref _writeToLog ) )
        {
            Penumbra.Config.EnableResourceLogging = _writeToLog;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        DrawFilterInput();

        ImGui.SetCursorPosY( ImGui.GetCursorPosY() + ImGui.GetTextLineHeightWithSpacing() / 2 );

        _table.Draw( ImGui.GetTextLineHeightWithSpacing() );
    }

    private void DrawFilterInput()
    {
        ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
        var       tmp          = _logFilter;
        var       invalidRegex = _logRegex == null && _logFilter.Length > 0;
        using var color        = ImRaii.PushColor( ImGuiCol.Border, Colors.RegexWarningBorder, invalidRegex );
        using var style        = ImRaii.PushStyle( ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, invalidRegex );
        if( ImGui.InputTextWithHint( "##logFilter", "If path matches this Regex...", ref tmp, 256 ) )
        {
            UpdateFilter( tmp, true );
        }
    }

    private void UpdateFilter( string newString, bool config )
    {
        if( newString == _logFilter )
        {
            return;
        }

        _logFilter = newString;
        try
        {
            _logRegex = new Regex( _logFilter, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase );
        }
        catch
        {
            _logRegex = null;
        }

        if( config )
        {
            Penumbra.Config.ResourceLoggingFilter = newString;
            Penumbra.Config.Save();
        }
    }

    private bool FilterMatch( ByteString path, out string match )
    {
        match = path.ToString();
        return _logFilter.Length == 0 || ( _logRegex?.IsMatch( match ) ?? false ) || match.Contains( _logFilter, StringComparison.OrdinalIgnoreCase );
    }


    private void DrawMaxEntries()
    {
        ImGui.SetNextItemWidth( 80 * ImGuiHelpers.GlobalScale );
        ImGui.InputInt( "Max. Entries", ref _newMaxEntries, 0, 0 );
        var change = ImGui.IsItemDeactivatedAfterEdit();
        if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) && ImGui.GetIO().KeyCtrl )
        {
            change         = true;
            _newMaxEntries = DefaultMaxEntries;
        }

        if( _maxEntries != DefaultMaxEntries && ImGui.IsItemHovered() )
        {
            ImGui.SetTooltip( $"CTRL + Right-Click to reset to default {DefaultMaxEntries}." );
        }

        if( !change )
        {
            return;
        }

        _newMaxEntries = Math.Max( 16, _newMaxEntries );
        if( _newMaxEntries != _maxEntries )
        {
            _maxEntries                               = _newMaxEntries;
            Penumbra.Config.MaxResourceWatcherRecords = _maxEntries;
            Penumbra.Config.Save();
            _records.RemoveRange( 0, _records.Count - _maxEntries );
        }
    }

    private void UpdateRecords()
    {
        var count = _newRecords.Count;
        if( count > 0 )
        {
            while( _newRecords.TryDequeue( out var rec ) && count-- > 0 )
            {
                _records.Add( rec );
            }

            if( _records.Count > _maxEntries )
            {
                _records.RemoveRange( 0, _records.Count - _maxEntries );
            }

            _table.Reset();
        }
    }


    private void OnResourceRequested( Utf8GamePath data, bool synchronous )
    {
        if( _writeToLog && FilterMatch( data.Path, out var match ) )
        {
            Penumbra.Log.Information( $"[ResourceLoader] [REQ] {match} was requested {( synchronous ? "synchronously." : "asynchronously." )}" );
        }

        if( _isEnabled )
        {
            _newRecords.Enqueue( Record.CreateRequest( data.Path, synchronous ) );
        }
    }

    private unsafe void OnResourceLoaded( ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, ResolveData data )
    {
        if( _writeToLog )
        {
            var log   = FilterMatch( path.Path, out var name );
            var name2 = string.Empty;
            if( manipulatedPath != null )
            {
                log |= FilterMatch( manipulatedPath.Value.InternalName, out name2 );
            }

            if( log )
            {
                var pathString = manipulatedPath != null ? $"custom file {name2} instead of {name}" : name;
                Penumbra.Log.Information(
                    $"[ResourceLoader] [LOAD] [{handle->FileType}] Loaded {pathString} to 0x{( ulong )handle:X} using collection {data.ModCollection.AnonymizedName} for {data.AssociatedName()} (Refcount {handle->RefCount}) " );
            }
        }

        if( _isEnabled )
        {
            var record = manipulatedPath == null
                ? Record.CreateDefaultLoad( path.Path, handle, data.ModCollection )
                : Record.CreateLoad( path.Path, manipulatedPath.Value.InternalName, handle, data.ModCollection );
            _newRecords.Enqueue( record );
        }
    }

    private unsafe void OnFileLoaded( ResourceHandle* resource, ByteString path, bool success, bool custom )
    {
        if( _writeToLog && FilterMatch( path, out var match ) )
        {
            Penumbra.Log.Information(
                $"[ResourceLoader] [FILE] [{resource->FileType}] Loading {match} from {( custom ? "local files" : "SqPack" )} into 0x{( ulong )resource:X} returned {success}." );
        }

        if( _isEnabled )
        {
            _newRecords.Enqueue( Record.CreateFileLoad( path, resource, success, custom ) );
        }
    }
}