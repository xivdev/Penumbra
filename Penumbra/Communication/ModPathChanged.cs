using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a mod is added, deleted, moved or reloaded.
/// <list type="number">
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the old directory on deletion, move or reload and null on addition. </item>
///     <item>Parameter is the new directory on addition, move or reload and null on deletion. </item>
/// </list>
/// </summary>
public sealed class ModPathChanged : EventWrapper<Action<ModPathChangeType, Mod, DirectoryInfo?, DirectoryInfo?>, ModPathChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModChangeAddition"/>
        CollectionCacheManagerAddition = -100,

        /// <seealso cref="PenumbraApi.ModPathChangeSubscriber"/>
        Api = 0,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModPathChange"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModExportManager.OnModPathChange"/>
        ModExportManager = 0,

        /// <seealso cref="Mods.ModFileSystem.OnModPathChange"/>
        ModFileSystem = 0,

        /// <seealso cref="Mods.Manager.ModManager.OnModPathChange"/>
        ModManager = 0,

        /// <seealso cref="Mods.Editor.ModMerger.OnModPathChange"/>
        ModMerger = 0,

        /// <seealso cref="UI.AdvancedWindow.ModEditWindow.OnModPathChange"/>
        ModEditWindow = 0,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModPathChange"/>
        CollectionStorage = 10,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModChangeRemoval"/>
        CollectionCacheManagerRemoval = 100,
    }
    public ModPathChanged()
        : base(nameof(ModPathChanged))
    { }

    public void Invoke(ModPathChangeType changeType, Mod mod, DirectoryInfo? oldModDirectory, DirectoryInfo? newModDirectory)
        => Invoke(this, changeType, mod, oldModDirectory, newModDirectory);
}
