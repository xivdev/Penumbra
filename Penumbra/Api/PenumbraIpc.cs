using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Penumbra.GameData.Enums;

namespace Penumbra.Api;

public partial class PenumbraIpc : IDisposable
{
    internal readonly IPenumbraApi Api;

    public PenumbraIpc( DalamudPluginInterface pi, IPenumbraApi api )
    {
        Api = api;

        InitializeGeneralProviders( pi );
        InitializeResolveProviders( pi );
        InitializeRedrawProviders( pi );
        InitializeChangedItemProviders( pi );
        InitializeDataProviders( pi );
        ProviderInitialized?.SendMessage();
    }

    public void Dispose()
    {
        DisposeDataProviders();
        DisposeChangedItemProviders();
        DisposeRedrawProviders();
        DisposeResolveProviders();
        DisposeGeneralProviders();
        ProviderDisposed?.SendMessage();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderInitialized = "Penumbra.Initialized";
    public const string LabelProviderDisposed = "Penumbra.Disposed";
    public const string LabelProviderApiVersion = "Penumbra.ApiVersion";
    public const string LabelProviderGetModDirectory = "Penumbra.GetModDirectory";
    public const string LabelProviderGetConfiguration = "Penumbra.GetConfiguration";

    internal ICallGateProvider<object?>? ProviderInitialized;
    internal ICallGateProvider<object?>? ProviderDisposed;
    internal ICallGateProvider<int>? ProviderApiVersion;
    internal ICallGateProvider<string>? ProviderGetModDirectory;
    internal ICallGateProvider<IPluginConfiguration>? ProviderGetConfiguration;

    private void InitializeGeneralProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderInitialized = pi.GetIpcProvider<object?>( LabelProviderInitialized );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderInitialized}:\n{e}" );
        }

        try
        {
            ProviderDisposed = pi.GetIpcProvider<object?>( LabelProviderDisposed );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderDisposed}:\n{e}" );
        }

        try
        {
            ProviderApiVersion = pi.GetIpcProvider<int>( LabelProviderApiVersion );
            ProviderApiVersion.RegisterFunc( () => Api.ApiVersion );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderApiVersion}:\n{e}" );
        }

        try
        {
            ProviderGetModDirectory = pi.GetIpcProvider<string>( LabelProviderGetModDirectory );
            ProviderGetModDirectory.RegisterFunc( Api.GetModDirectory );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetModDirectory}:\n{e}" );
        }

        try
        {
            ProviderGetConfiguration = pi.GetIpcProvider<IPluginConfiguration>( LabelProviderGetConfiguration );
            ProviderGetConfiguration.RegisterFunc( Api.GetConfiguration );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetConfiguration}:\n{e}" );
        }
    }

    private void DisposeGeneralProviders()
    {
        ProviderGetConfiguration?.UnregisterFunc();
        ProviderGetModDirectory?.UnregisterFunc();
        ProviderApiVersion?.UnregisterFunc();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderRedrawName = "Penumbra.RedrawObjectByName";
    public const string LabelProviderRedrawIndex = "Penumbra.RedrawObjectByIndex";
    public const string LabelProviderRedrawObject = "Penumbra.RedrawObject";
    public const string LabelProviderRedrawAll = "Penumbra.RedrawAll";
    public const string LabelProviderObjectIsRedrawn = "Penumbra.ObjectIsRedrawn";

    internal ICallGateProvider<string, int, object>? ProviderRedrawName;
    internal ICallGateProvider<int, int, object>? ProviderRedrawIndex;
    internal ICallGateProvider<GameObject, int, object>? ProviderRedrawObject;
    internal ICallGateProvider<int, object>? ProviderRedrawAll;
    internal ICallGateProvider<string, string> ProviderObjectIsRedrawn;

    private static RedrawType CheckRedrawType( int value )
    {
        var type = ( RedrawType )value;
        if( Enum.IsDefined( type ) )
        {
            return type;
        }

        throw new Exception( "The integer provided for a Redraw Function was not a valid RedrawType." );
    }

    private void InitializeRedrawProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderRedrawName = pi.GetIpcProvider<string, int, object>( LabelProviderRedrawName );
            ProviderRedrawName.RegisterAction( ( s, i ) => Api.RedrawObject( s, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawName}:\n{e}" );
        }

        try
        {
            ProviderRedrawIndex = pi.GetIpcProvider<int, int, object>( LabelProviderRedrawIndex );
            ProviderRedrawIndex.RegisterAction( ( idx, i ) => Api.RedrawObject( idx, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawName}:\n{e}" );
        }

        try
        {
            ProviderRedrawObject = pi.GetIpcProvider<GameObject, int, object>( LabelProviderRedrawObject );
            ProviderRedrawObject.RegisterAction( ( o, i ) => Api.RedrawObject( o, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawObject}:\n{e}" );
        }

        try
        {
            ProviderRedrawAll = pi.GetIpcProvider<int, object>( LabelProviderRedrawAll );
            ProviderRedrawAll.RegisterAction( i => Api.RedrawAll( CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawAll}:\n{e}" );
        }

        try
        {
            ProviderObjectIsRedrawn = pi.GetIpcProvider<string, string>( LabelProviderObjectIsRedrawn );
            Api.ObjectIsRedrawn += Api_ObjectIsRedrawn;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderObjectIsRedrawn}:\n{e}" );
        }
    }

    private void Api_ObjectIsRedrawn( object? sender, EventArgs e )
    {
        ProviderObjectIsRedrawn.SendMessage( ( ( GameObject? )sender )?.Name.ToString() ?? "" );
    }

    private void DisposeRedrawProviders()
    {
        ProviderRedrawName?.UnregisterAction();
        ProviderRedrawIndex?.UnregisterAction();
        ProviderRedrawObject?.UnregisterAction();
        ProviderRedrawAll?.UnregisterAction();
        Api.ObjectIsRedrawn -= Api_ObjectIsRedrawn;
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderResolveDefault = "Penumbra.ResolveDefaultPath";
    public const string LabelProviderResolveCharacter = "Penumbra.ResolveCharacterPath";
    public const string LabelProviderGetDrawObjectInfo = "Penumbra.GetDrawObjectInfo";
    public const string LabelProviderReverseResolvePath = "Penumbra.ReverseResolvePath";

    internal ICallGateProvider<string, string>? ProviderResolveDefault;
    internal ICallGateProvider<string, string, string>? ProviderResolveCharacter;
    internal ICallGateProvider<IntPtr, (IntPtr, string)>? ProviderGetDrawObjectInfo;
    internal ICallGateProvider<string, string, string[]>? ProviderReverseResolvePath;

    private void InitializeResolveProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderResolveDefault = pi.GetIpcProvider<string, string>( LabelProviderResolveDefault );
            ProviderResolveDefault.RegisterFunc( Api.ResolvePath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveDefault}:\n{e}" );
        }

        try
        {
            ProviderResolveCharacter = pi.GetIpcProvider<string, string, string>( LabelProviderResolveCharacter );
            ProviderResolveCharacter.RegisterFunc( Api.ResolvePath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveCharacter}:\n{e}" );
        }

        try
        {
            ProviderGetDrawObjectInfo = pi.GetIpcProvider<IntPtr, (IntPtr, string)>( LabelProviderGetDrawObjectInfo );
            ProviderGetDrawObjectInfo.RegisterFunc( Api.GetDrawObjectInfo );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetDrawObjectInfo}:\n{e}" );
        }

        try
        {
            ProviderReverseResolvePath = pi.GetIpcProvider<string, string, string[]>( LabelProviderReverseResolvePath );
            ProviderReverseResolvePath.RegisterFunc( Api.ReverseResolvePath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetDrawObjectInfo}:\n{e}" );
        }
    }

    private void DisposeResolveProviders()
    {
        ProviderGetDrawObjectInfo?.UnregisterFunc();
        ProviderResolveDefault?.UnregisterFunc();
        ProviderResolveCharacter?.UnregisterFunc();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderChangedItemTooltip = "Penumbra.ChangedItemTooltip";
    public const string LabelProviderChangedItemClick = "Penumbra.ChangedItemClick";
    public const string LabelProviderGetChangedItems = "Penumbra.GetChangedItems";

    internal ICallGateProvider<ChangedItemType, uint, object>? ProviderChangedItemTooltip;
    internal ICallGateProvider<MouseButton, ChangedItemType, uint, object>? ProviderChangedItemClick;
    internal ICallGateProvider<string, IReadOnlyDictionary<string, object?>>? ProviderGetChangedItems;

    private void OnClick( MouseButton click, object? item )
    {
        var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
        ProviderChangedItemClick?.SendMessage( click, type, id );
    }

    private void OnTooltip( object? item )
    {
        var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
        ProviderChangedItemTooltip?.SendMessage( type, id );
    }

    private void InitializeChangedItemProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderChangedItemTooltip = pi.GetIpcProvider<ChangedItemType, uint, object>( LabelProviderChangedItemTooltip );
            Api.ChangedItemTooltip += OnTooltip;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemTooltip}:\n{e}" );
        }

        try
        {
            ProviderChangedItemClick = pi.GetIpcProvider<MouseButton, ChangedItemType, uint, object>( LabelProviderChangedItemClick );
            Api.ChangedItemClicked += OnClick;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderGetChangedItems = pi.GetIpcProvider<string, IReadOnlyDictionary<string, object?>>( LabelProviderGetChangedItems );
            ProviderGetChangedItems.RegisterFunc( Api.GetChangedItemsForCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }
    }

    private void DisposeChangedItemProviders()
    {
        ProviderGetChangedItems?.UnregisterFunc();
        Api.ChangedItemClicked -= OnClick;
        Api.ChangedItemTooltip -= OnTooltip;
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderGetMods = "Penumbra.GetMods";
    public const string LabelProviderGetCollections = "Penumbra.GetCollections";
    public const string LabelProviderCurrentCollectionName = "Penumbra.GetCurrentCollectionName";
    public const string LabelProviderDefaultCollectionName = "Penumbra.GetDefaultCollectionName";
    public const string LabelProviderCharacterCollectionName = "Penumbra.GetCharacterCollectionName";

    internal ICallGateProvider<IList<(string, string)>>? ProviderGetMods;
    internal ICallGateProvider<IList<string>>? ProviderGetCollections;
    internal ICallGateProvider<string>? ProviderCurrentCollectionName;
    internal ICallGateProvider<string>? ProviderDefaultCollectionName;
    internal ICallGateProvider<string, (string, bool)>? ProviderCharacterCollectionName;

    private void InitializeDataProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderGetMods = pi.GetIpcProvider<IList<(string, string)>>( LabelProviderGetMods );
            ProviderGetMods.RegisterFunc( Api.GetModList );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderGetCollections = pi.GetIpcProvider<IList<string>>( LabelProviderGetCollections );
            ProviderGetCollections.RegisterFunc( Api.GetCollections );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderCurrentCollectionName = pi.GetIpcProvider<string>( LabelProviderCurrentCollectionName );
            ProviderCurrentCollectionName.RegisterFunc( Api.GetCurrentCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderDefaultCollectionName = pi.GetIpcProvider<string>( LabelProviderDefaultCollectionName );
            ProviderDefaultCollectionName.RegisterFunc( Api.GetDefaultCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderCharacterCollectionName = pi.GetIpcProvider<string, (string, bool)>( LabelProviderCharacterCollectionName );
            ProviderCharacterCollectionName.RegisterFunc( Api.GetCharacterCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }
    }

    private void DisposeDataProviders()
    {
        ProviderGetMods?.UnregisterFunc();
        ProviderGetCollections?.UnregisterFunc();
        ProviderCurrentCollectionName?.UnregisterFunc();
        ProviderDefaultCollectionName?.UnregisterFunc();
        ProviderCharacterCollectionName?.UnregisterFunc();
    }
}