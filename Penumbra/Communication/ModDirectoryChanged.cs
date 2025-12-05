using Luna;
using Penumbra.Api.Api;
using Penumbra.UI.Classes;

namespace Penumbra.Communication;

/// <summary> Triggered whenever the mod root directory changes. </summary>
public sealed class ModDirectoryChanged(Logger log)
    : EventBase<ModDirectoryChanged.Arguments, ModDirectoryChanged.Priority>(nameof(ModDirectoryChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="PluginStateApi.ModDirectoryChanged"/>
        Api = 0,

        /// <seealso cref="UI.Classes.FileDialogService.OnModDirectoryChange"/>
        FileDialogService = 0,
    }

    /// <summary> The arguments for a ModFileChanged event. </summary>
    /// <param name="Directory"> The full path of the new mod directory. </param>
    /// <param name="Valid"> Whether the directory is valid. </param>
    public readonly record struct Arguments(string Directory, bool Valid);
}
