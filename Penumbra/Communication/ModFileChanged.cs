using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;
using Penumbra.Mods.Editor;

namespace Penumbra.Communication;

/// <summary> Triggered whenever an existing file in a mod is overwritten by Penumbra. </summary>
public sealed class ModFileChanged(Logger log) : EventBase<ModFileChanged.Arguments, ModFileChanged.Priority>(nameof(ModFileChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.OnModFileChanged"/>
        Api = int.MinValue,

        /// <seealso cref="Interop.Services.RedrawService.OnModFileChanged"/>
        RedrawService = -50,

        /// <seealso cref="Collections.Manager.CollectionStorage.OnModFileChanged"/>
        CollectionStorage = 0,
    }

    /// <summary> The arguments for a ModFileChanged event. </summary>
    /// <param name="Mod"> The changed mod. </param>
    /// <param name="File"> The file registry of the changed file. </param>
    public readonly record struct Arguments(Mod Mod, FileRegistry File);
}
