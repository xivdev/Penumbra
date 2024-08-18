using Dalamud.Plugin;
using OtterGui.Services;
using Penumbra.Api.Api;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public sealed class IpcProviders : IDisposable, IApiService
{
    private readonly List<IDisposable> _providers;

    private readonly EventProvider _disposedProvider;
    private readonly EventProvider _initializedProvider;

    public IpcProviders(IDalamudPluginInterface pi, IPenumbraApi api)
    {
        _disposedProvider    = IpcSubscribers.Disposed.Provider(pi);
        _initializedProvider = IpcSubscribers.Initialized.Provider(pi);
        _providers =
        [
            IpcSubscribers.GetCollections.Provider(pi, api.Collection),
            IpcSubscribers.GetCollectionsByIdentifier.Provider(pi, api.Collection),
            IpcSubscribers.GetChangedItemsForCollection.Provider(pi, api.Collection),
            IpcSubscribers.GetCollection.Provider(pi, api.Collection),
            IpcSubscribers.GetCollectionForObject.Provider(pi, api.Collection),
            IpcSubscribers.SetCollection.Provider(pi, api.Collection),
            IpcSubscribers.SetCollectionForObject.Provider(pi, api.Collection),

            IpcSubscribers.ConvertTextureFile.Provider(pi, api.Editing),
            IpcSubscribers.ConvertTextureData.Provider(pi, api.Editing),

            IpcSubscribers.GetDrawObjectInfo.Provider(pi, api.GameState),
            IpcSubscribers.GetCutsceneParentIndex.Provider(pi, api.GameState),
            IpcSubscribers.SetCutsceneParentIndex.Provider(pi, api.GameState),
            IpcSubscribers.CreatingCharacterBase.Provider(pi, api.GameState),
            IpcSubscribers.CreatedCharacterBase.Provider(pi, api.GameState),
            IpcSubscribers.GameObjectResourcePathResolved.Provider(pi, api.GameState),

            IpcSubscribers.GetPlayerMetaManipulations.Provider(pi, api.Meta),
            IpcSubscribers.GetMetaManipulations.Provider(pi, api.Meta),

            IpcSubscribers.GetModList.Provider(pi, api.Mods),
            IpcSubscribers.InstallMod.Provider(pi, api.Mods),
            IpcSubscribers.ReloadMod.Provider(pi, api.Mods),
            IpcSubscribers.AddMod.Provider(pi, api.Mods),
            IpcSubscribers.DeleteMod.Provider(pi, api.Mods),
            IpcSubscribers.ModDeleted.Provider(pi, api.Mods),
            IpcSubscribers.ModAdded.Provider(pi, api.Mods),
            IpcSubscribers.ModMoved.Provider(pi, api.Mods),
            IpcSubscribers.GetModPath.Provider(pi, api.Mods),
            IpcSubscribers.SetModPath.Provider(pi, api.Mods),
            IpcSubscribers.GetChangedItems.Provider(pi, api.Mods),

            IpcSubscribers.GetAvailableModSettings.Provider(pi, api.ModSettings),
            IpcSubscribers.GetCurrentModSettings.Provider(pi, api.ModSettings),
            IpcSubscribers.TryInheritMod.Provider(pi, api.ModSettings),
            IpcSubscribers.TrySetMod.Provider(pi, api.ModSettings),
            IpcSubscribers.TrySetModPriority.Provider(pi, api.ModSettings),
            IpcSubscribers.TrySetModSetting.Provider(pi, api.ModSettings),
            IpcSubscribers.TrySetModSettings.Provider(pi, api.ModSettings),
            IpcSubscribers.ModSettingChanged.Provider(pi, api.ModSettings),
            IpcSubscribers.CopyModSettings.Provider(pi, api.ModSettings),

            IpcSubscribers.ApiVersion.Provider(pi, api),
            new FuncProvider<(int Major, int Minor)>(pi, "Penumbra.ApiVersions", () => api.ApiVersion), // backward compatibility
            new FuncProvider<int>(pi, "Penumbra.ApiVersion", () => api.ApiVersion.Breaking), // backward compatibility
            IpcSubscribers.GetModDirectory.Provider(pi, api.PluginState),
            IpcSubscribers.GetConfiguration.Provider(pi, api.PluginState),
            IpcSubscribers.ModDirectoryChanged.Provider(pi, api.PluginState),
            IpcSubscribers.GetEnabledState.Provider(pi, api.PluginState),
            IpcSubscribers.EnabledChange.Provider(pi, api.PluginState),

            IpcSubscribers.RedrawObject.Provider(pi, api.Redraw),
            IpcSubscribers.RedrawAll.Provider(pi, api.Redraw),
            IpcSubscribers.GameObjectRedrawn.Provider(pi, api.Redraw),

            IpcSubscribers.ResolveDefaultPath.Provider(pi, api.Resolve),
            IpcSubscribers.ResolveInterfacePath.Provider(pi, api.Resolve),
            IpcSubscribers.ResolveGameObjectPath.Provider(pi, api.Resolve),
            IpcSubscribers.ResolvePlayerPath.Provider(pi, api.Resolve),
            IpcSubscribers.ReverseResolveGameObjectPath.Provider(pi, api.Resolve),
            IpcSubscribers.ReverseResolvePlayerPath.Provider(pi, api.Resolve),
            IpcSubscribers.ResolvePlayerPaths.Provider(pi, api.Resolve),
            IpcSubscribers.ResolvePlayerPathsAsync.Provider(pi, api.Resolve),

            IpcSubscribers.GetGameObjectResourcePaths.Provider(pi, api.ResourceTree),
            IpcSubscribers.GetPlayerResourcePaths.Provider(pi, api.ResourceTree),
            IpcSubscribers.GetGameObjectResourcesOfType.Provider(pi, api.ResourceTree),
            IpcSubscribers.GetPlayerResourcesOfType.Provider(pi, api.ResourceTree),
            IpcSubscribers.GetGameObjectResourceTrees.Provider(pi, api.ResourceTree),
            IpcSubscribers.GetPlayerResourceTrees.Provider(pi, api.ResourceTree),

            IpcSubscribers.CreateTemporaryCollection.Provider(pi, api.Temporary),
            IpcSubscribers.DeleteTemporaryCollection.Provider(pi, api.Temporary),
            IpcSubscribers.AssignTemporaryCollection.Provider(pi, api.Temporary),
            IpcSubscribers.AddTemporaryModAll.Provider(pi, api.Temporary),
            IpcSubscribers.AddTemporaryMod.Provider(pi, api.Temporary),
            IpcSubscribers.RemoveTemporaryModAll.Provider(pi, api.Temporary),
            IpcSubscribers.RemoveTemporaryMod.Provider(pi, api.Temporary),

            IpcSubscribers.ChangedItemTooltip.Provider(pi, api.Ui),
            IpcSubscribers.ChangedItemClicked.Provider(pi, api.Ui),
            IpcSubscribers.PreSettingsTabBarDraw.Provider(pi, api.Ui),
            IpcSubscribers.PreSettingsDraw.Provider(pi, api.Ui),
            IpcSubscribers.PostEnabledDraw.Provider(pi, api.Ui),
            IpcSubscribers.PostSettingsDraw.Provider(pi, api.Ui),
            IpcSubscribers.OpenMainWindow.Provider(pi, api.Ui),
            IpcSubscribers.CloseMainWindow.Provider(pi, api.Ui),
        ];
        _initializedProvider.Invoke();
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
            provider.Dispose();
        _providers.Clear();
        _initializedProvider.Dispose();
        _disposedProvider.Invoke();
        _disposedProvider.Dispose();
    }
}
