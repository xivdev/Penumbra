using OtterGui.Classes;
using Penumbra.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever the mod root directory changes.
/// <list type="number">
///     <item>Parameter is the full path of the new directory. </item>
///     <item>Parameter is whether the new directory is valid. </item>
/// </list>
/// </summary>
public sealed class ModDirectoryChanged : EventWrapper<Action<string, bool>, ModDirectoryChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.ModDirectoryChanged"/>
        Api = 0,

        /// <seealso cref="UI.FileDialogService.OnModDirectoryChange"/>
        FileDialogService = 0,
    }

    public ModDirectoryChanged()
        : base(nameof(ModDirectoryChanged))
    { }

    public void Invoke(string newModDirectory, bool newDirectoryValid)
        => Invoke(this, newModDirectory, newDirectoryValid);
}
