using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using System;
using System.Collections.Generic;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

using CurrentSettings = ValueTuple< PenumbraApiEc, (bool, int, IDictionary< string, IList< string > >, bool)? >;

public class PenumbraIpcProviders : IDisposable
{
    internal readonly IPenumbraApi Api;
    internal readonly IpcTester    Tester;

    // Plugin State
    internal readonly EventProvider                                Initialized;
    internal readonly EventProvider                                Disposed;
    internal readonly FuncProvider< int >                          ApiVersion;
    internal readonly FuncProvider< (int Breaking, int Features) > ApiVersions;

    // Configuration
    internal readonly FuncProvider< string >        GetModDirectory;
    internal readonly FuncProvider< string >        GetConfiguration;
    internal readonly EventProvider< string, bool > ModDirectoryChanged;

    // UI
    internal readonly EventProvider< string >                             PreSettingsDraw;
    internal readonly EventProvider< string >                             PostSettingsDraw;
    internal readonly EventProvider< ChangedItemType, uint >              ChangedItemTooltip;
    internal readonly EventProvider< MouseButton, ChangedItemType, uint > ChangedItemClick;

    // Redrawing
    internal readonly ActionProvider< RedrawType >             RedrawAll;
    internal readonly ActionProvider< GameObject, RedrawType > RedrawObject;
    internal readonly ActionProvider< int, RedrawType >        RedrawObjectByIndex;
    internal readonly ActionProvider< string, RedrawType >     RedrawObjectByName;
    internal readonly EventProvider< nint, int >               GameObjectRedrawn;

    // Game State
    internal readonly FuncProvider< nint, (nint, string) >            GetDrawObjectInfo;
    internal readonly FuncProvider< int, int >                        GetCutsceneParentIndex;
    internal readonly EventProvider< nint, string, nint, nint, nint > CreatingCharacterBase;
    internal readonly EventProvider< nint, string, nint >             CreatedCharacterBase;
    internal readonly EventProvider< nint, string, string >           GameObjectResourcePathResolved;

    // Resolve
    internal readonly FuncProvider< string, string >           ResolveDefaultPath;
    internal readonly FuncProvider< string, string >           ResolveInterfacePath;
    internal readonly FuncProvider< string, string >           ResolvePlayerPath;
    internal readonly FuncProvider< string, string, string >   ResolveCharacterPath;
    internal readonly FuncProvider< string, string, string[] > ReverseResolvePath;
    internal readonly FuncProvider< string, string[] >         ReverseResolvePathPlayer;

    // Collections
    internal readonly FuncProvider< IList< string > >                                GetCollections;
    internal readonly FuncProvider< string >                                         GetCurrentCollectionName;
    internal readonly FuncProvider< string >                                         GetDefaultCollectionName;
    internal readonly FuncProvider< string >                                         GetInterfaceCollectionName;
    internal readonly FuncProvider< string, (string, bool) >                         GetCharacterCollectionName;
    internal readonly FuncProvider< string, IReadOnlyDictionary< string, object? > > GetChangedItems;

    // Meta
    internal readonly FuncProvider< string >         GetPlayerMetaManipulations;
    internal readonly FuncProvider< string, string > GetMetaManipulations;

    // Mods
    internal readonly FuncProvider< IList< (string, string) > >                     GetMods;
    internal readonly FuncProvider< string, string, PenumbraApiEc >                 ReloadMod;
    internal readonly FuncProvider< string, PenumbraApiEc >                         AddMod;
    internal readonly FuncProvider< string, string, PenumbraApiEc >                 DeleteMod;
    internal readonly FuncProvider< string, string, (PenumbraApiEc, string, bool) > GetModPath;
    internal readonly FuncProvider< string, string, string, PenumbraApiEc >         SetModPath;

    // ModSettings
    internal readonly FuncProvider< string, string, IDictionary< string, (IList< string >, GroupType) >? >   GetAvailableModSettings;
    internal readonly FuncProvider< string, string, string, bool, CurrentSettings >                          GetCurrentModSettings;
    internal readonly FuncProvider< string, string, string, bool, PenumbraApiEc >                            TryInheritMod;
    internal readonly FuncProvider< string, string, string, bool, PenumbraApiEc >                            TrySetMod;
    internal readonly FuncProvider< string, string, string, int, PenumbraApiEc >                             TrySetModPriority;
    internal readonly FuncProvider< string, string, string, string, string, PenumbraApiEc >                  TrySetModSetting;
    internal readonly FuncProvider< string, string, string, string, IReadOnlyList< string >, PenumbraApiEc > TrySetModSettings;
    internal readonly EventProvider< ModSettingChange, string, string, bool >                                ModSettingChanged;

