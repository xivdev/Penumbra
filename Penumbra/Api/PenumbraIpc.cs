using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Penumbra.GameData.Enums;

namespace Penumbra.Api;

public partial class PenumbraIpc : IDisposable
{
    internal readonly IPenumbraApi Api;
    internal readonly IpcTester    Tester;

    public PenumbraIpc( DalamudPluginInterface pi, IPenumbraApi api )
    {
        Api    = api;
        Tester = new IpcTester( pi, this );
        InitializeGeneralProviders( pi );
        InitializeResolveProviders( pi );
        InitializeRedrawProviders( pi );
        InitializeChangedItemProviders( pi );
        InitializeDataProviders( pi );
        InitializeSettingProviders( pi );
        InitializeTempProviders( pi );
        ProviderInitialized?.SendMessage();
    }

    public void Dispose()
    {
        DisposeDataProviders();
        DisposeChangedItemProviders();
        DisposeRedrawProviders();
        DisposeResolveProviders();
        DisposeGeneralProviders();
        DisposeSettingProviders();
        DisposeTempProviders();
        ProviderDisposed?.SendMessage();
        Tester.Dispose();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderInitialized      = "Penumbra.Initialized";
    public const string LabelProviderDisposed         = "Penumbra.Disposed";
    public const string LabelProviderApiVersion       = "Penumbra.ApiVersion";
    public const string LabelProviderGetModDirectory  = "Penumbra.GetModDirectory";
    public const string LabelProviderGetConfiguration = "Penumbra.GetConfiguration";

    internal ICallGateProvider< object? >? ProviderInitialized;
    internal ICallGateProvider< object? >? ProviderDisposed;
    internal ICallGateProvider< int >?     ProviderApiVersion;
    internal ICallGateProvider< string >?  ProviderGetModDirectory;
    internal ICallGateProvider< string >?  ProviderGetConfiguration;

    private void InitializeGeneralProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderInitialized = pi.GetIpcProvider< object? >( LabelProviderInitialized );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderInitialized}:\n{e}" );
        }

        try
        {
            ProviderDisposed = pi.GetIpcProvider< object? >( LabelProviderDisposed );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderDisposed}:\n{e}" );
        }

        try
        {
            ProviderApiVersion = pi.GetIpcProvider< int >( LabelProviderApiVersion );
            ProviderApiVersion.RegisterFunc( () => Api.ApiVersion );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderApiVersion}:\n{e}" );
        }

        try
        {
            ProviderGetModDirectory = pi.GetIpcProvider< string >( LabelProviderGetModDirectory );
            ProviderGetModDirectory.RegisterFunc( Api.GetModDirectory );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetModDirectory}:\n{e}" );
        }

        try
        {
            ProviderGetConfiguration = pi.GetIpcProvider< string >( LabelProviderGetConfiguration );
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
    public const string LabelProviderRedrawObject      = "Penumbra.RedrawObject";
    public const string LabelProviderRedrawName        = "Penumbra.RedrawObjectByName";
    public const string LabelProviderRedrawIndex       = "Penumbra.RedrawObjectByIndex";
    public const string LabelProviderRedrawAll         = "Penumbra.RedrawAll";
    public const string LabelProviderGameObjectRedrawn = "Penumbra.GameObjectRedrawn";

    internal ICallGateProvider< string, int, object? >?     ProviderRedrawName;
    internal ICallGateProvider< GameObject, int, object? >? ProviderRedrawObject;
    internal ICallGateProvider< int, int, object? >?        ProviderRedrawIndex;
    internal ICallGateProvider< int, object? >?             ProviderRedrawAll;
    internal ICallGateProvider< IntPtr, int, object? >?     ProviderGameObjectRedrawn;

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
            ProviderRedrawName = pi.GetIpcProvider< string, int, object? >( LabelProviderRedrawName );
            ProviderRedrawName.RegisterAction( ( s, i ) => Api.RedrawObject( s, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawName}:\n{e}" );
        }

        try
        {
            ProviderRedrawObject = pi.GetIpcProvider< GameObject, int, object? >( LabelProviderRedrawObject );
            ProviderRedrawObject.RegisterAction( ( s, i ) => Api.RedrawObject( s, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawObject}:\n{e}" );
        }

        try
        {
            ProviderRedrawIndex = pi.GetIpcProvider< int, int, object? >( LabelProviderRedrawIndex );
            ProviderRedrawIndex.RegisterAction( ( idx, i ) => Api.RedrawObject( idx, CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawName}:\n{e}" );
        }

        try
        {
            ProviderRedrawAll = pi.GetIpcProvider< int, object? >( LabelProviderRedrawAll );
            ProviderRedrawAll.RegisterAction( i => Api.RedrawAll( CheckRedrawType( i ) ) );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRedrawAll}:\n{e}" );
        }

        try
        {
            ProviderGameObjectRedrawn =  pi.GetIpcProvider< IntPtr, int, object? >( LabelProviderGameObjectRedrawn );
            Api.GameObjectRedrawn     += OnGameObjectRedrawn;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGameObjectRedrawn}:\n{e}" );
        }
    }

    private void OnGameObjectRedrawn( IntPtr objectAddress, int objectTableIndex )
        => ProviderGameObjectRedrawn?.SendMessage( objectAddress, objectTableIndex );

    private void DisposeRedrawProviders()
    {
        ProviderRedrawName?.UnregisterAction();
        ProviderRedrawObject?.UnregisterAction();
        ProviderRedrawIndex?.UnregisterAction();
        ProviderRedrawAll?.UnregisterAction();
        Api.GameObjectRedrawn -= OnGameObjectRedrawn;
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderResolveDefault     = "Penumbra.ResolveDefaultPath";
    public const string LabelProviderResolveCharacter   = "Penumbra.ResolveCharacterPath";
    public const string LabelProviderGetDrawObjectInfo  = "Penumbra.GetDrawObjectInfo";
    public const string LabelProviderReverseResolvePath = "Penumbra.ReverseResolvePath";

    internal ICallGateProvider< string, string >?                  ProviderResolveDefault;
    internal ICallGateProvider< string, string, string >?          ProviderResolveCharacter;
    internal ICallGateProvider< IntPtr, (IntPtr, string) >?        ProviderGetDrawObjectInfo;
    internal ICallGateProvider< string, string, IList< string > >? ProviderReverseResolvePath;

    private void InitializeResolveProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderResolveDefault = pi.GetIpcProvider< string, string >( LabelProviderResolveDefault );
            ProviderResolveDefault.RegisterFunc( Api.ResolvePath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveDefault}:\n{e}" );
        }

        try
        {
            ProviderResolveCharacter = pi.GetIpcProvider< string, string, string >( LabelProviderResolveCharacter );
            ProviderResolveCharacter.RegisterFunc( Api.ResolvePath );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderResolveCharacter}:\n{e}" );
        }

        try
        {
            ProviderGetDrawObjectInfo = pi.GetIpcProvider< IntPtr, (IntPtr, string) >( LabelProviderGetDrawObjectInfo );
            ProviderGetDrawObjectInfo.RegisterFunc( Api.GetDrawObjectInfo );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetDrawObjectInfo}:\n{e}" );
        }

        try
        {
            ProviderReverseResolvePath = pi.GetIpcProvider< string, string, IList< string > >( LabelProviderReverseResolvePath );
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
        ProviderReverseResolvePath?.UnregisterFunc();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderChangedItemTooltip = "Penumbra.ChangedItemTooltip";
    public const string LabelProviderChangedItemClick   = "Penumbra.ChangedItemClick";
    public const string LabelProviderGetChangedItems    = "Penumbra.GetChangedItems";

    internal ICallGateProvider< ChangedItemType, uint, object? >?                 ProviderChangedItemTooltip;
    internal ICallGateProvider< MouseButton, ChangedItemType, uint, object? >?    ProviderChangedItemClick;
    internal ICallGateProvider< string, IReadOnlyDictionary< string, object? > >? ProviderGetChangedItems;

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
            ProviderChangedItemTooltip =  pi.GetIpcProvider< ChangedItemType, uint, object? >( LabelProviderChangedItemTooltip );
            Api.ChangedItemTooltip     += OnTooltip;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemTooltip}:\n{e}" );
        }

        try
        {
            ProviderChangedItemClick =  pi.GetIpcProvider< MouseButton, ChangedItemType, uint, object? >( LabelProviderChangedItemClick );
            Api.ChangedItemClicked   += OnClick;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderGetChangedItems = pi.GetIpcProvider< string, IReadOnlyDictionary< string, object? > >( LabelProviderGetChangedItems );
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
    public const string LabelProviderGetMods                 = "Penumbra.GetMods";
    public const string LabelProviderGetCollections          = "Penumbra.GetCollections";
    public const string LabelProviderCurrentCollectionName   = "Penumbra.GetCurrentCollectionName";
    public const string LabelProviderDefaultCollectionName   = "Penumbra.GetDefaultCollectionName";
    public const string LabelProviderCharacterCollectionName = "Penumbra.GetCharacterCollectionName";
    public const string LabelProviderGetMetaManipulations    = "Penumbra.GetMetaManipulations";

    internal ICallGateProvider< IList< (string, string) > >? ProviderGetMods;
    internal ICallGateProvider< IList< string > >?           ProviderGetCollections;
    internal ICallGateProvider< string >?                    ProviderCurrentCollectionName;
    internal ICallGateProvider< string >?                    ProviderDefaultCollectionName;
    internal ICallGateProvider< string, (string, bool) >?    ProviderCharacterCollectionName;
    internal ICallGateProvider< string, string >?            ProviderGetMetaManipulations;

    private void InitializeDataProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderGetMods = pi.GetIpcProvider< IList< (string, string) > >( LabelProviderGetMods );
            ProviderGetMods.RegisterFunc( Api.GetModList );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderGetCollections = pi.GetIpcProvider< IList< string > >( LabelProviderGetCollections );
            ProviderGetCollections.RegisterFunc( Api.GetCollections );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderCurrentCollectionName = pi.GetIpcProvider< string >( LabelProviderCurrentCollectionName );
            ProviderCurrentCollectionName.RegisterFunc( Api.GetCurrentCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderDefaultCollectionName = pi.GetIpcProvider< string >( LabelProviderDefaultCollectionName );
            ProviderDefaultCollectionName.RegisterFunc( Api.GetDefaultCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderCharacterCollectionName = pi.GetIpcProvider< string, (string, bool) >( LabelProviderCharacterCollectionName );
            ProviderCharacterCollectionName.RegisterFunc( Api.GetCharacterCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderChangedItemClick}:\n{e}" );
        }

        try
        {
            ProviderGetMetaManipulations = pi.GetIpcProvider< string, string >( LabelProviderGetMetaManipulations );
            ProviderGetMetaManipulations.RegisterFunc( Api.GetMetaManipulations );
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
        ProviderGetMetaManipulations?.UnregisterFunc();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderGetAvailableModSettings = "Penumbra.GetAvailableModSettings";
    public const string LabelProviderGetCurrentModSettings   = "Penumbra.GetCurrentModSettings";
    public const string LabelProviderTryInheritMod           = "Penumbra.TryInheritMod";
    public const string LabelProviderTrySetMod               = "Penumbra.TrySetMod";
    public const string LabelProviderTrySetModPriority       = "Penumbra.TrySetModPriority";
    public const string LabelProviderTrySetModSetting        = "Penumbra.TrySetModSetting";
    public const string LabelProviderTrySetModSettings       = "Penumbra.TrySetModSettings";

    internal ICallGateProvider< string, string, IDictionary< string, (IList< string >, Mods.SelectType) >? >? ProviderGetAvailableModSettings;

    internal ICallGateProvider< string, string, string, bool, (PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)?) >?
        ProviderGetCurrentModSettings;

    internal ICallGateProvider< string, string, string, bool, PenumbraApiEc >?                            ProviderTryInheritMod;
    internal ICallGateProvider< string, string, string, bool, PenumbraApiEc >?                            ProviderTrySetMod;
    internal ICallGateProvider< string, string, string, int, PenumbraApiEc >?                             ProviderTrySetModPriority;
    internal ICallGateProvider< string, string, string, string, string, PenumbraApiEc >?                  ProviderTrySetModSetting;
    internal ICallGateProvider< string, string, string, string, IReadOnlyList< string >, PenumbraApiEc >? ProviderTrySetModSettings;

    private void InitializeSettingProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderGetAvailableModSettings =
                pi.GetIpcProvider< string, string, IDictionary< string, (IList< string >, Mods.SelectType) >? >(
                    LabelProviderGetAvailableModSettings );
            ProviderGetAvailableModSettings.RegisterFunc( Api.GetAvailableModSettings );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetAvailableModSettings}:\n{e}" );
        }

        try
        {
            ProviderGetCurrentModSettings =
                pi.GetIpcProvider< string, string, string, bool, (PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)?) >(
                    LabelProviderGetCurrentModSettings );
            ProviderGetCurrentModSettings.RegisterFunc( Api.GetCurrentModSettings );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderGetCurrentModSettings}:\n{e}" );
        }

        try
        {
            ProviderTryInheritMod = pi.GetIpcProvider< string, string, string, bool, PenumbraApiEc >( LabelProviderTryInheritMod );
            ProviderTryInheritMod.RegisterFunc( Api.TryInheritMod );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderTryInheritMod}:\n{e}" );
        }

        try
        {
            ProviderTrySetMod = pi.GetIpcProvider< string, string, string, bool, PenumbraApiEc >( LabelProviderTrySetMod );
            ProviderTrySetMod.RegisterFunc( Api.TrySetMod );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderTrySetMod}:\n{e}" );
        }

        try
        {
            ProviderTrySetModPriority = pi.GetIpcProvider< string, string, string, int, PenumbraApiEc >( LabelProviderTrySetModPriority );
            ProviderTrySetModPriority.RegisterFunc( Api.TrySetModPriority );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderTrySetModPriority}:\n{e}" );
        }

        try
        {
            ProviderTrySetModSetting =
                pi.GetIpcProvider< string, string, string, string, string, PenumbraApiEc >( LabelProviderTrySetModSetting );
            ProviderTrySetModSetting.RegisterFunc( Api.TrySetModSetting );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderTrySetModSetting}:\n{e}" );
        }

        try
        {
            ProviderTrySetModSettings =
                pi.GetIpcProvider< string, string, string, string, IReadOnlyList< string >, PenumbraApiEc >( LabelProviderTrySetModSettings );
            ProviderTrySetModSettings.RegisterFunc( Api.TrySetModSettings );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderTrySetModSettings}:\n{e}" );
        }
    }

    private void DisposeSettingProviders()
    {
        ProviderGetAvailableModSettings?.UnregisterFunc();
        ProviderGetCurrentModSettings?.UnregisterFunc();
        ProviderTryInheritMod?.UnregisterFunc();
        ProviderTrySetMod?.UnregisterFunc();
        ProviderTrySetModPriority?.UnregisterFunc();
        ProviderTrySetModSetting?.UnregisterFunc();
        ProviderTrySetModSettings?.UnregisterFunc();
    }
}

