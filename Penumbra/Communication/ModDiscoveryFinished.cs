using OtterGui.Classes;
using Penumbra.Mods.Manager;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a new mod discovery has finished. </summary>
public sealed class ModDiscoveryFinished : EventWrapper<Action, ModDiscoveryFinished.Priority>
{
    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.RestoreLastSelection"/>
        ModFileSystemSelector = -200,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModDiscoveryFinished"/>
        CollectionCacheManager = -100,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModDiscoveryFinished"/>
        CollectionStorage = 0,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModDiscoveryFinished"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModFileSystem.Reload"/>
        ModFileSystem = 0,
    }

    public ModDiscoveryFinished()
        : base(nameof(ModDiscoveryFinished))
    { }

    public void Invoke()
        => Invoke(this);
}