    // Temporary
    internal readonly FuncProvider< string, string, bool, (PenumbraApiEc, string) >                            CreateTemporaryCollection;
    internal readonly FuncProvider< string, PenumbraApiEc >                                                    RemoveTemporaryCollection;
    internal readonly FuncProvider< string, Dictionary< string, string >, string, int, PenumbraApiEc >         AddTemporaryModAll;
    internal readonly FuncProvider< string, string, Dictionary< string, string >, string, int, PenumbraApiEc > AddTemporaryMod;
    internal readonly FuncProvider< string, int, PenumbraApiEc >                                               RemoveTemporaryModAll;
    internal readonly FuncProvider< string, string, int, PenumbraApiEc >                                       RemoveTemporaryMod;

    public PenumbraIpcProviders( DalamudPluginInterface pi, IPenumbraApi api )
    {
        Api = api;

        // Plugin State
        Initialized = Ipc.Initialized.Provider( pi );
        Disposed    = Ipc.Disposed.Provider( pi );
        ApiVersion  = Ipc.ApiVersion.Provider( pi, DeprecatedVersion );
        ApiVersions = Ipc.ApiVersions.Provider( pi, () => Api.ApiVersion );

        // Configuration
        GetModDirectory     = Ipc.GetModDirectory.Provider( pi, Api.GetModDirectory );
        GetConfiguration    = Ipc.GetConfiguration.Provider( pi, Api.GetConfiguration );
        ModDirectoryChanged = Ipc.ModDirectoryChanged.Provider( pi, a => Api.ModDirectoryChanged += a, a => Api.ModDirectoryChanged -= a );

        // UI
        PreSettingsDraw    = Ipc.PreSettingsDraw.Provider( pi, a => Api.PreSettingsPanelDraw   += a, a => Api.PreSettingsPanelDraw        -= a );
        PostSettingsDraw   = Ipc.PostSettingsDraw.Provider( pi, a => Api.PostSettingsPanelDraw += a, a => Api.PostSettingsPanelDraw       -= a );
        ChangedItemTooltip = Ipc.ChangedItemTooltip.Provider( pi, () => Api.ChangedItemTooltip += OnTooltip, () => Api.ChangedItemTooltip -= OnTooltip );
        ChangedItemClick   = Ipc.ChangedItemClick.Provider( pi, () => Api.ChangedItemClicked   += OnClick, () => Api.ChangedItemClicked   -= OnClick );

        // Redrawing
        RedrawAll           = Ipc.RedrawAll.Provider( pi, Api.RedrawAll );
        RedrawObject        = Ipc.RedrawObject.Provider( pi, Api.RedrawObject );
        RedrawObjectByIndex = Ipc.RedrawObjectByIndex.Provider( pi, Api.RedrawObject );
        RedrawObjectByName  = Ipc.RedrawObjectByName.Provider( pi, Api.RedrawObject );
        GameObjectRedrawn   = Ipc.GameObjectRedrawn.Provider( pi, () => Api.GameObjectRedrawn += OnGameObjectRedrawn, () => Api.GameObjectRedrawn -= OnGameObjectRedrawn );

        // Game State
        GetDrawObjectInfo      = Ipc.GetDrawObjectInfo.Provider( pi, Api.GetDrawObjectInfo );
        GetCutsceneParentIndex = Ipc.GetCutsceneParentIndex.Provider( pi, Api.GetCutsceneParentIndex );
        CreatingCharacterBase = Ipc.CreatingCharacterBase.Provider( pi,
            () => Api.CreatingCharacterBase += CreatingCharacterBaseEvent,
            () => Api.CreatingCharacterBase -= CreatingCharacterBaseEvent );
        CreatedCharacterBase = Ipc.CreatedCharacterBase.Provider( pi,
            () => Api.CreatedCharacterBase += CreatedCharacterBaseEvent,
            () => Api.CreatedCharacterBase -= CreatedCharacterBaseEvent );
        GameObjectResourcePathResolved = Ipc.GameObjectResourcePathResolved.Provider( pi,
            () => Api.GameObjectResourceResolved += GameObjectResourceResolvedEvent,
            () => Api.GameObjectResourceResolved -= GameObjectResourceResolvedEvent );

        // Resolve
        ResolveDefaultPath       = Ipc.ResolveDefaultPath.Provider( pi, Api.ResolveDefaultPath );
        ResolveInterfacePath     = Ipc.ResolveInterfacePath.Provider( pi, Api.ResolveInterfacePath );
        ResolvePlayerPath        = Ipc.ResolvePlayerPath.Provider( pi, Api.ResolvePlayerPath );
        ResolveCharacterPath     = Ipc.ResolveCharacterPath.Provider( pi, Api.ResolvePath );
        ReverseResolvePath       = Ipc.ReverseResolvePath.Provider( pi, Api.ReverseResolvePath );
        ReverseResolvePathPlayer = Ipc.ReverseResolvePlayerPath.Provider( pi, Api.ReverseResolvePlayerPath );

        // Collections
        GetCollections             = Ipc.GetCollections.Provider( pi, Api.GetCollections );
        GetCurrentCollectionName   = Ipc.GetCurrentCollectionName.Provider( pi, Api.GetCurrentCollection );
        GetDefaultCollectionName   = Ipc.GetDefaultCollectionName.Provider( pi, Api.GetDefaultCollection );
        GetInterfaceCollectionName = Ipc.GetInterfaceCollectionName.Provider( pi, Api.GetInterfaceCollection );
        GetCharacterCollectionName = Ipc.GetCharacterCollectionName.Provider( pi, Api.GetCharacterCollection );
        GetChangedItems            = Ipc.GetChangedItems.Provider( pi, Api.GetChangedItemsForCollection );

        // Meta
        GetPlayerMetaManipulations = Ipc.GetPlayerMetaManipulations.Provider( pi, Api.GetPlayerMetaManipulations );
        GetMetaManipulations       = Ipc.GetMetaManipulations.Provider( pi, Api.GetMetaManipulations );

        // Mods
        GetMods    = Ipc.GetMods.Provider( pi, Api.GetModList );
        ReloadMod  = Ipc.ReloadMod.Provider( pi, Api.ReloadMod );
        AddMod     = Ipc.AddMod.Provider( pi, Api.AddMod );
        DeleteMod  = Ipc.DeleteMod.Provider( pi, Api.DeleteMod );
        GetModPath = Ipc.GetModPath.Provider( pi, Api.GetModPath );
        SetModPath = Ipc.SetModPath.Provider( pi, Api.SetModPath );

        // ModSettings
        GetAvailableModSettings = Ipc.GetAvailableModSettings.Provider( pi, Api.GetAvailableModSettings );
        GetCurrentModSettings   = Ipc.GetCurrentModSettings.Provider( pi, Api.GetCurrentModSettings );
        TryInheritMod           = Ipc.TryInheritMod.Provider( pi, Api.TryInheritMod );
        TrySetMod               = Ipc.TrySetMod.Provider( pi, Api.TrySetMod );
        TrySetModPriority       = Ipc.TrySetModPriority.Provider( pi, Api.TrySetModPriority );
        TrySetModSetting        = Ipc.TrySetModSetting.Provider( pi, Api.TrySetModSetting );
        TrySetModSettings       = Ipc.TrySetModSettings.Provider( pi, Api.TrySetModSettings );
        ModSettingChanged = Ipc.ModSettingChanged.Provider( pi,
            () => Api.ModSettingChanged += ModSettingChangedEvent,
            () => Api.ModSettingChanged -= ModSettingChangedEvent );

        // Temporary
        CreateTemporaryCollection = Ipc.CreateTemporaryCollection.Provider( pi, Api.CreateTemporaryCollection );
        RemoveTemporaryCollection = Ipc.RemoveTemporaryCollection.Provider( pi, Api.RemoveTemporaryCollection );
        AddTemporaryModAll        = Ipc.AddTemporaryModAll.Provider( pi, Api.AddTemporaryModAll );
        AddTemporaryMod           = Ipc.AddTemporaryMod.Provider( pi, Api.AddTemporaryMod );
        RemoveTemporaryModAll     = Ipc.RemoveTemporaryModAll.Provider( pi, Api.RemoveTemporaryModAll );
        RemoveTemporaryMod        = Ipc.RemoveTemporaryMod.Provider( pi, Api.RemoveTemporaryMod );

        Tester = new IpcTester( pi, this );

        Initialized.Invoke();
    }