public partial class PenumbraIpc
{
    public const string LabelProviderCreateTemporaryCollection = "Penumbra.CreateTemporaryCollection";
    public const string LabelProviderRemoveTemporaryCollection = "Penumbra.RemoveTemporaryCollection";
    public const string LabelProviderAddTemporaryModAll        = "Penumbra.AddTemporaryModAll";
    public const string LabelProviderAddTemporaryMod           = "Penumbra.AddTemporaryMod";
    public const string LabelProviderRemoveTemporaryModAll     = "Penumbra.RemoveTemporaryModAll";
    public const string LabelProviderRemoveTemporaryMod        = "Penumbra.RemoveTemporaryMod";

    internal ICallGateProvider< string, string, bool, (PenumbraApiEc, string) >? ProviderCreateTemporaryCollection;
    internal ICallGateProvider< string, PenumbraApiEc >?                         ProviderRemoveTemporaryCollection;

    internal ICallGateProvider< string, Dictionary< string, string >, string, int, PenumbraApiEc >?
        ProviderAddTemporaryModAll;

    internal ICallGateProvider< string, string, Dictionary< string, string >, string, int, PenumbraApiEc >?
        ProviderAddTemporaryMod;

    internal ICallGateProvider< string, int, PenumbraApiEc >?         ProviderRemoveTemporaryModAll;
    internal ICallGateProvider< string, string, int, PenumbraApiEc >? ProviderRemoveTemporaryMod;

