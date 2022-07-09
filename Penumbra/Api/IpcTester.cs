using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Mods;

namespace Penumbra.Api;

public class IpcTester : IDisposable
{
    private readonly PenumbraIpc            _ipc;
    private readonly DalamudPluginInterface _pi;

    private readonly ICallGateSubscriber< object? >                                         _initialized;
    private readonly ICallGateSubscriber< object? >                                         _disposed;
    private readonly ICallGateSubscriber< string, object? >                                 _preSettingsDraw;
    private readonly ICallGateSubscriber< string, object? >                                 _postSettingsDraw;
    private readonly ICallGateSubscriber< IntPtr, int, object? >                            _redrawn;
    private readonly ICallGateSubscriber< ModSettingChange, string, string, bool, object? > _settingChanged;
    private readonly ICallGateSubscriber< IntPtr, string, IntPtr, IntPtr, IntPtr, object? > _characterBaseCreated;

    private readonly List< DateTimeOffset > _initializedList = new();
    private readonly List< DateTimeOffset > _disposedList    = new();

    public IpcTester( DalamudPluginInterface pi, PenumbraIpc ipc )
    {
        _ipc = ipc;
        _pi = pi;
        _initialized = _pi.GetIpcSubscriber< object? >( PenumbraIpc.LabelProviderInitialized );
        _disposed = _pi.GetIpcSubscriber< object? >( PenumbraIpc.LabelProviderDisposed );
        _redrawn = _pi.GetIpcSubscriber< IntPtr, int, object? >( PenumbraIpc.LabelProviderGameObjectRedrawn );
        _preSettingsDraw = _pi.GetIpcSubscriber< string, object? >( PenumbraIpc.LabelProviderPreSettingsDraw );
        _postSettingsDraw = _pi.GetIpcSubscriber< string, object? >( PenumbraIpc.LabelProviderPostSettingsDraw );
        _settingChanged = _pi.GetIpcSubscriber< ModSettingChange, string, string, bool, object? >( PenumbraIpc.LabelProviderModSettingChanged );
        _characterBaseCreated =
            _pi.GetIpcSubscriber< IntPtr, string, IntPtr, IntPtr, IntPtr, object? >( PenumbraIpc.LabelProviderCreatingCharacterBase );
        _initialized.Subscribe( AddInitialized );
        _disposed.Subscribe( AddDisposed );
        _redrawn.Subscribe( SetLastRedrawn );
        _preSettingsDraw.Subscribe( UpdateLastDrawnMod );
        _postSettingsDraw.Subscribe( UpdateLastDrawnMod );
        _settingChanged.Subscribe( UpdateLastModSetting );
        _characterBaseCreated.Subscribe( UpdateLastCreated );
    }

    public void Dispose()
    {
        _initialized.Unsubscribe( AddInitialized );
        _disposed.Unsubscribe( AddDisposed );
        _redrawn.Subscribe( SetLastRedrawn );
        _tooltip?.Unsubscribe( AddedTooltip );
        _click?.Unsubscribe( AddedClick );
        _preSettingsDraw.Unsubscribe( UpdateLastDrawnMod );
        _postSettingsDraw.Unsubscribe( UpdateLastDrawnMod );
        _settingChanged.Unsubscribe( UpdateLastModSetting );
        _characterBaseCreated.Unsubscribe( UpdateLastCreated );
    }

    private void AddInitialized()
        => _initializedList.Add( DateTimeOffset.UtcNow );

    private void AddDisposed()
        => _disposedList.Add( DateTimeOffset.UtcNow );

    public void Draw()
    {
        try
        {
            DrawAvailable();
            DrawGeneral();
            DrawResolve();
            DrawRedraw();
            DrawChangedItems();
            DrawData();
            DrawSetting();
            DrawTemp();
            DrawTempCollections();
            DrawTempMods();
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error during IPC Tests:\n{e}" );
        }
    }

    private void DrawAvailable()
    {
        using var _ = ImRaii.TreeNode( "Availability" );
        if( !_ )
        {
            return;
        }

        ImGui.TextUnformatted( $"API Version: {_ipc.Api.ApiVersion.Breaking}.{_ipc.Api.ApiVersion.Feature:D4}" );
        ImGui.TextUnformatted( "Available subscriptions:" );
        using var indent = ImRaii.PushIndent();

        var dict = _ipc.GetType().GetFields( BindingFlags.Static | BindingFlags.Public ).Where( f => f.IsLiteral )
           .ToDictionary( f => f.Name, f => f.GetValue( _ipc ) as string );
        foreach( var provider in _ipc.GetType().GetFields( BindingFlags.Instance | BindingFlags.NonPublic ) )
        {
            var value = provider.GetValue( _ipc );
            if( value != null && dict.TryGetValue( "Label" + provider.Name, out var label ) )
            {
                ImGui.TextUnformatted( label );
            }
        }
    }

