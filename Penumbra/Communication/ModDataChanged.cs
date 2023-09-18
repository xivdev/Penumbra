using OtterGui.Classes;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever mod meta data or local data is changed.
/// <list type="number">
///     <item>Parameter is the type of data change for the mod, which can be multiple flags. </item>
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter is the old name of the mod in case of a name change, and null otherwise. </item>
/// </list> </summary>
public sealed class ModDataChanged : EventWrapper<Action<ModDataChangeType, Mod, string?>, ModDataChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModFileSystemSelector.OnModDataChange"/>
        ModFileSystemSelector = -10,

        /// <seealso cref="Mods.Manager.ModCacheManager.OnModDataChange"/>
        ModCacheManager = 0,

        /// <seealso cref="Mods.Manager.ModFileSystem.OnDataChange"/>
        ModFileSystem = 0,
    }

    public ModDataChanged()
        : base(nameof(ModDataChanged))
    { }

    public void Invoke(ModDataChangeType changeType, Mod mod, string? oldName)
        => Invoke(this, changeType, mod, oldName);
}