    public void Dispose()
    {
        Tester.Dispose();

        // Plugin State
        Initialized.Dispose();
        ApiVersion.Dispose();
        ApiVersions.Dispose();

        // Configuration
        GetModDirectory.Dispose();
        GetConfiguration.Dispose();
        ModDirectoryChanged.Dispose();

        // UI
        PreSettingsDraw.Dispose();
        PostSettingsDraw.Dispose();
        ChangedItemTooltip.Dispose();
        ChangedItemClick.Dispose();

        // Redrawing
        RedrawAll.Dispose();
        RedrawObject.Dispose();
        RedrawObjectByIndex.Dispose();
        RedrawObjectByName.Dispose();
        GameObjectRedrawn.Dispose();

        // Game State
        GetDrawObjectInfo.Dispose();
        GetCutsceneParentIndex.Dispose();
        CreatingCharacterBase.Dispose();
        CreatedCharacterBase.Dispose();
        GameObjectResourcePathResolved.Dispose();

        // Resolve
        ResolveDefaultPath.Dispose();
        ResolveInterfacePath.Dispose();
        ResolvePlayerPath.Dispose();
        ResolveCharacterPath.Dispose();
        ReverseResolvePath.Dispose();
        ReverseResolvePathPlayer.Dispose();

        // Collections
        GetCollections.Dispose();
        GetCurrentCollectionName.Dispose();
        GetDefaultCollectionName.Dispose();
        GetInterfaceCollectionName.Dispose();
        GetCharacterCollectionName.Dispose();
        GetChangedItems.Dispose();

        // Meta
        GetPlayerMetaManipulations.Dispose();
        GetMetaManipulations.Dispose();

        // Mods
        GetMods.Dispose();
        ReloadMod.Dispose();
        AddMod.Dispose();
        DeleteMod.Dispose();
        GetModPath.Dispose();
        SetModPath.Dispose();

        // ModSettings
        GetAvailableModSettings.Dispose();
        GetCurrentModSettings.Dispose();
        TryInheritMod.Dispose();
        TrySetMod.Dispose();
        TrySetModPriority.Dispose();
        TrySetModSetting.Dispose();
        TrySetModSettings.Dispose();
        ModSettingChanged.Dispose();

        // Temporary
        CreateTemporaryCollection.Dispose();
        RemoveTemporaryCollection.Dispose();
        AddTemporaryModAll.Dispose();
        AddTemporaryMod.Dispose();
        RemoveTemporaryModAll.Dispose();
        RemoveTemporaryMod.Dispose();

        Disposed.Invoke();
        Disposed.Dispose();
    }

