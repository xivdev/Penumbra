using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary> Triggered whenever mods are prepared to be rediscovered. </summary>
public sealed class ModDiscoveryStarted : EventWrapper<Action, ModDiscoveryStarted.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModDiscoveryStarted"/>
        CollectionCacheManager = 0,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModDiscoveryStarted"/>
        CollectionStorage = 0,

        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.StoreCurrentSelection"/>
        ModFileSystemSelector = 200,
    }
    public ModDiscoveryStarted()
        : base(nameof(ModDiscoveryStarted))
    { }

    public void Invoke()
        => Invoke(this);
}
