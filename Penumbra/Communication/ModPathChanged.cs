using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Api.Api;
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
public sealed class ModPathChanged()
    : EventWrapper<ModPathChangeType, Mod, DirectoryInfo?, DirectoryInfo?, ModPathChanged.Priority>(nameof(ModPathChanged))
{
    public enum Priority
    {
        /// <seealso cref="ModsApi.OnModPathChange"/>
        ApiMods = int.MinValue,

        /// <seealso cref="ModSettingsApi.OnModPathChange"/>
        ApiModSettings = int.MinValue,

        /// <seealso cref="EphemeralConfig.OnModPathChanged"/>
        EphemeralConfig = -500,

        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnModChangeAddition"/>
        CollectionCacheManagerAddition = -100,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModPathChange"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModExportManager.OnModPathChange"/>
        ModExportManager = 0,

        /// <seealso cref="Mods.Manager.ModFileSystem.OnModPathChange"/>
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
}