    // Wrappers
    private int DeprecatedVersion()
    {
        Penumbra.Log.Warning( $"{Ipc.ApiVersion.Label} is outdated. Please use {Ipc.ApiVersions.Label} instead." );
        return Api.ApiVersion.Breaking;
    }

    private void OnClick( MouseButton click, object? item )
    {
        var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
        ChangedItemClick.Invoke( click, type, id );
    }

    private void OnTooltip( object? item )
    {
        var (type, id) = ChangedItemExtensions.ChangedItemToTypeAndId( item );
        ChangedItemTooltip.Invoke( type, id );
    }

    private void OnGameObjectRedrawn( IntPtr objectAddress, int objectTableIndex )
        => GameObjectRedrawn.Invoke( objectAddress, objectTableIndex );

    private void CreatingCharacterBaseEvent( IntPtr gameObject, string collectionName, IntPtr modelId, IntPtr customize, IntPtr equipData )
        => CreatingCharacterBase.Invoke( gameObject, collectionName, modelId, customize, equipData );

    private void CreatedCharacterBaseEvent( IntPtr gameObject, string collectionName, IntPtr drawObject )
        => CreatedCharacterBase.Invoke( gameObject, collectionName, drawObject );

    private void GameObjectResourceResolvedEvent( IntPtr gameObject, string gamePath, string localPath )
        => GameObjectResourcePathResolved.Invoke( gameObject, gamePath, localPath );

    private void ModSettingChangedEvent( ModSettingChange type, string collection, string mod, bool inherited )
        => ModSettingChanged.Invoke( type, collection, mod, inherited );
}