    private static void DrawIntro( string label, string info )
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted( label );
        ImGui.TableNextColumn();
        ImGui.TextUnformatted( info );
        ImGui.TableNextColumn();
    }

    private string         _currentConfiguration = string.Empty;
    private string         _lastDrawnMod         = string.Empty;
    private DateTimeOffset _lastDrawnModTime;

    private void UpdateLastDrawnMod( string name )
        => ( _lastDrawnMod, _lastDrawnModTime ) = ( name, DateTimeOffset.Now );

    private void DrawGeneral()
    {
        using var _ = ImRaii.TreeNode( "General IPC" );
        if( !_ )
        {
            return;
        }

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );

        void DrawList( string label, string text, List< DateTimeOffset > list )
        {
            DrawIntro( label, text );
            if( list.Count == 0 )
            {
                ImGui.TextUnformatted( "Never" );
            }
            else
            {
                ImGui.TextUnformatted( list[ ^1 ].LocalDateTime.ToString( CultureInfo.CurrentCulture ) );
                if( list.Count > 1 && ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( string.Join( "\n",
                        list.SkipLast( 1 ).Select( t => t.LocalDateTime.ToString( CultureInfo.CurrentCulture ) ) ) );
                }
            }
        }

        DrawList( PenumbraIpc.LabelProviderInitialized, "Last Initialized", _initializedList );
        DrawList( PenumbraIpc.LabelProviderDisposed, "Last Disposed", _disposedList );
        DrawIntro( PenumbraIpc.LabelProviderPostSettingsDraw, "Last Drawn Mod" );
        ImGui.TextUnformatted( _lastDrawnMod.Length > 0 ? $"{_lastDrawnMod} at {_lastDrawnModTime}" : "None" );
        DrawIntro( PenumbraIpc.LabelProviderApiVersions, "Current Version" );
        var (breaking, features) = _pi.GetIpcSubscriber< (int, int) >( PenumbraIpc.LabelProviderApiVersions ).InvokeFunc();
        ImGui.TextUnformatted( $"{breaking}.{features:D4}" );
        DrawIntro( PenumbraIpc.LabelProviderGetModDirectory, "Current Mod Directory" );
        ImGui.TextUnformatted( _pi.GetIpcSubscriber< string >( PenumbraIpc.LabelProviderGetModDirectory ).InvokeFunc() );
        DrawIntro( PenumbraIpc.LabelProviderGetConfiguration, "Configuration" );
        if( ImGui.Button( "Get" ) )
        {
            _currentConfiguration = _pi.GetIpcSubscriber< string >( PenumbraIpc.LabelProviderGetConfiguration ).InvokeFunc();
            ImGui.OpenPopup( "Config Popup" );
        }

        ImGui.SetNextWindowSize( ImGuiHelpers.ScaledVector2( 500, 500 ) );
        using var popup = ImRaii.Popup( "Config Popup" );
        if( popup )
        {
            using( var font = ImRaii.PushFont( UiBuilder.MonoFont ) )
            {
                ImGuiUtil.TextWrapped( _currentConfiguration );
            }

            if( ImGui.Button( "Close", -Vector2.UnitX ) || ImGui.IsWindowFocused() )
            {
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private string         _currentResolvePath        = string.Empty;
    private string         _currentResolveCharacter   = string.Empty;
    private string         _currentDrawObjectString   = string.Empty;
    private string         _currentReversePath        = string.Empty;
    private IntPtr         _currentDrawObject         = IntPtr.Zero;
    private string         _lastCreatedGameObjectName = string.Empty;
    private DateTimeOffset _lastCreatedGameObjectTime = DateTimeOffset.MaxValue;

    private unsafe void UpdateLastCreated( IntPtr gameObject, string _, IntPtr _2, IntPtr _3, IntPtr _4 )
    {
        var obj = ( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* )gameObject;
        _lastCreatedGameObjectName = new Utf8String( obj->GetName() ).ToString();
        _lastCreatedGameObjectTime = DateTimeOffset.Now;
    }

    private void DrawResolve()
    {
        using var _ = ImRaii.TreeNode( "Resolve IPC" );
        if( !_ )
        {
            return;
        }

        ImGui.InputTextWithHint( "##resolvePath", "Resolve this game path...", ref _currentResolvePath, Utf8GamePath.MaxGamePathLength );
        ImGui.InputTextWithHint( "##resolveCharacter", "Character Name (leave blank for default)...", ref _currentResolveCharacter, 32 );
        ImGui.InputTextWithHint( "##resolveInversePath", "Reverse-resolve this path...", ref _currentReversePath,
            Utf8GamePath.MaxGamePathLength );
        if( ImGui.InputTextWithHint( "##drawObject", "Draw Object Address..", ref _currentDrawObjectString, 16,
               ImGuiInputTextFlags.CharsHexadecimal ) )
        {
            _currentDrawObject = IntPtr.TryParse( _currentDrawObjectString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp )
                ? tmp
                : IntPtr.Zero;
        }

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( PenumbraIpc.LabelProviderResolveDefault, "Default Collection Resolve" );
        if( _currentResolvePath.Length != 0 )
        {
            ImGui.TextUnformatted( _pi.GetIpcSubscriber< string, string >( PenumbraIpc.LabelProviderResolveDefault )
               .InvokeFunc( _currentResolvePath ) );
        }

        DrawIntro( PenumbraIpc.LabelProviderResolveCharacter, "Character Collection Resolve" );
        if( _currentResolvePath.Length != 0 && _currentResolveCharacter.Length != 0 )
        {
            ImGui.TextUnformatted( _pi.GetIpcSubscriber< string, string, string >( PenumbraIpc.LabelProviderResolveCharacter )
               .InvokeFunc( _currentResolvePath, _currentResolveCharacter ) );
        }

        DrawIntro( PenumbraIpc.LabelProviderGetDrawObjectInfo, "Draw Object Info" );
        if( _currentDrawObject == IntPtr.Zero )
        {
            ImGui.TextUnformatted( "Invalid" );
        }
        else
        {
            var (ptr, collection) = _pi.GetIpcSubscriber< IntPtr, (IntPtr, string) >( PenumbraIpc.LabelProviderGetDrawObjectInfo )
               .InvokeFunc( _currentDrawObject );
            ImGui.TextUnformatted( ptr == IntPtr.Zero ? $"No Actor Associated, {collection}" : $"{ptr:X}, {collection}" );
        }

        DrawIntro( PenumbraIpc.LabelProviderReverseResolvePath, "Reversed Game Paths" );
        if( _currentReversePath.Length > 0 )
        {
            var list = _pi.GetIpcSubscriber< string, string, string[] >( PenumbraIpc.LabelProviderReverseResolvePath )
               .InvokeFunc( _currentReversePath, _currentResolveCharacter );
            if( list.Length > 0 )
            {
                ImGui.TextUnformatted( list[ 0 ] );
                if( list.Length > 1 && ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( string.Join( "\n", list.Skip( 1 ) ) );
                }
            }
        }

        DrawIntro( PenumbraIpc.LabelProviderReverseResolvePlayerPath, "Reversed Game Paths (Player)" );
        if( _currentReversePath.Length > 0 )
        {
            var list = _pi.GetIpcSubscriber< string, string[] >( PenumbraIpc.LabelProviderReverseResolvePlayerPath )
               .InvokeFunc( _currentReversePath );
            if( list.Length > 0 )
            {
                ImGui.TextUnformatted( list[ 0 ] );
                if( list.Length > 1 && ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( string.Join( "\n", list.Skip( 1 ) ) );
                }
            }
        }

        DrawIntro( PenumbraIpc.LabelProviderCreatingCharacterBase, "Last Drawobject created" );
        if( _lastCreatedGameObjectTime < DateTimeOffset.Now )
        {
            ImGui.TextUnformatted( $"for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}" );
        }
    }

    private string _redrawName        = string.Empty;
    private int    _redrawIndex       = 0;
    private string _lastRedrawnString = "None";

    private void SetLastRedrawn( IntPtr address, int index )
    {
        if( index < 0 || index > Dalamud.Objects.Length || address == IntPtr.Zero || Dalamud.Objects[ index ]?.Address != address )
        {
            _lastRedrawnString = "Invalid";
        }

        _lastRedrawnString = $"{Dalamud.Objects[ index ]!.Name} (0x{address:X}, {index})";
    }

    private void DrawRedraw()
    {
        using var _ = ImRaii.TreeNode( "Redraw IPC" );
        if( !_ )
        {
            return;
        }

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( PenumbraIpc.LabelProviderRedrawName, "Redraw by Name" );
        ImGui.SetNextItemWidth( 100 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( "##redrawName", "Name...", ref _redrawName, 32 );
        ImGui.SameLine();
        if( ImGui.Button( "Redraw##Name" ) )
        {
            _pi.GetIpcSubscriber< string, int, object? >( PenumbraIpc.LabelProviderRedrawName )
               .InvokeAction( _redrawName, ( int )RedrawType.Redraw );
        }

        DrawIntro( PenumbraIpc.LabelProviderRedrawObject, "Redraw Player Character" );
        if( ImGui.Button( "Redraw##pc" ) && Dalamud.ClientState.LocalPlayer != null )
        {
            _pi.GetIpcSubscriber< GameObject, int, object? >( PenumbraIpc.LabelProviderRedrawObject )
               .InvokeAction( Dalamud.ClientState.LocalPlayer, ( int )RedrawType.Redraw );
        }

        DrawIntro( PenumbraIpc.LabelProviderRedrawIndex, "Redraw by Index" );
        var tmp = _redrawIndex;
        ImGui.SetNextItemWidth( 100 * ImGuiHelpers.GlobalScale );
        if( ImGui.DragInt( "##redrawIndex", ref tmp, 0.1f, 0, Dalamud.Objects.Length ) )
        {
            _redrawIndex = Math.Clamp( tmp, 0, Dalamud.Objects.Length );
        }

        ImGui.SameLine();
        if( ImGui.Button( "Redraw##Index" ) )
        {
            _pi.GetIpcSubscriber< int, int, object? >( PenumbraIpc.LabelProviderRedrawIndex )
               .InvokeAction( _redrawIndex, ( int )RedrawType.Redraw );
        }

        DrawIntro( PenumbraIpc.LabelProviderRedrawAll, "Redraw All" );
        if( ImGui.Button( "Redraw##All" ) )
        {
            _pi.GetIpcSubscriber< int, object? >( PenumbraIpc.LabelProviderRedrawAll ).InvokeAction( ( int )RedrawType.Redraw );
        }

        DrawIntro( PenumbraIpc.LabelProviderGameObjectRedrawn, "Last Redrawn Object:" );
        ImGui.TextUnformatted( _lastRedrawnString );
    }

    private bool                                                                _subscribedToTooltip   = false;
    private bool                                                                _subscribedToClick     = false;
    private string                                                              _changedItemCollection = string.Empty;
    private IReadOnlyDictionary< string, object? >                              _changedItems          = new Dictionary< string, object? >();
    private string                                                              _lastClicked           = string.Empty;
    private string                                                              _lastHovered           = string.Empty;
    private ICallGateSubscriber< ChangedItemType, uint, object? >?              _tooltip;
    private ICallGateSubscriber< MouseButton, ChangedItemType, uint, object? >? _click;

    private void DrawChangedItems()
    {
        using var _ = ImRaii.TreeNode( "Changed Item IPC" );
        if( !_ )
        {
            return;
        }

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( PenumbraIpc.LabelProviderChangedItemTooltip, "Add Tooltip" );
        if( ImGui.Checkbox( "##tooltip", ref _subscribedToTooltip ) )
        {
            _tooltip = _pi.GetIpcSubscriber< ChangedItemType, uint, object? >( PenumbraIpc.LabelProviderChangedItemTooltip );
            if( _subscribedToTooltip )
            {
                _tooltip.Subscribe( AddedTooltip );
            }
            else
            {
                _tooltip.Unsubscribe( AddedTooltip );
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted( _lastHovered );

        DrawIntro( PenumbraIpc.LabelProviderChangedItemClick, "Subscribe Click" );
        if( ImGui.Checkbox( "##click", ref _subscribedToClick ) )
        {
            _click = _pi.GetIpcSubscriber< MouseButton, ChangedItemType, uint, object? >( PenumbraIpc.LabelProviderChangedItemClick );
            if( _subscribedToClick )
            {
                _click.Subscribe( AddedClick );
            }
            else
            {
                _click.Unsubscribe( AddedClick );
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted( _lastClicked );

        DrawIntro( PenumbraIpc.LabelProviderGetChangedItems, "Changed Item List" );
        ImGui.SetNextItemWidth( 200 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( "##changedCollection", "Collection Name...", ref _changedItemCollection, 64 );
        ImGui.SameLine();
        if( ImGui.Button( "Get" ) )
        {
            _changedItems = _pi.GetIpcSubscriber< string, IReadOnlyDictionary< string, object? > >( PenumbraIpc.LabelProviderGetChangedItems )
               .InvokeFunc( _changedItemCollection );
            ImGui.OpenPopup( "Changed Item List" );
        }

        ImGui.SetNextWindowSize( ImGuiHelpers.ScaledVector2( 500, 500 ) );
        using var p = ImRaii.Popup( "Changed Item List" );
        if( p )
        {
            foreach( var item in _changedItems )
            {
                ImGui.TextUnformatted( item.Key );
            }

            if( ImGui.Button( "Close", -Vector2.UnitX ) || ImGui.IsWindowFocused() )
            {
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private void AddedTooltip( ChangedItemType type, uint id )
    {
        _lastHovered = $"{type} {id} at {DateTime.UtcNow.ToLocalTime().ToString( CultureInfo.CurrentCulture )}";
        ImGui.TextUnformatted( "IPC Test Successful" );
    }

    private void AddedClick( MouseButton button, ChangedItemType type, uint id )
    {
        _lastClicked = $"{button}-click on {type} {id} at {DateTime.UtcNow.ToLocalTime().ToString( CultureInfo.CurrentCulture )}";
    }

    private string                    _characterCollectionName = string.Empty;
    private IList< (string, string) > _mods                    = new List< (string, string) >();
    private IList< string >           _collections             = new List< string >();
    private bool                      _collectionMode          = false;

    private void DrawData()
    {
        using var _ = ImRaii.TreeNode( "Data IPC" );
        if( !_ )
        {
            return;
        }

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( PenumbraIpc.LabelProviderCurrentCollectionName, "Current Collection" );
        ImGui.TextUnformatted( _pi.GetIpcSubscriber< string >( PenumbraIpc.LabelProviderCurrentCollectionName ).InvokeFunc() );
        DrawIntro( PenumbraIpc.LabelProviderDefaultCollectionName, "Default Collection" );
        ImGui.TextUnformatted( _pi.GetIpcSubscriber< string >( PenumbraIpc.LabelProviderDefaultCollectionName ).InvokeFunc() );
        DrawIntro( PenumbraIpc.LabelProviderCharacterCollectionName, "Character" );
        ImGui.SetNextItemWidth( 200 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( "##characterCollectionName", "Character Name...", ref _characterCollectionName, 64 );
        var (c, s) = _pi.GetIpcSubscriber< string, (string, bool) >( PenumbraIpc.LabelProviderCharacterCollectionName )
           .InvokeFunc( _characterCollectionName );
        ImGui.SameLine();
        ImGui.TextUnformatted( $"{c}, {( s ? "Custom" : "Default" )}" );

        DrawIntro( PenumbraIpc.LabelProviderGetCollections, "Collections" );
        if( ImGui.Button( "Get##Collections" ) )
        {
            _collectionMode = true;
            _collections    = _pi.GetIpcSubscriber< IList< string > >( PenumbraIpc.LabelProviderGetCollections ).InvokeFunc();
            ImGui.OpenPopup( "Ipc Data" );
        }

        DrawIntro( PenumbraIpc.LabelProviderGetMods, "Mods" );
        if( ImGui.Button( "Get##Mods" ) )
        {
            _collectionMode = false;
            _mods           = _pi.GetIpcSubscriber< IList< (string, string) > >( PenumbraIpc.LabelProviderGetMods ).InvokeFunc();
            ImGui.OpenPopup( "Ipc Data" );
        }

        DrawIntro( PenumbraIpc.LabelProviderGetMetaManipulations, "Meta Manipulations" );
        if( ImGui.Button( "Copy to Clipboard" ) )
        {
            var base64 = _pi.GetIpcSubscriber< string, string >( PenumbraIpc.LabelProviderGetMetaManipulations )
               .InvokeFunc( _characterCollectionName );
            ImGui.SetClipboardText( base64 );
        }

        ImGui.SetNextWindowSize( ImGuiHelpers.ScaledVector2( 500, 500 ) );
        using var p = ImRaii.Popup( "Ipc Data" );
        if( p )
        {
            if( _collectionMode )
            {
                foreach( var collection in _collections )
                {
                    ImGui.TextUnformatted( collection );
                }
            }
            else
            {
                foreach( var (modDir, modName) in _mods )
                {
                    ImGui.TextUnformatted( $"{modDir}: {modName}" );
                }
            }

            if( ImGui.Button( "Close", -Vector2.UnitX ) || ImGui.IsWindowFocused() )
            {
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private string                                                _settingsModDirectory     = string.Empty;
    private string                                                _settingsModName          = string.Empty;
    private string                                                _settingsCollection       = string.Empty;
    private bool                                                  _settingsAllowInheritance = true;
    private bool                                                  _settingsInherit          = false;
    private bool                                                  _settingsEnabled          = false;
    private int                                                   _settingsPriority         = 0;
    private IDictionary< string, (IList< string >, SelectType) >? _availableSettings;
    private IDictionary< string, IList< string > >?               _currentSettings   = null;
    private PenumbraApiEc                                         _lastSettingsError = PenumbraApiEc.Success;
    private ModSettingChange                                      _lastSettingChangeType;
    private string                                                _lastSettingChangeCollection = string.Empty;
    private string                                                _lastSettingChangeMod        = string.Empty;
    private bool                                                  _lastSettingChangeInherited;
    private DateTimeOffset                                        _lastSettingChange;
    private PenumbraApiEc                                         _lastReloadEc = PenumbraApiEc.Success;


    private void UpdateLastModSetting( ModSettingChange type, string collection, string mod, bool inherited )
    {
        _lastSettingChangeType       = type;
        _lastSettingChangeCollection = collection;
        _lastSettingChangeMod        = mod;
        _lastSettingChangeInherited  = inherited;
        _lastSettingChange           = DateTimeOffset.Now;
    }

    private void DrawSetting()
    {
        using var _ = ImRaii.TreeNode( "Settings IPC" );
        if( !_ )
        {
            return;
        }

        ImGui.InputTextWithHint( "##settingsDir", "Mod Directory Name...", ref _settingsModDirectory, 100 );
        ImGui.InputTextWithHint( "##settingsName", "Mod Name...", ref _settingsModName, 100 );
        ImGui.InputTextWithHint( "##settingsCollection", "Collection...", ref _settingsCollection, 100 );
        ImGui.Checkbox( "Allow Inheritance", ref _settingsAllowInheritance );

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( "Last Error", _lastSettingsError.ToString() );
        DrawIntro( PenumbraIpc.LabelProviderModSettingChanged, "Last Mod Setting Changed" );
        ImGui.TextUnformatted( _lastSettingChangeMod.Length > 0
            ? $"{_lastSettingChangeType} of {_lastSettingChangeMod} in {_lastSettingChangeCollection}{( _lastSettingChangeInherited ? " (Inherited)" : string.Empty )} at {_lastSettingChange}"
            : "None" );
        DrawIntro( PenumbraIpc.LabelProviderGetAvailableModSettings, "Get Available Settings" );
        if( ImGui.Button( "Get##Available" ) )
        {
            _availableSettings = _pi
               .GetIpcSubscriber< string, string, IDictionary< string, (IList< string >, SelectType) >? >(
                    PenumbraIpc.LabelProviderGetAvailableModSettings ).InvokeFunc( _settingsModDirectory, _settingsModName );
            _lastSettingsError = _availableSettings == null ? PenumbraApiEc.ModMissing : PenumbraApiEc.Success;
        }

        DrawIntro( PenumbraIpc.LabelProviderReloadMod, "Reload Mod" );
        if( ImGui.Button( "Reload" ) )
        {
            _lastReloadEc = _pi.GetIpcSubscriber< string, string, PenumbraApiEc >( PenumbraIpc.LabelProviderReloadMod )
               .InvokeFunc( _settingsModDirectory, _settingsModName );
        }

        ImGui.SameLine();
        ImGui.TextUnformatted( _lastReloadEc.ToString() );

        DrawIntro( PenumbraIpc.LabelProviderGetCurrentModSettings, "Get Current Settings" );
        if( ImGui.Button( "Get##Current" ) )
        {
            var ret = _pi
               .GetIpcSubscriber< string, string, string, bool, (PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)?) >(
                    PenumbraIpc.LabelProviderGetCurrentModSettings ).InvokeFunc( _settingsCollection, _settingsModDirectory, _settingsModName,
                    _settingsAllowInheritance );
            _lastSettingsError = ret.Item1;
            if( ret.Item1 == PenumbraApiEc.Success )
            {
                _settingsEnabled  = ret.Item2?.Item1 ?? false;
                _settingsInherit  = ret.Item2?.Item4 ?? false;
                _settingsPriority = ret.Item2?.Item2 ?? 0;
                _currentSettings  = ret.Item2?.Item3;
            }
            else
            {
                _currentSettings = null;
            }
        }

        DrawIntro( PenumbraIpc.LabelProviderTryInheritMod, "Inherit Mod" );
        ImGui.Checkbox( "##inherit", ref _settingsInherit );
        ImGui.SameLine();
        if( ImGui.Button( "Set##Inherit" ) )
        {
            _lastSettingsError = _pi.GetIpcSubscriber< string, string, string, bool, PenumbraApiEc >( PenumbraIpc.LabelProviderTryInheritMod )
               .InvokeFunc( _settingsCollection, _settingsModDirectory, _settingsModName, _settingsInherit );
        }

        DrawIntro( PenumbraIpc.LabelProviderTrySetMod, "Set Enabled" );
        ImGui.Checkbox( "##enabled", ref _settingsEnabled );
        ImGui.SameLine();
        if( ImGui.Button( "Set##Enabled" ) )
        {
            _lastSettingsError = _pi.GetIpcSubscriber< string, string, string, bool, PenumbraApiEc >( PenumbraIpc.LabelProviderTrySetMod )
               .InvokeFunc( _settingsCollection, _settingsModDirectory, _settingsModName, _settingsEnabled );
        }

        DrawIntro( PenumbraIpc.LabelProviderTrySetModPriority, "Set Priority" );
        ImGui.SetNextItemWidth( 200 * ImGuiHelpers.GlobalScale );
        ImGui.DragInt( "##Priority", ref _settingsPriority );
        ImGui.SameLine();
        if( ImGui.Button( "Set##Priority" ) )
        {
            _lastSettingsError = _pi
               .GetIpcSubscriber< string, string, string, int, PenumbraApiEc >( PenumbraIpc.LabelProviderTrySetModPriority )
               .InvokeFunc( _settingsCollection, _settingsModDirectory, _settingsModName, _settingsPriority );
        }

        DrawIntro( PenumbraIpc.LabelProviderTrySetModSetting, "Set Setting(s)" );
        if( _availableSettings != null )
        {
            foreach( var (group, (list, type)) in _availableSettings )
            {
                using var       id      = ImRaii.PushId( group );
                var             preview = list.Count > 0 ? list[ 0 ] : string.Empty;
                IList< string > current;
                if( _currentSettings != null && _currentSettings.TryGetValue( group, out current! ) && current.Count > 0 )
                {
                    preview = current[ 0 ];
                }
                else
                {
                    current = new List< string >();
                    if( _currentSettings != null )
                    {
                        _currentSettings[ group ] = current;
                    }
                }

                ImGui.SetNextItemWidth( 200 * ImGuiHelpers.GlobalScale );
                using( var c = ImRaii.Combo( "##group", preview ) )
                {
                    if( c )
                    {
                        foreach( var s in list )
                        {
                            var contained = current.Contains( s );
                            if( ImGui.Checkbox( s, ref contained ) )
                            {
                                if( contained )
                                {
                                    current.Add( s );
                                }
                                else
                                {
                                    current.Remove( s );
                                }
                            }
                        }
                    }
                }

                ImGui.SameLine();
                if( ImGui.Button( "Set##setting" ) )
                {
                    if( type == SelectType.Single )
                    {
                        _lastSettingsError = _pi
                           .GetIpcSubscriber< string, string, string, string, string,
                                PenumbraApiEc >( PenumbraIpc.LabelProviderTrySetModSetting ).InvokeFunc( _settingsCollection,
                                _settingsModDirectory, _settingsModName, group, current.Count > 0 ? current[ 0 ] : string.Empty );
                    }
                    else
                    {
                        _lastSettingsError = _pi
                           .GetIpcSubscriber< string, string, string, string, IReadOnlyList< string >,
                                PenumbraApiEc >( PenumbraIpc.LabelProviderTrySetModSettings ).InvokeFunc( _settingsCollection,
                                _settingsModDirectory, _settingsModName, group, current.ToArray() );
                    }
                }

                ImGui.SameLine();
                ImGui.TextUnformatted( group );
            }
        }
    }

    private string        _tempCollectionName        = string.Empty;
    private string        _tempCharacterName         = string.Empty;
    private bool          _forceOverwrite            = true;
    private string        _tempModName               = string.Empty;
    private PenumbraApiEc _lastTempError             = PenumbraApiEc.Success;
    private string        _lastCreatedCollectionName = string.Empty;
    private string        _tempGamePath              = "test/game/path.mtrl";
    private string        _tempFilePath              = "test/success.mtrl";
    private string        _tempManipulation          = string.Empty;


    private void DrawTemp()
    {
        using var _ = ImRaii.TreeNode( "Temp IPC" );
        if( !_ )
        {
            return;
        }

        ImGui.InputTextWithHint( "##tempCollection", "Collection Name...", ref _tempCollectionName, 128 );
        ImGui.InputTextWithHint( "##tempCollectionChar", "Collection Character...", ref _tempCharacterName, 32 );
        ImGui.InputTextWithHint( "##tempMod", "Temporary Mod Name...", ref _tempModName, 32 );
        ImGui.InputTextWithHint( "##tempGame", "Game Path...", ref _tempGamePath, 256 );
        ImGui.InputTextWithHint( "##tempFile", "File Path...", ref _tempFilePath, 256 );
        ImGui.InputTextWithHint( "##tempManip", "Manipulation Base64 String...", ref _tempManipulation, 256 );
        ImGui.Checkbox( "Force Character Collection Overwrite", ref _forceOverwrite );

        using var table = ImRaii.Table( string.Empty, 3, ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return;
        }

        DrawIntro( "Last Error", _lastTempError.ToString() );
        DrawIntro( "Last Created Collection", _lastCreatedCollectionName );
        DrawIntro( PenumbraIpc.LabelProviderCreateTemporaryCollection, "Create Temporary Collection" );
        if( ImGui.Button( "Create##Collection" ) )
        {
            ( _lastTempError, _lastCreatedCollectionName ) =
                _pi.GetIpcSubscriber< string, string, bool, (PenumbraApiEc, string) >( PenumbraIpc.LabelProviderCreateTemporaryCollection )
                   .InvokeFunc( _tempCollectionName, _tempCharacterName, _forceOverwrite );
        }

        DrawIntro( PenumbraIpc.LabelProviderRemoveTemporaryCollection, "Remove Temporary Collection from Character" );
        if( ImGui.Button( "Delete##Collection" ) )
        {
            _lastTempError = _pi.GetIpcSubscriber< string, PenumbraApiEc >( PenumbraIpc.LabelProviderRemoveTemporaryCollection )
               .InvokeFunc( _tempCharacterName );
        }

        DrawIntro( PenumbraIpc.LabelProviderAddTemporaryMod, "Add Temporary Mod to specific Collection" );
        if( ImGui.Button( "Add##Mod" ) )
        {
            _lastTempError = _pi
               .GetIpcSubscriber< string, string, Dictionary< string, string >, string, int, PenumbraApiEc >(
                    PenumbraIpc.LabelProviderAddTemporaryMod )
               .InvokeFunc( _tempModName, _tempCollectionName,
                    new Dictionary< string, string > { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue );
        }

        DrawIntro( PenumbraIpc.LabelProviderAddTemporaryModAll, "Add Temporary Mod to all Collections" );
        if( ImGui.Button( "Add##All" ) )
        {
            _lastTempError = _pi
               .GetIpcSubscriber< string, Dictionary< string, string >, string, int, PenumbraApiEc >(
                    PenumbraIpc.LabelProviderAddTemporaryModAll )
               .InvokeFunc( _tempModName, new Dictionary< string, string > { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue );
        }

        DrawIntro( PenumbraIpc.LabelProviderRemoveTemporaryMod, "Remove Temporary Mod from specific Collection" );
        if( ImGui.Button( "Remove##Mod" ) )
        {
            _lastTempError = _pi.GetIpcSubscriber< string, string, int, PenumbraApiEc >( PenumbraIpc.LabelProviderRemoveTemporaryMod )
               .InvokeFunc( _tempModName, _tempCollectionName, int.MaxValue );
        }

        DrawIntro( PenumbraIpc.LabelProviderRemoveTemporaryModAll, "Remove Temporary Mod from all Collections" );
        if( ImGui.Button( "Remove##ModAll" ) )
        {
            _lastTempError = _pi.GetIpcSubscriber< string, int, PenumbraApiEc >( PenumbraIpc.LabelProviderRemoveTemporaryModAll )
               .InvokeFunc( _tempModName, int.MaxValue );
        }
    }

    private void DrawTempCollections()
    {
        using var collTree = ImRaii.TreeNode( "Collections" );
        if( !collTree )
        {
            return;
        }

        using var table = ImRaii.Table( "##collTree", 4 );
        if( !table )
        {
            return;
        }

        foreach( var (character, collection) in Penumbra.TempMods.Collections )
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( character );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( collection.Name );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( collection.ResolvedFiles.Count.ToString() );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( collection.MetaCache?.Count.ToString() ?? "0" );
        }
    }

    private void DrawTempMods()
    {
        using var modTree = ImRaii.TreeNode( "Mods" );
        if( !modTree )
        {
            return;
        }

        using var table = ImRaii.Table( "##modTree", 5 );

        void PrintList( string collectionName, IReadOnlyList< Mod.TemporaryMod > list )
        {
            foreach( var mod in list )
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( mod.Name );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( mod.Priority.ToString() );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( collectionName );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( mod.Default.Files.Count.ToString() );
                if( ImGui.IsItemHovered() )
                {
                    using var tt = ImRaii.Tooltip();
                    foreach( var (path, file) in mod.Default.Files )
                    {
                        ImGui.TextUnformatted( $"{path} -> {file}" );
                    }
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted( mod.TotalManipulations.ToString() );
                if( ImGui.IsItemHovered() )
                {
                    using var tt = ImRaii.Tooltip();
                    foreach( var manip in mod.Default.Manipulations )
                    {
                        ImGui.TextUnformatted( manip.ToString() );
                    }
                }
            }
        }

        if( table )
        {
            PrintList( "All", Penumbra.TempMods.ModsForAllCollections );
            foreach( var (collection, list) in Penumbra.TempMods.Mods )
            {
                PrintList( collection.Name, list );
            }
        }
    }
}