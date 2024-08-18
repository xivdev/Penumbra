using OtterGui.Classes;
using Penumbra.Api.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever the mod root directory changes.
/// <list type="number">
///     <item>Parameter is the full path of the new directory. </item>
///     <item>Parameter is whether the new directory is valid. </item>
/// </list>
/// </summary>
public sealed class ModDirectoryChanged() : EventWrapper<string, bool, ModDirectoryChanged.Priority>(nameof(ModDirectoryChanged))
{
    public enum Priority
    {
        /// <seealso cref="PluginStateApi.ModDirectoryChanged"/>
        Api = 0,

        /// <seealso cref="UI.FileDialogService.OnModDirectoryChange"/>
        FileDialogService = 0,
    }
}
