using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Communication;

/// <summary> Triggered whenever mod meta data or local data is changed. </summary>
public sealed class ModDataChanged(Logger log) : EventBase<ModDataChanged.Arguments, ModDataChanged.Priority>(nameof(ModDataChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnModDataChange"/>
        ModFileSystemSelector = -10,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModDataChange"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModFileSystem.OnModDataChange"/>
        ModFileSystem = 0,

        /// <seealso cref="UI.ModsTab.ModPanelHeader.OnModDataChange"/>
        ModPanelHeader = 0,
    }

    /// <summary> The arguments for a ModDataChanged event. </summary>
    /// <param name="Type"> The type of data change for the mod, which can be multiple flags. </param>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="OldName"> The old name of the mod in case of a name change, and null otherwise. </param>
    public readonly record struct Arguments(ModDataChangeType Type, Mod Mod, string? OldName);
}
