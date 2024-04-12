using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Api.Api;
using Penumbra.Mods;
using Penumbra.Mods.Editor;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever an existing file in a mod is overwritten by Penumbra.
/// <list type="number">
///     <item>Parameter is the changed mod. </item>
///     <item>Parameter file registry of the changed file. </item>
/// </list> </summary>
public sealed class ModFileChanged()
    : EventWrapper<Mod, FileRegistry, ModFileChanged.Priority>(nameof(ModFileChanged))
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
}