    private void InitializeTempProviders( DalamudPluginInterface pi )
    {
        try
        {
            ProviderCreateTemporaryCollection =
                pi.GetIpcProvider< string, string, bool, (PenumbraApiEc, string) >( LabelProviderCreateTemporaryCollection );
            ProviderCreateTemporaryCollection.RegisterFunc( Api.CreateTemporaryCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderCreateTemporaryCollection}:\n{e}" );
        }

        try
        {
            ProviderRemoveTemporaryCollection =
                pi.GetIpcProvider< string, PenumbraApiEc >( LabelProviderRemoveTemporaryCollection );
            ProviderRemoveTemporaryCollection.RegisterFunc( Api.RemoveTemporaryCollection );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRemoveTemporaryCollection}:\n{e}" );
        }

        try
        {
            ProviderAddTemporaryModAll =
                pi.GetIpcProvider< string, Dictionary< string, string >, string, int, PenumbraApiEc >(
                    LabelProviderAddTemporaryModAll );
            ProviderAddTemporaryModAll.RegisterFunc( Api.AddTemporaryModAll );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderAddTemporaryModAll}:\n{e}" );
        }

        try
        {
            ProviderAddTemporaryMod =
                pi.GetIpcProvider< string, string, Dictionary< string, string >, string, int, PenumbraApiEc >(
                    LabelProviderAddTemporaryMod );
            ProviderAddTemporaryMod.RegisterFunc( Api.AddTemporaryMod );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderAddTemporaryMod}:\n{e}" );
        }

        try
        {
            ProviderRemoveTemporaryModAll = pi.GetIpcProvider< string, int, PenumbraApiEc >( LabelProviderRemoveTemporaryModAll );
            ProviderRemoveTemporaryModAll.RegisterFunc( Api.RemoveTemporaryModAll );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRemoveTemporaryModAll}:\n{e}" );
        }

        try
        {
            ProviderRemoveTemporaryMod = pi.GetIpcProvider< string, string, int, PenumbraApiEc >( LabelProviderRemoveTemporaryMod );
            ProviderRemoveTemporaryMod.RegisterFunc( Api.RemoveTemporaryMod );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Error registering IPC provider for {LabelProviderRemoveTemporaryMod}:\n{e}" );
        }
    }

    private void DisposeTempProviders()
    {
        ProviderCreateTemporaryCollection?.UnregisterFunc();
        ProviderRemoveTemporaryCollection?.UnregisterFunc();
        ProviderAddTemporaryModAll?.UnregisterFunc();
        ProviderAddTemporaryMod?.UnregisterFunc();
        ProviderRemoveTemporaryModAll?.UnregisterFunc();
        ProviderRemoveTemporaryMod?.UnregisterFunc();
    }
}