using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a mod is added, deleted, moved or reloaded. </summary>
public sealed class ModPathChanged(Logger log)
    : EventBase<ModPathChanged.Arguments, ModPathChanged.Priority>(nameof(ModPathChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="PcpService.OnModPathChange"/>
        PcpService = int.MinValue,

        /// <seealso cref="ModsApi.OnModPathChange"/>
        ApiMods = int.MinValue + 1,

        /// <seealso cref="ModSettingsApi.OnModPathChange"/>
        ApiModSettings = int.MinValue + 1,

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

    /// <summary> The arguments for a ModPathChanged event. </summary>
    /// <param name="Type"> The type of change for the mod. </param>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="OldDirectory"> The old directory on deletion, move or reload and null on addition. </param>
    /// <param name="NewDirectory"> The new directory on addition, move or reload and null on deletion. </param>
    public readonly record struct Arguments(ModPathChangeType Type, Mod Mod, DirectoryInfo? OldDirectory, DirectoryInfo? NewDirectory);
}
